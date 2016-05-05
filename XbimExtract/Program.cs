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
        public static ILog Logger = LogManager.GetLogger(typeof(Program));

        private static readonly string ApplicationName = Path.GetFileName(Assembly.GetExecutingAssembly().CodeBase);
        public static string AppName {
            get { return ApplicationName; }
        }

        /// <summary>
        /// Given a list of IFC entity labels in the source model, extracts them and inserts them in the target model
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            Logger.InfoFormat("{0} Started", AppName);

            var arguments = Params.ParseParams(args);

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
                        Logger.InfoFormat("Reading {0}", arguments.SourceModelName);
                        Logger.InfoFormat("Extracting and copying to " + arguments.TargetModelName);
                        using (var target = IfcStore.Create(source.IfcSchemaVersion,XbimStoreType.InMemoryModel))
                        {
                            var maps = new XbimInstanceHandleMap(source, target); //prevents the same thing being written twice
                            using (var txn = target.BeginTransaction())
                            {
                                try
                                {
                                    var toInsert =
                                        arguments.EntityLabels.Select(label => source.Instances[label]).ToList();
                                    var products = toInsert.OfType<IIfcProduct>().ToList();
                                    var others = toInsert.Except(products).ToList();

                                    if (products.Any())
                                        //this will insert products including their spatial containment, 
                                        //decomposition, properties and other related information
                                        target.InsertCopy(products, true, true, maps);
                                    if (others.Any())
                                        //if any of the specified objects were not products, insert them straight
                                        foreach (var entity in others)
                                            target.InsertCopy(entity, maps, null, false, true);
                                }
                                catch (Exception)
                                {
                                    Logger.Error("Some entity labels don't exist in the source file.");
                                    return;
                                }
                                txn.Commit();
                            }

                            File.Delete(arguments.TargetModelName);
                            Logger.Info("Saving to " + arguments.TargetModelName);
                            target.SaveAs(arguments.TargetModelName,null,progDelegate);
                            Logger.Info("Success");
                        }

                    }
                }
                catch (Exception e)
                {
                    Logger.FatalFormat("{0}\n{1}", e.Message, e.StackTrace);
                }
            }
            else
            {
                Logger.Error("Supplied params are invalid");
            }

            Logger.InfoFormat("{0} Ended", AppName);

#if DEBUG
            Console.WriteLine("Press any key...");
            Console.ReadKey();
#endif
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
