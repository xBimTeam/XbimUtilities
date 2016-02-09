using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xbim.Common.Logging;
using Xbim.IO;
using Xbim.ModelGeometry.Scene;
using System.Collections.Generic;
using Xbim.Common.Geometry;
using Xbim.Common.Step21;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;


namespace XbimRegression
{
    /// <summary>
    /// Class to process a folder of IFC files, capturing key metrics about their conversion
    /// </summary>
    public class BatchProcessor
    {
        private static readonly ILogger Logger = LoggerFactory.GetLogger();
        private const string XbimConvert = @"XbimConvert.exe";
        private static Object thisLock = new Object();
        private ProcessResultSet _lastReportSet = null;
        private ProcessResultSet _thisReportSet = null;
            
        Params _params;

        public BatchProcessor(Params arguments)
        {
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
            Logger.Debug("Conversion starting...");
            
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
            using (EventTrace eventTrace = LoggerFactory.CreateEventTrace())
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
                            Logger.Error(String.Format("Error compiling geometry: {0} - {1}", ifcFile, ex.Message), ex);

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
                    Logger.Error(String.Format("Problem converting file: {0}", ifcFile), ex);
                    result.Failed = true;
                }
                finally
                {
                    result.Errors = (from e in eventTrace.Events
                                     where (e.EventLevel == EventLevel.ERROR)
                                     select e).Count();
                    result.Warnings = (from e in eventTrace.Events
                                       where (e.EventLevel == EventLevel.WARN)
                                       select e).Count();
                    result.FileName = ifcFile;
                    if (eventTrace.Events.Count > 0)
                    {
                        CreateLogFile(ifcFile, eventTrace.Events);
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
            IfcStore model;
            //create a callback for progress
            switch (Path.GetExtension(ifcFileName).ToLowerInvariant())
            {
                case ".ifc":
                case ".ifczip":
                case ".ifcxml":
                   return IfcStore.Open(ifcFileName);                 
                default:
                    throw new NotImplementedException(String.Format("XbimConvert does not support converting {0} file formats currently", Path.GetExtension(ifcFileName)));
            }
           
        }


        private static String BuildFileName(string ifcFile, string extension)
        {
            return String.Concat(ifcFile, extension);
        }

        private void RemoveFiles(string ifcFile)
        {
            DeleteFile(BuildFileName(ifcFile, ".xbim"));
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
        private static void CreateLogFile(string ifcFile, IList<Event> events)
        {
            try
            {
                string logfile = String.Concat(ifcFile, ".log");
                using (StreamWriter writer = new StreamWriter(logfile, false))
                {
                    foreach (Event logEvent in events)
                    {
                        string message = SanitiseMessage(logEvent.Message, ifcFile);
                        writer.WriteLine("{0:yyyy-MM-dd HH:mm:ss} : {1:-5} {2}.{3} - {4}",
                            logEvent.EventTime,
                            logEvent.EventLevel.ToString(),
                            logEvent.Logger,
                            logEvent.Method,
                            message
                            );
                    }
                    writer.Flush();
                }
            }
            catch (Exception e)
            {
                Logger.Error(String.Format("Failed to create Log File for {0}", ifcFile), e);
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
    }

}
