using System;
using System.IO;
using System.Linq;
using Xbim.Common;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace XbimExtract
{
    class Program
    {
        /// <summary>
        /// Given a list of IFC entity labels in the source model, extracts them and inserts them in the target model
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            Params arguments = Params.ParseParams(args);
            if (arguments.IsValid)
            {
                try
                {
                    ReportProgressDelegate progDelegate = delegate(int percentProgress, object userState)
                    {
                        Console.Write("{0:D5}", percentProgress);
                        ResetCursor(Console.CursorTop); 
                    };
                    using (var source =  IfcStore.Open(arguments.SourceModelName))
                    {
                        Console.WriteLine("Reading {0}", arguments.SourceModelName);                      
                        Console.WriteLine();
                        Console.WriteLine("Extracting and copying to " + arguments.TargetModelName);
                        using (var target = IfcStore.Create(new XbimEditorCredentials(), source.IfcSchemaVesion,XbimStoreType.InMemoryModel))
                        {
                            if (arguments.IncludeContext) //add in the project and building to maintain a valid-ish file
                            {
                                var project = source.Instances.OfType<IIfcProject>().FirstOrDefault(); //get the spatial decomposition hierarchy
                                if (project != null)
                                {
                                    arguments.EntityLabels.Add(project.EntityLabel);
                                    foreach (var rel in project.IsDecomposedBy)
                                    {
                                        arguments.EntityLabels.Add(rel.EntityLabel);
                                    }
                                }
                            }
                            XbimInstanceHandleMap maps = new XbimInstanceHandleMap(source, target); //prevents the same thing being written twice
                            using (ITransaction txn = target.BeginTransaction())
                            {
                                foreach (var label in arguments.EntityLabels)
                                {
                                    var ent = source.Instances[label];
                                    if (ent != null)
                                    {
                                        target.InsertCopy(ent, maps, null,false, true);
                                    }
                                }  
                                txn.Commit();
                            }
                            File.Delete(arguments.TargetModelName);
                            Console.WriteLine("Saving to " + arguments.TargetModelName);
                            target.SaveAs(arguments.TargetModelName,null,progDelegate);
                            Console.WriteLine("Success");
                        }

                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
            Console.WriteLine("Press any key to exit");
            Console.ReadLine(); //wait for use to kill
        }

        private static void ResetCursor(int top)
        {
            try
            {
                // Can't reset outside of buffer, and should ignore when in quiet mode
                if (top >= Console.BufferHeight)
                    return;
                Console.SetCursorPosition(0, top);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
