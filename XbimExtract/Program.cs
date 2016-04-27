using System;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;
using log4net.Config;
using Xbim.Common;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;

namespace XbimExtract
{
    class Program
    {
        public static ILog logger = LogManager.GetLogger(typeof(Program));

        public static string AppName { get; } = Path.GetFileName(Assembly.GetExecutingAssembly().CodeBase);

        /// <summary>
        /// Given a list of IFC entity labels in the source model, extracts them and inserts them in the target model
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            logger.InfoFormat("{0} Started", AppName);

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
                        logger.InfoFormat("Reading {0}", arguments.SourceModelName);
                        logger.InfoFormat("Extracting and copying to " + arguments.TargetModelName);
                        using (var target = IfcStore.Create(new XbimEditorCredentials(), source.IfcSchemaVersion,XbimStoreType.InMemoryModel))
                        {
                            if (arguments.IncludeContext) //add in the project and building to maintain a valid-ish file
                            {
                                var project = source.Instances.OfType<IIfcProject>().FirstOrDefault(); //get the spatial decomposition hierarchy
                                if (project != null)
                                {
                                    if (! arguments.EntityLabels.Contains(project.EntityLabel))
                                    {
                                        arguments.EntityLabels.Add(project.EntityLabel);
                                    }

                                    foreach (var rel in project.IsDecomposedBy)
                                    {
                                        if (! arguments.EntityLabels.Contains(rel.EntityLabel))
                                        {
                                            arguments.EntityLabels.Add(rel.EntityLabel);
                                        }
                                    }
                                }
                            }

                            XbimInstanceHandleMap maps = new XbimInstanceHandleMap(source, target); //prevents the same thing being written twice
                            using (ITransaction txn = target.BeginTransaction())
                            {
                                foreach (var label in arguments.EntityLabels)
                                {
                                    var ent = source.Instances.Where(x => x.EntityLabel == label).FirstOrDefault();

                                    if ((ent != null) &&
                                        (target.Instances.Count(x => x.EntityLabel == label) == 0))
                                    {
                                        target.InsertCopy(ent, maps, null, false, true);
                                    }
                                }  
                                txn.Commit();
                            }

                            File.Delete(arguments.TargetModelName);
                            logger.Info("Saving to " + arguments.TargetModelName);
                            target.SaveAs(arguments.TargetModelName,null,progDelegate);
                            logger.Info("Success");
                        }

                    }
                }
                catch (Exception e)
                {
                    logger.FatalFormat("{0}\n{1}", e.Message, e.StackTrace);
                }
            }
            else
            {
                logger.Error("Supplied params are invalid");
            }

            logger.InfoFormat("{0} Ended", AppName);
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
