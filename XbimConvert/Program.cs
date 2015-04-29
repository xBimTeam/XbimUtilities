using System;
using System.Collections.Generic;
using System.Linq;
using Xbim.IO;
using Xbim.ModelGeometry.Scene;
using System.IO;
using Xbim.Common.Logging;
using Xbim.Ifc2x3.Kernel;
using Xbim.Common.Exceptions;
using Xbim.Ifc2x3.ProductExtension;
using Xbim.XbimExtensions.Interfaces;
using XbimGeometry.Interfaces;

namespace XbimConvert
{
    class Program
    {
        private static readonly ILogger Logger = LoggerFactory.GetLogger();
        private static Params arguments;

        static int Main(string[] args)
        {
            int totErrors = 0;
            // We need to use the logger early to initialise before we use EventTrace
            Logger.Debug("XbimConvert starting...");

                arguments = Params.ParseParams(args);

                if (!arguments.IsValid)
                {
                    return -1;
                }

            var SubMode = SearchOption.TopDirectoryOnly;
            if (arguments.ProcessSubDir)
                SubMode = SearchOption.AllDirectories;
            var files = Directory.GetFiles(arguments.specdir, arguments.specpart, SubMode);
            if (files.Length == 0)
            {
                Console.WriteLine("Invalid IFC filename or filter: {0}, current directory is: {1}", args[0], Directory.GetCurrentDirectory());
                return -1;
            }

            long parseTime = 0;
            long geomTime = 0;
            foreach (var origFileName in files)
            {
                using (EventTrace eventTrace = LoggerFactory.CreateEventTrace())
                {
                try
                {
                    Console.WriteLine("Starting conversion of {0}", origFileName);
                        Logger.InfoFormat("Starting conversion of {0}", origFileName);
                        string xbimFileName = BuildFileName(origFileName, ".xbim");
                    //string xbimGeometryFileName = BuildFileName(arguments.IfcFileName, ".xbimGC");
                    System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
                    ReportProgressDelegate progDelegate = delegate(int percentProgress, object userState)
                    {
                        if (!arguments.IsQuiet)
                        {
                            Console.Write(string.Format("{0:D5} Converted", percentProgress));
                            ResetCursor(Console.CursorTop);
                        }
                    };
                    watch.Start();
                    using (XbimModel model = ParseModelFile(origFileName, xbimFileName, arguments.Caching))
                    {


                        parseTime = watch.ElapsedMilliseconds;
                        if (!arguments.NoGeometry)
                        {
                            // prods left null would default to excluding ifcfeatureElements in the mesher.
                            //IEnumerable<IfcProduct> prods = null;
                            //if (arguments.GeometryGeneration == Params.GeometryGenerationOptions.All)
                            //    prods = model.InstancesLocal.OfType<IfcProduct>(); // this one includes ifcfeatureElements instead.
                            //XbimMesher.GenerateGeometry(model, Logger, progDelegate, prods);

                            //if (arguments.GeomVersion == 1)
                            //    XbimMesher.GenerateGeometry(model, Logger, progDelegate);
                            //else //assume 2;

                            var m3D = new Xbim3DModelContext(model);
                            try
                            {
                                // m3D.CreateContext(progDelegate: progDelegate);
                                m3D.CreateContext(progDelegate: progDelegate, geomStorageType: XbimGeometryType.PolyhedronBinary);
                                string webFileName = BuildFileName(origFileName, ".wexbim");
                                var bw = new BinaryWriter(new FileStream(webFileName, FileMode.Create));
                                m3D.Write(bw);
                                bw.Close();

                            }
                            catch (Exception ce)
                            {
                                Console.WriteLine("Error compiling geometry\n" + ce.Message);
                            }


                            geomTime = watch.ElapsedMilliseconds - parseTime;
                            //if (arguments.GenerateScene)
                            //{
                            //    Stopwatch sceneTimer = new Stopwatch();
                            //    if (!arguments.IsQuiet)
                            //        Console.Write("Scene generation started...");
                            //    sceneTimer.Start();
                            //    XbimSceneBuilder sb = new XbimSceneBuilder();
                            //        sb.Options = arguments.GenerateSceneOptions;
                            //    string xbimSceneName = BuildFileName(xbimFileName, ".xbimScene");
                            //        sb.BuildGlobalScene(model, xbimSceneName,
                            //            !arguments.IsQuiet ? Logger : null
                            //            );
                            //    sceneTimer.Stop();
                            //    if (!arguments.IsQuiet)
                            //        Console.WriteLine(string.Format(" Completed in {0} ms", sceneTimer.ElapsedMilliseconds));

                            //}
                            
                        }
                        model.Close();
                        watch.Stop();
                    }
                   
                   // XbimModel.Terminate();
                    GC.Collect();
                    ResetCursor(Console.CursorTop + 1);
                    Console.WriteLine("Success. Parsed in " + parseTime + " ms, geometry meshed in " + geomTime + " ms, total time " + (parseTime+geomTime) + " ms.");
                }
                catch (Exception e)
                {
                        if (e is XbimException || e is NotImplementedException)
                    {
                         // Errors we have already handled or know about. Keep details brief in the log
                            Logger.ErrorFormat("One or more errors converting {0}. Exiting...", origFileName);
                            CreateLogFile(origFileName, eventTrace.Events);

                            DisplayError(string.Format("One or more errors converting {0}, {1}", origFileName, e.Message));
                    }
                    else
                    {
                        // Unexpected failures. Log exception details
                            Logger.Fatal(String.Format("Fatal Error converting {0}. Exiting...", origFileName), e);
                            CreateLogFile(origFileName, eventTrace.Events);

                            DisplayError(string.Format("Fatal Error converting {0}, {1}", origFileName, e.Message));
                    }
                }
                int errors = (from e in eventTrace.Events
                             where (e.EventLevel > EventLevel.INFO)
                             select e).Count();
                if (errors > 0)
                {
                        CreateLogFile(origFileName, eventTrace.Events);
                    }
                    totErrors += errors;
                }
            }
            GetInput();
            Logger.Info("XbimConvert finished successfully...");
            return totErrors;
        }

        private static void DisplayError(String message)
        {
            ResetCursor(Console.CursorTop + 1);
            Console.WriteLine(message);
        }
               
        private static IEnumerable<IfcProduct> GetProducts(XbimModel model)
        {
            IEnumerable<IfcProduct> result = null;

            switch (arguments.FilterType)
            {
                case FilterType.None:
                    result = model.Instances.OfType<IfcProduct>(true).Where(t => !(t is IfcFeatureElement)); //exclude openings and additions
                    Logger.Debug("All geometry items will be generated");
                    break;

                case FilterType.ElementID:
                    List<IfcProduct> list = new List<IfcProduct>();
                    list.Add(model.Instances[arguments.ElementIdFilter] as IfcProduct);
                    result = list;
                    Logger.DebugFormat("Only generating product element ID {0}", arguments.ElementIdFilter);
                    break;

                case FilterType.ElementType:
                    Type theType = arguments.ElementTypeFilter.Type;
                    result = model.Instances.Where<IfcProduct>(i => i.GetType() == theType);
                    Logger.DebugFormat("Only generating product elements of type '{0}'", arguments.ElementTypeFilter);
                    break;

                default:
                    throw new NotImplementedException();
            }
            return result;
        }

        private static XbimModel ParseModelFile(string inFileName, string xbimFileName, bool caching)
        {
            XbimModel model = new XbimModel();
           
            //create a callback for progress
            switch (Path.GetExtension(inFileName).ToLowerInvariant())
            {
                case ".ifc":
                case ".ifczip":
                case ".ifcxml":
                    model.CreateFrom(
                        inFileName,
                        xbimFileName,
                        delegate(int percentProgress, object userState)
                        {
                            if (!arguments.IsQuiet)
                            {
                                Console.Write(string.Format("{0:D2}% Parsed", percentProgress));
                                ResetCursor(Console.CursorTop);
                            }
                        },true, caching
                        );
                    break;
                case ".xbim":
                    
                    model = new XbimModel();
                    break;
                default:
                    throw new NotImplementedException(String.Format("XbimConvert does not support {0} file formats currently", Path.GetExtension(inFileName)));
            }
            
            return model;
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
                        string message = SanitiseMessage(logEvent.Message);
                        writer.WriteLine("{0:yyyy-MM-dd HH:mm:ss} : {1:-5} {2}.{3} - {4}",
                            logEvent.EventTime,
                            logEvent.EventLevel.ToString(),
                            logEvent.Logger,
                            logEvent.Method,
                            message
                            );
                    }
                    writer.Flush();
                    writer.Close();
                }
            }
            catch (Exception e)
            {
                Logger.Error(String.Format("Failed to create Log File for {0}", ifcFile), e);
            }
        }

        private static string SanitiseMessage(string message)
        {
            if (arguments.SanitiseLogs == false)
                return message;

            string modelPath = Path.GetDirectoryName(arguments.specdir);
            string currentPath = Environment.CurrentDirectory;

            return message
                .Replace(modelPath, String.Empty)
                .Replace(currentPath, String.Empty);
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
