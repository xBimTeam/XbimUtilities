using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Ifc2x3.Kernel;
using Xbim.Ifc2x3.ProductExtension;
using Xbim.IO;
using Xbim.XbimExtensions.Interfaces;

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
                        Console.Write(string.Format("{0:D5}", percentProgress));
                        ResetCursor(Console.CursorTop); 
                    };
                    using (XbimModel source = new XbimModel())
                    {
                        Console.WriteLine(string.Format("Reading {0}", arguments.SourceModelName));
                        if (arguments.SourceIsXbimFile)
                            source.Open(arguments.SourceModelName);
                        else
                            source.CreateFrom(arguments.SourceModelName, null, progDelegate,true);
                        Console.WriteLine();
                        Console.WriteLine("Extracting and copying to " + arguments.TargetModelName);
                        using (XbimModel target = XbimModel.CreateTemporaryModel())
                        {
                            if (arguments.IncludeContext) //add in the project and building to maintain a valid-ish file
                            {
                                IfcProject project = source.IfcProject; //get the spatial decomposition hierarchy
                                arguments.EntityLabels.Add(project.EntityLabel);
                                foreach (var rel in project.IsDecomposedBy)
                                {
                                    arguments.EntityLabels.Add(rel.EntityLabel);
                                }
                                
                            }
                            XbimInstanceHandleMap maps = new XbimInstanceHandleMap(source, target); //prevents the same thing being written twice
                            using (XbimReadWriteTransaction txn = target.BeginTransaction())
                            {
                                foreach (var label in arguments.EntityLabels)
                                {
                                    IPersistIfcEntity ent = source.Instances[label];
                                    if (ent != null)
                                    {
                                        target.InsertCopy(ent, maps, txn, false);
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
