using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xbim.Common.Logging;
using Xbim.Ifc2x3.Extensions;
using Xbim.Ifc2x3.GeometricModelResource;
using Xbim.Ifc2x3.GeometryResource;
using Xbim.Ifc2x3.IO;
using Xbim.Ifc2x3.Kernel;
using Xbim.Ifc2x3.ProductExtension;
using Xbim.Ifc2x3.RepresentationResource;
using Xbim.Ifc2x3.TopologyResource;
using Xbim.IO;
using Xbim.ModelGeometry.Scene;

namespace XbimMetrics
{
    class Program
    {
       
        /// <summary>
        /// Retrieves model metrics for future analysis
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            Params arguments = Params.ParseParams(args);
            if (arguments.IsValid)
            {
                if (!arguments.SourceIsXbimFile) //need to parse the file
                {
                    using (EventTrace eventTrace = LoggerFactory.CreateEventTrace())
                    {
                        using (var model = new XbimModel())
                        {
                            model.CreateFrom(arguments.SourceModelName,null,null,true);
                            var m3D = new Xbim3DModelContext(model);
                            arguments.SourceModelName = Path.ChangeExtension(arguments.SourceModelName, ".xbim");
                            m3D.CreateContext();
                            model.Close();
                        }
                        CreateLogFile(arguments.SourceModelName, eventTrace.Events);
                    }
                }
                Console.WriteLine("Analysing....."); 
                var metrics = new XbimModelMetrics();
                using (var model = new XbimModel())
                {
                    model.Open(arguments.SourceModelName);

                    metrics["Number Of IfcProducts"] = model.Instances.OfType<IfcProduct>().Count();
                    var shapeDefinitions = model.Instances.OfType<IfcProductDefinitionShape>().ToList();
                    metrics["Number Of IfcProductDefinitionShape"] = shapeDefinitions.Count();
                    var rep = shapeDefinitions.SelectMany(shape=>shape.Representations.SelectMany(a=>a.Items).Where(s=>s.IsSolidModel()));
                    metrics["Number Of Shape Items"] = rep.Count();

                    metrics["Number Of IfcCartesianPoint"] = model.Instances.OfType<IfcCartesianPoint>().Count();
                    metrics["Number Of IfcFaceBasedSurfaceModel"] = model.Instances.OfType<IfcFaceBasedSurfaceModel>().Count();
                    metrics["Number Of IfcShellBasedSurfaceModel"] = model.Instances.OfType<IfcShellBasedSurfaceModel>().Count();                 
                    metrics["Number Of IfcSolidModel"] = model.Instances.OfType<IfcSolidModel>().Count();
                    metrics["Number Of IfcHalfSpaceSolid"] = model.Instances.OfType<IfcHalfSpaceSolid>().Count();
                    metrics["Number Of IfcBooleanResult"] = model.Instances.OfType<IfcBooleanResult>().Count();
                    metrics["Number Of IfcMappedItem"] = model.Instances.OfType<IfcMappedItem>().Count();
                    double totalVoids = 0;
                    double maxVoidsPerElement = 0;
                    double totalElements = 0;
                    foreach (var relVoids in model.Instances.OfType<IfcRelVoidsElement>().GroupBy(r=>r.RelatingBuildingElement))
                    {
                        var voidCount = relVoids.Count();
                        totalVoids += voidCount;
                        totalElements++;
                        double newmaxVoidsPerElement = Math.Max(maxVoidsPerElement, voidCount);
                        if (newmaxVoidsPerElement != maxVoidsPerElement)
                        {
                            maxVoidsPerElement = newmaxVoidsPerElement;
                            Console.WriteLine("Element is #{0}",relVoids.Key.EntityLabel);
                        }


                    }
                    metrics["Total Of Openings Cut"] = totalVoids;
                    metrics["Number Of Element with Openings"] = totalElements;
                    metrics["Maximum openings in an Element"] = maxVoidsPerElement;
                    metrics["Average openings in an Element"] = totalVoids == 0 ? 0.0 : totalElements / totalVoids;

                    //if the model has shape geometry report on that
                    var context = new Xbim3DModelContext(model);
                    metrics["Number of Shape Geometries"] = context.ShapeGeometries().Count();
                    metrics["Number of Shape Instances"] = context.ShapeInstances().Count();
                   // metrics["Number of Surface Styles"] = context.().Count();
                    model.Close();
                }
                foreach (var metric in metrics)
                {
                    Console.WriteLine("{0} = {1}",metric.Key,metric.Value);
                }
            }

            Console.WriteLine("Press Enter to continue...");
            Console.ReadLine();
        }

        private static void CreateLogFile(string ifcFile, IList<Event> events)
        {

            string logfile = Path.ChangeExtension(ifcFile, ".log");
            using (StreamWriter writer = new StreamWriter(logfile, false))
            {
                foreach (Event logEvent in events)
                {
                    string message = SanitiseMessage(logEvent.Message, ifcFile);
                    writer.WriteLine("{0:yyyy-MM-dd HH:mm:ss} : {1} {2}.{3} - {4}",
                        logEvent.EventTime,
                        logEvent.EventLevel,
                        logEvent.Logger,
                        logEvent.Method,
                        message
                        );
                }
                writer.Flush();
                writer.Close();
            }

        }

        private static string SanitiseMessage(string message, string xbimFile)
        {
            string modelPath = Path.GetDirectoryName(xbimFile);
            string currentPath = Environment.CurrentDirectory;
            return message
                .Replace(modelPath, String.Empty)
                .Replace(currentPath, String.Empty);
        }
    }
}
