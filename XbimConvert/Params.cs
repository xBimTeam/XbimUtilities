using System;
using System.IO;
using System.Linq;

namespace XbimConvert
{
    public class Params
    {
        public  int GeomVersion = 2;
        public static Params ParseParams(string[] args)
        {
            Params result = new Params(args);

            return result;
        }

        private Params(string[] args)
        {
            // init behaviour
            ProcessSubDir = false;
            GeometryGeneration = GeometryGenerationOptions.ExcludeIfcFeatures;

            if (args.Length < 1)
            {
                Console.WriteLine("Invalid number of Parameters, filename required");
                Console.WriteLine("Syntax: XbimConvert source [-quiet|-q] [-generatescene|-gs[:options]] [-nogeometry|-ng] [-keepextension|-ke] [-filter|-f <elementid|elementtype>] [-sanitiselog] [-occ] [-geomVersion|-gv]");
                Console.Write("-geomversion options are: 1 or 2, 2 is the latest and default version supporting maps");
                Console.Write("-generatescene options are: ");
                //foreach (var i in Enum.GetValues(typeof(GenerateSceneOption)))
                //    Console.Write(" " + i.ToString());
                return;
            }
            Specdir = Path.GetDirectoryName(args[0]);
            if (Specdir == "")
                Specdir = Directory.GetCurrentDirectory();
            Specpart = Path.GetFileName(args[0]);

            //GenerateSceneOptions = 
            //            GenerateSceneOption.IncludeRegions |
            //            GenerateSceneOption.IncludeStoreys |
            //            GenerateSceneOption.IncludeSpaces;

            CompoundParameter paramType = CompoundParameter.None;
            foreach (string arg in args.Skip(1))
            {

                switch (paramType)
                {
                    case CompoundParameter.None:
                        string[] argNames = arg.ToLowerInvariant().Split(new[] { ":" }, StringSplitOptions.RemoveEmptyEntries);
                        switch (argNames[0])
                        {
                            case "-caching":
                            case "-c":
                                Caching = true;
                                break;
                            case "-quiet":
                            case "-q":
                                IsQuiet = true;
                                break;

                            case "-keepextension":
                            case "-ke":
                                KeepFileExtension = true;
                                break;
                            case "-generatescene":
                            case "-gs":
                                GenerateScene = true;

                                if (argNames.Length > 1)
                                {
                                    //foreach (var i in Enum.GetValues(typeof(GenerateSceneOption)))
                                    //{
                                    //    if (CultureInfo.CurrentCulture.CompareInfo.IndexOf((string)argNames[1], i.ToString(), CompareOptions.IgnoreCase) >= 0)
                                    //    {
                                    //        GenerateSceneOptions = GenerateSceneOptions | (GenerateSceneOption)i;
                                    //    }
                                    //}
                                }
                                break;
                            case "-nogeometry":
                            case "-ng":
                                GeometryGeneration = GeometryGenerationOptions.None;
                                break;
                            case "-allgeometry":
                            case "-ag":
                                GeometryGeneration = GeometryGenerationOptions.All;
                                break;
                            case "-filter":
                            case "-f":
                                paramType = CompoundParameter.Filter;
                                // need to read next param
                                break;
                            case "-subdir":
                            case "-s":
                                ProcessSubDir = true;
                                break;
                            case "-sanitiselog":
                                SanitiseLogs = true;
                                break;
                            case "-occ":
                                Occ = true;
                                break;
                            case "-geomversion":
                            case "-gv":
                                if(argNames.Length>1 && Convert.ToInt32(argNames[1])==1)
                                    GeomVersion = 1;
                                break;
                            default:
                                Console.WriteLine("Skipping un-expected argument '{0}'", arg);
                                break;
                        }
                        break;
                    
                }

            }
            // Parameters are valid
            IsValid = true;
        }

        // files identification
        public string Specdir { get; set; }
        public string Specpart { get; set; }
        public bool ProcessSubDir { get; set; }
        
        public bool IsQuiet { get; set; }
        public bool KeepFileExtension { get; set; }
        public bool GenerateScene { get; set; }
        //public GenerateSceneOption GenerateSceneOptions { get; set; }
        
        public bool NoGeometry
        {
            get
            {
                return (GeometryGeneration == GeometryGenerationOptions.None);
            }
        }

        public GeometryGenerationOptions GeometryGeneration { get; set; }

        public enum GeometryGenerationOptions
        {
            None,
            ExcludeIfcFeatures,
            All
        }

        public bool IsValid { get; set; }
        public FilterType FilterType { get; set; }
        
        public bool Occ { get; set; }
        /// <summary>
        /// Indicates that logs should not contain sensitive path information.
        /// </summary>
        public bool SanitiseLogs { get; set; }



        private enum CompoundParameter
        {
            None,
            Filter
        };

        public bool Caching { get; set; }
    }

    public enum FilterType
    {
        None,
        ElementId,
        ElementType
    };
}
