using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xbim.Common;
using Xbim.Common.Exceptions;
using Xbim.Ifc;
using Xbim.Ifc2x3.Kernel;
using Xbim.Ifc2x3.ProductExtension;
using Xbim.ModelGeometry.Scene;

namespace XbimConvert
{
    class Program
    {
        private static ILogger Logger;
        private static Params arguments;

        static int Main(string[] args)
        {
            var serviceProvider = ConfigureServices();
            SetupXbimLogging(serviceProvider);
            IfcStore.ModelProviderFactory.UseHeuristicModelProvider();

            int totErrors = 0;
            // We need to use the logger early to initialise before we use EventTrace
            Logger.LogDebug("XbimConvert starting...");

            arguments = Params.ParseParams(args);

            if (!arguments.IsValid)
            {
                return -1;
            }

            var SubMode = SearchOption.TopDirectoryOnly;
            if (arguments.ProcessSubDir)
            {
                SubMode = SearchOption.AllDirectories;
            }

            var files = Directory.GetFiles(arguments.Specdir, arguments.Specpart, SubMode);
            if (files.Length == 0)
            {
                Console.WriteLine("Invalid IFC filename or filter: {0}, current directory is: {1}", args[0], Directory.GetCurrentDirectory());
                return -1;
            }

            long parseTime = 0;
            long geomTime = 0;
            foreach (var origFileName in files)
            {
                using (Logger.BeginScope(origFileName))
                {
                    try
                    {
                        Console.WriteLine("Starting conversion of {0}", origFileName);
                        Logger.LogInformation("Starting conversion of {0}", origFileName);
                        string xbimFileName = BuildFileName(origFileName, ".xbim");
                        System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
                        ReportProgressDelegate progDelegate = delegate (int percentProgress, object userState)
                        {
                            if (!arguments.IsQuiet)
                            {
                                Console.Write(string.Format("{0:D5} Converted", percentProgress));
                                ResetCursor(Console.CursorTop);
                            }
                        };
                        watch.Start();
                        using (var model = ParseModelFile(origFileName, xbimFileName, arguments.Caching))
                        {
                            parseTime = watch.ElapsedMilliseconds;
                            if (!arguments.NoGeometry)
                            {
                                // Process Geometry
                                var wexbimFile = BuildFileName(origFileName, ".wexbim");
                                var m3D = new Xbim3DModelContext(model);
                                try
                                {
                                    m3D.CreateContext(progDelegate: progDelegate);
                                }
                                catch (Exception ce)
                                {
                                    Console.WriteLine("Error compiling geometry\n" + ce.Message);
                                    Logger.LogError(ce, "Failed to compile geometry");
                                }

                                geomTime = watch.ElapsedMilliseconds - parseTime;

                                using (var wexbimStream = new FileStream(wexbimFile, FileMode.Create, FileAccess.Write))
                                {
                                    using (var wexBimBinaryWriter = new BinaryWriter(wexbimStream))
                                    {
                                        model.SaveAsWexBim(wexBimBinaryWriter);
                                        wexBimBinaryWriter.Close();
                                    }
                                    wexbimStream.Close();
                                }
                                Logger.LogInformation("Created Wexbim file {wexbim}", wexbimFile);
                                
                            }

                            model.SaveAs(xbimFileName);
                            Logger.LogInformation("Created Xbim File {xbim}", xbimFileName);
                            model.Dispose();
                            watch.Stop();
                        }

                        GC.Collect();
                        ResetCursor(Console.CursorTop + 1);
                        Console.WriteLine("Success. Parsed in " + parseTime + " ms, geometry meshed in " + geomTime + " ms, total time " + (parseTime + geomTime) + " ms.");
                    }
                    catch (Exception e)
                    {
                        if (e is XbimException || e is NotImplementedException)
                        {
                            // Errors we have already handled or know about. Keep details brief in the log
                            Logger.LogError("One or more errors converting {0}. Exiting...", origFileName);

                            DisplayError(string.Format("One or more errors converting {0}, {1}", origFileName, e.Message));
                        }
                        else
                        {
                            // Unexpected failures. Log exception details
                            Logger.LogError(String.Format("Fatal Error converting {0}. Exiting...", origFileName), e);

                            DisplayError(string.Format("Fatal Error converting {0}, {1}", origFileName, e.Message));
                        }
                    }
                }
            }
            GetInput();
            Logger.LogInformation("XbimConvert finished successfully...");
            return totErrors;
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


        private static void DisplayError(String message)
        {
            ResetCursor(Console.CursorTop + 1);
            Console.WriteLine(message);
        }

        private static IEnumerable<IfcProduct> GetProducts(IModel model)
        {
            IEnumerable<IfcProduct> result = null;

            switch (arguments.FilterType)
            {
                case FilterType.None:
                    result = model.Instances.OfType<IfcProduct>(true).Where(t => !(t is IfcFeatureElement)); //exclude openings and additions
                    Logger.LogDebug("All geometry items will be generated");
                    break;

                default:
                    throw new NotImplementedException();
            }
            return result;
        }

        private static IfcStore ParseModelFile(string inFileName, string xbimFileName, bool caching)
        {
            switch (Path.GetExtension(inFileName).ToLowerInvariant())
            {
                case ".ifc":
                case ".ifczip":
                case ".ifcxml":
                case ".xbim":

                    return IfcStore.Open(inFileName, progDelegate: (percentProgress, userState) =>
                    {
                        if (!arguments.IsQuiet)
                        {
                            Console.Write(string.Format("{0:D2}% Parsed", percentProgress));
                            ResetCursor(Console.CursorTop);
                        }
                    });

                default:
                    throw new NotImplementedException(String.Format("XbimConvert does not support {0} file formats currently", Path.GetExtension(inFileName)));
            }
        }

        private static string BuildFileName(string file, string extension)
        {
            if (arguments.KeepFileExtension)
            {
                return String.Concat(file, extension);
            }
            else
            {
                return Path.ChangeExtension(file, extension);
            }
        }


        private static void GetInput()
        {
            if (!arguments.IsQuiet)
            {
                Console.WriteLine("Press Enter to continue...");
                Console.ReadLine();
            }
        }

        private static void ResetCursor(int top)
        {
            try
            {
                // Can't reset outside of buffer, and should ignore when in quiet mode
                if (top >= Console.BufferHeight || arguments.IsQuiet == true)
                {
                    return;
                }

                Console.SetCursorPosition(0, top);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

}
