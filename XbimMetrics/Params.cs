using System;
using System.IO;

namespace XbimMetrics
{
    public class Params
    {
        public string SourceModelName;
       
        public bool IsValid { get; set; }
        public bool SourceIsXbimFile { get; set; }


        public static Params ParseParams(string[] args)
        {
            Params result = new Params(args);

            return result;
        }

        private Params(string[] args)
        {

            try
            {
                if (args.Length < 1) throw new Exception("Invalid number of Parameters, 1 required");
                SourceModelName = GetModelFileName(args[0], ".ifc");
                if (!File.Exists(SourceModelName)) throw new Exception(SourceModelName + " does not exist"); 
                SourceIsXbimFile = Path.GetExtension(SourceModelName).ToLower() == ".xbim";

                // Parameters are valid
                IsValid = true;
               
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine("XbimExtract ModelName");
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
                    return Path.ChangeExtension(Path.Combine(dirName, fileName), extName);
                default:
                    throw new Exception("Invalid file extension (" + extName + ")");
            }
           
        }


       
    }

}
