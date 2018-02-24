//using System;
//using System.Collections.Generic;
using System.Linq;
using NuGet.Jobs;


namespace NuGet.Services.SymbolsImporter
{
    class Program
    {
        private const string LoggingCategory = "SymbolsImporter";

        static int Main(string[] args)
        {
            //SymbolAppWrapper.PublishSymbols(@"F:\NuGet\SymbolServer\cmvsts\SymServ\src\Math\Math\bin\Debug\netcoreapp2.0",
            //    @"F:\NuGet\SymbolServer\cmvsts\SymServ\src\Math\Math\SymbolFileList.txt",
            //    10,
            //    "cmanu1.1.0");

            //if (!args.Contains(JobArgumentNames.Once))
            //{
            //    args = args.Concat(new[] { "-" + JobArgumentNames.Once }).ToArray();
            //}
            var job = new Job();
            JobRunner.Run(job, args).Wait();

            return 0;
        }
    }
}
