using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace XbimRegression
{
    /// <summary>
    /// Class to hold a set of ProcessResults
    /// </summary>
    public class ProcessResultSet
    {
        /// <summary>
        /// ProcessResult list creating the set
        /// </summary>
        private List<ProcessResult> set = new List<ProcessResult>();
        public List<ProcessResult> Set { 
            get {
                return set;
            }
        }
        
        /// <summary>
        /// Add ProcessResult to the ProcessResultSet
        /// </summary>
        /// <param name="pR">ProcessResult</param>
        public void Add(ProcessResult pR)
        {
            set.Add(pR);
        }

        /// <summary>
        /// Load from a comma delimited file
        /// </summary>
        /// <param name="fileName"></param>
        public void LoadFromFile (string fileName)
        {
            using (StreamReader reader = new StreamReader(fileName))
            {
                string line = reader.ReadLine(); //skip header line as all strings
                if (line == null)
                {
                    Console.WriteLine("{0}, Empty File.", fileName);
                    return; //empty file
                }
                string[] cellChk = line.Split(new char[] { ',' }, StringSplitOptions.None);
                if (cellChk.Count() != 22)
                {
                    Console.WriteLine("{0}, Incorrect number of columns.", fileName);
                    return; //empty file
                }
                int count = 1;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] cells = line.Split(new char[] { ',' },StringSplitOptions.None);
                    try
                    {
                        ProcessResult pR = new ProcessResult()
                                   {
                                       FileName = cells[2] == null ? "" : cells[2],
                                       Errors = cells[3] == null ? 0 : int.Parse(cells[3]),
                                       Warnings = cells[4] == null ? 0 : int.Parse(cells[4]),
                                       ParseDuration = cells[5] == null ? 0 : long.Parse(cells[5]),
                                       GeometryDuration = cells[6] == null ? 0 : long.Parse(cells[6]),
                                       SceneDuration = cells[7] == null ? 0 : long.Parse(cells[7]),
                                       //TotalTime - read only, calculated field [8]
                                       IfcLength = cells[9] == null ? 0 : long.Parse(cells[9]),
                                       XbimLength = cells[10] == null ? 0 : long.Parse(cells[10]),
                                       SceneLength = cells[11] == null ? 0 : long.Parse(cells[11]),
                                       Entities = cells[12] == null ? 0 : long.Parse(cells[12]),
                                       GeometryEntries = cells[13] == null ? 0 : long.Parse(cells[13]),
                                       IfcSchema = cells[14] == null ? "" : cells[14],
                                       IfcName = cells[15] == null ? "" : cells[15],
                                       IfcDescription = cells[16] == null ? "" : cells[16],
                                       IfcProductEntries = cells[17] == null ? 0 : long.Parse(cells[17]),
                                       IfcSolidGeometries = cells[18] == null ? 0 : long.Parse(cells[18]),
                                       IfcMappedGeometries = cells[19] == null ? 0 : long.Parse(cells[19]),
                                       BooleanGeometries = cells[20] == null ? 0 : long.Parse(cells[20]),
                                       Application = cells[21] == null ? "" : cells[21]
                                   };
                        pR.FailedString = cells[0];
                        pR.LastTestFailedString = cells[1];
                        set.Add(pR);
                        count++;
                    }
                    catch (Exception ex)
                    {  
                        Console.WriteLine("Cannot read line {0}, error: {1}.", count, ex.Message);
                    }

                }
                Console.WriteLine("Finished");
            }
        }

        /// <summary>
        /// Write to a comma delimited file
        /// </summary>
        /// <param name="fileName"></param>
        public void WriteToFile(string fileName)
        {
            using (StreamWriter writer = new StreamWriter(fileName))
            {
                writer.WriteLine(ProcessResult.CsvHeader);
                foreach (var item in set)
                {
                    writer.WriteLine(item.ToCsv());
                    writer.Flush();
                }
                
            }
        }

        /// <summary>
        /// Compare pass and fails of two ProcessResultSet
        /// </summary>
        /// <param name="pRs">ProcessResultSet</param>
        public bool? Compare(ProcessResult pR)
        {
            ProcessResult thispR = this.set.Where(pr => (pr.FileName == pR.FileName)).FirstOrDefault();
            if (thispR != null) 
            {
               return thispR.Failed;
            }
            return null;
        }

        /// <summary>
        /// Split comma delimited strings with embedded strings holding commas, 
        /// Regex from http://stackoverflow.com/questions/3776458/split-a-comma-separated-string-with-both-quoted-and-unquoted-strings
        /// </summary>
        /// <param name="input">Comma delimited string</param>
        /// <returns>String array of cell values</returns>
        public string[] SplitCSV(string input)
        {
            Regex csvSplit = new Regex("(?:^|,)(\"(?:[^\"]+|\"\")*\"|[^,]*)");
            string[] result = new string[22];
            int index = 0;
            foreach (Match item in csvSplit.Matches(input))
            {
                result[index] = item.Value.TrimStart(',').Replace("\"", "").Trim();
                index++;
            }

            return result;
        }
    }
}
