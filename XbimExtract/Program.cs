using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xbim.Common;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.IO;

namespace XbimExtract
{
    class Program
    {
        private static ILogger Logger;
        public static string AppName { get; } = Path.GetFileName(Assembly.GetExecutingAssembly().CodeBase);

        /// <summary>
        /// Given a list of IFC entity labels in the source model, extracts them and inserts them in the target model
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            var serviceProvider = ConfigureServices();
            SetupXbimLogging(serviceProvider);
            IfcStore.ModelProviderFactory.UseHeuristicModelProvider();

            Logger.LogInformation("{0} Started", AppName);

            var arguments = Params.ParseParams(args);

            if (arguments.IsValid)
            {
                try
                {
                    ReportProgressDelegate progDelegate = delegate(int percentProgress, object userState)
                    {
                        Console.Write("{0:D2}%", percentProgress);
                        ResetCursor(Console.CursorTop); 
                    };

                    using (var source =  IfcStore.Open(arguments.SourceModelName))
                    {
                        Logger.LogInformation("Reading {0}", arguments.SourceModelName);
                        Logger.LogInformation("Extracting and copying to " + arguments.TargetModelName);
                        using (var target = IfcStore.Create(source.SchemaVersion, XbimStoreType.InMemoryModel))
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
                                catch (Exception ex)
                                {
                                    Logger.LogError(ex, "Some entity labels don't exist in the source file.");
                                    return;
                                }
                                txn.Commit();
                            }

                            File.Delete(arguments.TargetModelName);
                            Logger.LogInformation("Saving to {filename}", arguments.TargetModelName);
                            target.SaveAs(arguments.TargetModelName,null,progDelegate);
                            Logger.LogInformation("Success");
                        }

                    }
                }
                catch (Exception e)
                {
                    Logger.LogError(e,"Failed to extract data");
                }
            }
            else
            {
                Logger.LogError("Supplied params are invalid");
            }

            Logger.LogInformation("{0} Ended", AppName);

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

        private static IServiceProvider ConfigureServices()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddLogging(conf => {
                conf.SetMinimumLevel(LogLevel.Debug);   // Set the minimum log level
                // Could also add File Logger here
                conf.AddConsole();
            });

            return serviceCollection.BuildServiceProvider();
        }

        private static void SetupXbimLogging(IServiceProvider serviceProvider)
        {
            var loggerFactory = serviceProvider.GetService<ILoggerFactory>();

            XbimLogging.LoggerFactory = loggerFactory;

            Logger = loggerFactory.CreateLogger<Program>();
            Logger.LogInformation("Logging set up");
        }
    }
}
