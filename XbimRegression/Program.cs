using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XbimRegression
{
    class Program
    {
        static void Main(string[] args)
        {
            Params arguments = Params.ParseParams(args);
            if (arguments.IsValid)
            {
                BatchProcessor processor = new BatchProcessor(arguments);
                processor.Run();
            }
        }

    }
}
