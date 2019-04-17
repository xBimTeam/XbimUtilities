using System;
using System.Collections.Generic;
using System.IO;

namespace XbimExtract
{
    public class Params
    {
        public string SourceModelName;
        public string TargetModelName;
        public List<int> EntityLabels;
        public bool IsValid { get; set; }
        public bool SourceIsXbimFile { get; set; }
        /// <summary>
        /// Include project and other context objects to create a vlaid schema (ish)
        /// </summary>
        public bool IncludeProject { get; set; }
        public bool KeepLabels { get; set; }
        public bool IncludeGeometry { get; set; }
        public static Params ParseParams(string[] args)
        {
            Params result = new Params(args);

            return result;
        }

        private Params(string[] args)
        {

            try
            {
                if (args.Length < 3) throw new Exception("Invalid number of Parameters, 3 required");
                SourceModelName = GetModelFileName(args[0], ".ifc");
                if (!File.Exists(SourceModelName)) throw new Exception(SourceModelName + " does not exist");
                TargetModelName = GetModelFileName(args[1], ".xbim");
                SourceIsXbimFile = Path.GetExtension(SourceModelName).ToLower() == ".xbim";
                EntityLabels = new List<int>(args.Length - 2);
                IncludeProject = true;
                IncludeGeometry = true;
                KeepLabels = false;
                for (int i = 2; i < args.Length; i++)
                {
                    var entity = args[i];
                    if(entity.StartsWith("--")) //its a param
                    {
                        var param = entity.TrimStart('-').ToUpperInvariant();
                        var parts = param.Split('=');
                        var left = parts[0];
                        var right = (parts.Length==2 ? parts[1].ToLowerInvariant():"true");
                        switch (left)
                        {
                            case "INCLUDEPROJECT":
                                IncludeProject = (right=="true");
                                break;
                            case "KEEPLABELS":
                                KeepLabels = (right == "true");
                                break;
                            case "INCLUDEGEOMETRY":
                                IncludeGeometry = (right == "true");
                                break;
                            default:
                                break;
                        }

                    }
                    else if (entity.Contains("-"))
                    {
                        var parts = entity.Split('-');
                        Int32 bottom = Int32.Parse(parts[0]);
                        Int32 top = Int32.Parse(parts[1]);
                        Console.WriteLine("Including entities {0}..{1} inclusive", bottom, top);
                        for (Int32 j=bottom; j<=top; ++j)
                        {
                            EntityLabels.Add(j);
                        }
                    }
                    else {
                        EntityLabels.Add(Int32.Parse(entity));
                    }
                }
                // Parameters are valid
                IsValid = true;                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("XbimExtract SourceModelName TargetModelName 325 [1756 2678]");
                Console.WriteLine("\tUse XXX-YYY to extract a range");
                Console.WriteLine("\tModelName extensions supported are .xBIM, .ifc, .ifcxml, ifczip");
                IsValid = false;
            }
        }

        private string GetModelFileName(string arg, string defaultExtension)
        {
            string extName = Path.GetExtension(arg);
            string fileName = Path.GetFileNameWithoutExtension(arg);
            string dirName = Path.GetDirectoryName(arg);
            if (string.IsNullOrWhiteSpace(dirName))
                dirName = Directory.GetCurrentDirectory();
            if (string.IsNullOrWhiteSpace(extName))
                extName = defaultExtension;
            switch (extName.ToLower())
            {
                case ".xbim":
                case ".ifc":
                case ".ifcxml":
                case ".ifczip":
                    return Path.ChangeExtension(Path.Combine(dirName, fileName), extName);
                default:
                    throw new Exception("Invalid file extension (" + extName + ")");
            }
           
        }


       
    }

}
