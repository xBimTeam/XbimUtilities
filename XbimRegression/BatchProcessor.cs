using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xbim.Common;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;

namespace XbimRegression
{
    /// <summary>
    /// Class to process a folder of IFC files, capturing key metrics about their conversion
    /// </summary>
    public class BatchProcessor
    {
        private static ILogger Logger;
        private static readonly InMemoryLoggerProvider inMemoryProvider = new InMemoryLoggerProvider();
        private static Object thisLock = new Object();
        private ProcessResultSet _lastReportSet = null;
        private ProcessResultSet _thisReportSet = null;
            
        Params _params;

        public BatchProcessor(Params arguments)
        {
            var serviceProvider = ConfigureServices();
            SetupXbimLogging(serviceProvider);
            IfcStore.ModelProviderFactory.UseHeuristicModelProvider();
            _params = arguments;
            _thisReportSet = new ProcessResultSet();
        }

        public Params Params
        {
            get
            {
                return _params;
            }
        }

        public void Run()
        {
            DirectoryInfo di = new DirectoryInfo(Params.TestFileRoot);
            
            //get last report
            FileInfo lastReport = di.GetFiles("XbimRegression_*.csv").OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
            if (lastReport != null)
            {
                _lastReportSet = new ProcessResultSet();
                Console.WriteLine("Loading last report file");
                _lastReportSet.LoadFromFile(lastReport.FullName);
            }
            

            // We need to use the logger early to initialise before we use EventTrace
            Logger.LogDebug("Conversion starting...");
            
            //get files to process
            FileInfo[] toProcess = di.GetFiles("*.IFC", SearchOption.AllDirectories);
            foreach (var file in toProcess)
            {
                Console.WriteLine("Processing {0}", file);
                ProcessResult result = ProcessFile(file.FullName);
                if (!result.Failed)
                {
                    Console.WriteLine("Processed {0} : {1} errors, {2} Warnings in {3}ms. {4} IFC Elements & {5} Geometry Nodes.",
                        file, result.Errors, result.Warnings, result.TotalTime, result.Entities, result.GeometryEntries);
                }
                else
                {
                    Console.WriteLine("Processing failed for {0} after {1}ms.",
                        file, result.TotalTime);
                }

            }
            //write results to csv file
            Console.WriteLine("Creating report... ");
            String resultsFile = Path.Combine(Params.TestFileRoot, String.Format("XbimRegression_{0:yyyyMMdd-hhmmss}.csv", DateTime.Now));
            _thisReportSet.WriteToFile(resultsFile);
            ///Finished and wait...
            Console.WriteLine("Finished. Press Enter to continue...");
            Console.ReadLine();
        }

        private ProcessResult ProcessFile(string ifcFile)
        {
            RemoveFiles(ifcFile);  
            long geomTime = -1;  long parseTime = -1;
           // using (EventTrace eventTrace = LoggerFactory.CreateEventTrace())
            {
                ProcessResult result = new ProcessResult() { Errors = -1 };
                try
                {

                    Stopwatch watch = new Stopwatch();
                    watch.Start();
                    using (var model = ParseModelFile(ifcFile,Params.Caching))
                    {
                        parseTime = watch.ElapsedMilliseconds;
                        string xbimFilename = BuildFileName(ifcFile, ".xbim");
                        //add geometry
                        Xbim3DModelContext m3d = new Xbim3DModelContext(model);
                        try
                        {
                            m3d.CreateContext();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Error compiling geometry: {ifcFile} - {err}", ifcFile, ex.Message);

                        }
                        geomTime = watch.ElapsedMilliseconds - parseTime;
                        IStepFileHeader header = model.Header;
                        watch.Stop();
                        var ohs = model.Instances.OfType<IIfcOwnerHistory>().FirstOrDefault();
                        result = new ProcessResult()
                        {
                            ParseDuration = parseTime,
                            GeometryDuration = geomTime,
                            SceneDuration = 0,
                            FileName = ifcFile,
                            Entities = model.Instances.Count,
                            IfcSchema = header.FileSchema.Schemas.FirstOrDefault(),
                            IfcDescription = String.Format("{0}, {1}", header.FileDescription.Description.FirstOrDefault(), header.FileDescription.ImplementationLevel),
                            //GeometryEntries = model.,
                            IfcLength = ReadFileLength(ifcFile),
                            XbimLength = ReadFileLength(xbimFilename),
                            SceneLength = 0,
                            IfcProductEntries = model.Instances.CountOf<IIfcProduct>(),
                            IfcSolidGeometries = model.Instances.CountOf<IIfcSolidModel>(),
                            IfcMappedGeometries = model.Instances.CountOf<IIfcMappedItem>(),
                            BooleanGeometries = model.Instances.CountOf<IIfcBooleanResult>(),
                            Application = ohs == null ? "Unknown" : ohs.OwningApplication.ToString(),
                        };               
                    }
                }

                catch (Exception ex)
                {
                    Logger.LogError(ex, "Problem converting file: {0}", ifcFile);
                    result.Failed = true;
                }
                finally
                {
                    result.Errors = (from e in inMemoryProvider.Messages
                                     where (e.Type == LogLevel.Error)
                                     select e).Count();
                    result.Warnings = (from e in inMemoryProvider.Messages
                                       where (e.Type == LogLevel.Warning)
                                       select e).Count();
                    result.FileName = ifcFile;
                    if (inMemoryProvider.Messages.Count > 0)
                    {
                        CreateLogFile(ifcFile, inMemoryProvider.Messages);
                    }
                    //add last reports pass/fail and save report to report set
                    if (_lastReportSet != null) 
                    {
                         result.LastTestFailed = _lastReportSet.Compare(result);//set last test pass/fail result
                    }
                    _thisReportSet.Add(result);
                    
                }
                return result;
            }
        }

        private static IfcStore ParseModelFile(string ifcFileName, bool caching)
        {
            switch (Path.GetExtension(ifcFileName).ToLowerInvariant())
            {
                case ".ifc":
                case ".ifczip":
                case ".ifcxml":
            //create a callback for progress
                   return IfcStore.Open(ifcFileName);                 
                default:
                    throw new NotImplementedException(String.Format("Xbim does not support converting {0} file formats currently", Path.GetExtension(ifcFileName)));
            }
           
        }


        private static String BuildFileName(string ifcFile, string extension)
        {
            return String.Concat(ifcFile, extension);
        }

        private void RemoveFiles(string ifcFile)
        {
            DeleteFile(BuildFileName(ifcFile, ".xbim"));
            DeleteFile(BuildFileName(ifcFile, ".jfm"));
            DeleteFile(BuildFileName(ifcFile, ".xbimScene"));
            DeleteFile(BuildFileName(ifcFile, ".log"));
        }

        private void DeleteFile(string file)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
            catch (Exception)
            {
                // ignore
            }
        }


        private static long ReadFileLength(string file)
        {
            long length = 0;
            FileInfo fi = new FileInfo(file);
            if (fi.Exists)
            {
                length = fi.Length;
            }
            return length;
        }
        private static void CreateLogFile(string ifcFile, IList<LogMessage> events)
        {
            try
            {
                string logfile = String.Concat(ifcFile, ".log");
                using (StreamWriter writer = new StreamWriter(logfile, false))
                {
                    foreach (LogMessage logEvent in events)
                    {
                        string message = SanitiseMessage(logEvent.Message, ifcFile);
                        writer.WriteLine("{0:yyyy-MM-dd HH:mm:ss} : {1:-5} {2}.{3} - {4}",
                            logEvent.Timestamp,
                            logEvent.Type.ToString(),
                            logEvent.Category,
                            "<no method>",
                            message
                            );
                    }
                    writer.Flush();
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Failed to create Log File for {0}", ifcFile);
            }
        }

        private static string SanitiseMessage(string message, string ifcFileName)
        {
            string modelPath = Path.GetDirectoryName(ifcFileName);
            string currentPath = Environment.CurrentDirectory;

            return message
                .Replace(modelPath, String.Empty)
                .Replace(currentPath, String.Empty);
        }

        private static IServiceProvider ConfigureServices()
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddLogging(conf => {
                conf.SetMinimumLevel(LogLevel.Debug);   // Set the minimum log level
                // Could also add File Logger here
                conf.AddConsole();
                conf.AddProvider(inMemoryProvider);
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
