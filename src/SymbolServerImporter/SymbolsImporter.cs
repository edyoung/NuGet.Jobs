using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
//using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
//using NuGet.Packaging.Core;
//using NuGet.Services.Cursor;
//using NuGet.Versioning;
//using NuGetGallery;

namespace NuGet.Services.SymbolsImporter
{
    public class SymbolsImporter : ISymbolsImporter
    {
        private readonly IOptionsSnapshot<JobConfiguration> _configuration;
        private readonly ILogger<SymbolsImporter> _logger;

        public SymbolsImporter(
            IOptionsSnapshot<JobConfiguration> configuration,
            ILogger<SymbolsImporter> logger)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task<int> BeginImport(SymbolPackage package)
        {
            var processingTask = Task.Run(async () => {
                ProcessStartInfo pInfo = new ProcessStartInfo();
                pInfo.CreateNoWindow = true;
                pInfo.UseShellExecute = false;
                pInfo.WorkingDirectory = GetWorkingDirectory();
                pInfo.FileName = $"{GetWorkingDirectory()}\\Symbol.exe";
                var arguments = await package.PrepareSymbolPackageForIngestAsync(_configuration.Value);
                pInfo.Arguments = arguments.ToString();
                pInfo.RedirectStandardOutput = true;
                pInfo.RedirectStandardError = true;

                Process process = new Process();
                process.StartInfo = pInfo;

                try
                {
                    process.Start();
                }
                catch(Exception e)
                {
                    string m = e.Message;
                }

                string stdOut = process.StandardOutput.ReadToEnd();
                string stdError = process.StandardError.ReadToEnd();

                if (!string.IsNullOrEmpty(stdError)) { _logger.LogError(stdError); };
                if (!string.IsNullOrEmpty(stdOut)) { _logger.LogInformation(stdOut); };

                if (!process.HasExited) { process.WaitForExit(); }

                return process.ExitCode;
            });
            return processingTask;

            //--service https://cmanu.artifacts.visualstudio.com  --name cmanulocal1 --directory F:\NuGet\SymbolServer\cmvsts\SymServ\src\Math\Math\bin\Debug\netcoreapp2.0 --expirationInDays 365 --patAuthEnvVar SYMBOL_PAT_AUTH_TOKEN --fileListFileName F:\NuGet\SymbolServer\cmvsts\SymServ\src\Math\Math\SymbolFileList.txt --tracelevel verbose
        }

        public Task<IEnumerable<KeyValuePair<SymbolPackage, int>>> BeginImport(List<SymbolPackage> package)
        {
            var importers =  package.Select(p => new KeyValuePair<SymbolPackage, Task<int>>(p, BeginImport(p)));

            return Task.WhenAll(importers.Select(kv => kv.Value)).ContinueWith(t => importers.Select(kv => new KeyValuePair<SymbolPackage, int>(kv.Key, kv.Value.Result)));
        }

        string GetWorkingDirectory()
        {
            return Path.Combine(Environment.CurrentDirectory, @"symbol\lib\net45");
        }

    }
}
