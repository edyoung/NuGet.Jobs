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

        public Task<int> Import(SymbolPackage package)
        {
            var processingTask = Task.Run(async () => {
                ProcessStartInfo pInfo = new ProcessStartInfo();
                pInfo.CreateNoWindow = true;
                pInfo.UseShellExecute = false;
                pInfo.WorkingDirectory = GetWorkingDirectory();
                pInfo.FileName = $"{GetWorkingDirectory()}\\Symbol.exe";
                var arguments = await PrepareSymbolPackageForIngestAsync(_configuration.Value, package);
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

        //public Task<IEnumerable<KeyValuePair<SymbolPackage, int>>> BeginImport(List<SymbolPackage> package)
        //{
        //    var importers =  package.Select(p => new KeyValuePair<SymbolPackage, Task<int>>(p, BeginImport(p)));

        //    return Task.WhenAll(importers.Select(kv => kv.Value)).ContinueWith(t => importers.Select(kv => new KeyValuePair<SymbolPackage, int>(kv.Key, kv.Value.Result)));
        //}

        string GetWorkingDirectory()
        {
            return Path.Combine(Environment.CurrentDirectory, @"symbol\lib\net45");
        }

        async Task<VSTSSymbolArguments> PrepareSymbolPackageForIngestAsync(JobConfiguration serverConfiguration, ISymbolPackage package)
        {
            if (await package.TryDownloadAsync(serverConfiguration.DownloadTimeoutInSeconds))
            {
                package.CreateSymbolRepositoryFile();
                return new VSTSSymbolArguments(serverConfiguration.VSTSUri, package.Name, package.DownloadFolderPath, serverConfiguration.ExpirationInDays, serverConfiguration.PAT, package.SymbolRepositoryFilePath);
            }
            return null;
        }

        private class VSTSSymbolArguments
        {
            public string VSTSUri
            { get; }

            public string Name
            { get; }

            public string Directory
            { get; }

            public int ExpirationInDays
            { get; }

            public string PAT
            { get; }

            public string FileListFileName
            { get; }

            public string TraceLevel
            { get; }

            public VSTSSymbolArguments(string vstsUri, string name, string directory, int expirationInDays, string pat, string fileListFileName)
            {
                VSTSUri = vstsUri;
                Name = name;
                Directory = directory;
                ExpirationInDays = expirationInDays;
                PAT = pat;
                FileListFileName = fileListFileName;
                TraceLevel = "verbose";
            }

            public override string ToString()
            {
                return $"publish  --service {VSTSUri}  --name {Name}3 --directory {Directory} --expirationInDays {ExpirationInDays} --patAuth {PAT} --fileListFileName {FileListFileName} --tracelevel {TraceLevel}";
            }
        }
    }
}
