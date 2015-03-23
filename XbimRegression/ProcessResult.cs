using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace XbimRegression
{
    /// <summary>
    /// Class summarising the results of a model conversion
    /// </summary>
    public class ProcessResult
    {
        public ProcessResult()
        {
            Errors = -1;
            LastTestFailed = null;
        }
        public bool Failed { get; set; }
        public bool? LastTestFailed { get; set; }
        public String FileName { get; set; }
        public int Errors { get; set; }
        public int Warnings { get; set; }
        public long ParseDuration { get; set; }
        public long GeometryDuration { get; set; }
        public long SceneDuration { get; set; }
        public long XbimLength { get; set; }
        public long SceneLength { get; set; }
        public long IfcLength { get; set; }
        public long Entities { get; set; }
        public long GeometryEntries { get; set; }
        public String IfcSchema { get; set; }
        public String IfcName { get; set; }
        public String IfcDescription { get; set; }
        public long IfcProductEntries { get; set; }
        public long IfcSolidGeometries { get; set; }
        public long IfcMappedGeometries { get; set; }
        public String Application { get; set; }
        public long BooleanGeometries { get; set; }
        public const String CsvHeader = @"Test, Last Test, IFC File, Errors, Warnings, Parse Duration (ms), Geometry Conversion (ms), Scene Generation (ms), Total Duration (ms), IFC Size, Xbim Size, Scene Size, IFC Entities, Geometry Nodes, " +
           
            "FILE_SCHEMA, FILE_NAME, FILE_DESCRIPTION, "+
             "Products, Solid Models, Maps, Booleans, Application";

        /// <summary>
        /// String version of the Failed property
        /// </summary>
        public string FailedString
        {
            get
            {
                return BoolToString((bool?)Failed);
            }

            set
            {
                try
                {
                    Failed = (bool)StringToBool(value);
                }
                catch (Exception) //if cast fails for some reason set to false
                {
                    Failed = false;
                }
            }
        }
        /// <summary>
        /// String version of the LastTestFailed property
        /// </summary>
        public string LastTestFailedString
        {
            get
            {
                return BoolToString(LastTestFailed);
            }

            set
            {
                LastTestFailed = StringToBool(value);
            }
        }

        /// <summary>
        /// Change a bool or bool? to a string
        /// </summary>
        /// <param name="boolValue"></param>
        /// <returns>bool?</returns>
        private string BoolToString(bool? boolValue)
        {
            if (boolValue != null)
                return(bool)boolValue ? "Failed" : "Passed";
            else
                return "No Test";
        }

        /// <summary>
        /// convert string to a bool/bool? 
        /// </summary>
        /// <param name="strValue">string</param>
        /// <returns></returns>
        private bool? StringToBool(string strValue)
        {
            switch (strValue.ToUpper())
            {
                case "FAILED":
                    return true;
                case "PASSED":
                    return false;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Convert ProcessResult to a csv string
        /// </summary>
        /// <returns>CSV string</returns>
        public String ToCsv()
        {
            
            //return String.Format("{20}, {21}, \"{0}\",{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},\"{12}\",\"{13}\",\"{14}\",{15},{16},{17},{18},\"{19}\"",
            return String.Format("{20},{21},{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19}",
                SanitiseString(FileName),// 0
                Errors,             // 1
                Warnings,           // 2
                ParseDuration,      // 3
                GeometryDuration,   // 4
                SceneDuration,      // 5
                TotalTime,          // 6
                IfcLength,          // 7
                XbimLength,         // 8
                SceneLength,        // 9
                Entities,           // 10
                GeometryEntries,    // 11
                SanitiseString(IfcSchema),          // 12
                SanitiseString(IfcName),            // 13
                SanitiseString(IfcDescription),     // 14
                IfcProductEntries,                  // 15
                IfcSolidGeometries,                 // 16
                IfcMappedGeometries,                // 17
                BooleanGeometries,                  // 18
                SanitiseString(Application),        // 19
                FailedString,                       // 20
                LastTestFailedString                // 21
                );
        }
        /// <summary>
        /// Sanitise string for comma delimited output/input
        /// </summary>
        /// <param name="str">String to process</param>
        /// <returns>Processed string</returns>
        public string SanitiseString(string str)
        {
            if (str != null && str.Length > 0)
            {
                return str.Replace(",", "-").Replace("\"", "'").Replace("\r", "").Replace("\n", " ");
            }
            return "Null";
        }
        /// <summary>
        /// Calculate total time to process
        /// </summary>
        public long TotalTime 
        {
            get
            {
                return ParseDuration + GeometryDuration + SceneDuration;
            }
        
        }

       
    }
}
