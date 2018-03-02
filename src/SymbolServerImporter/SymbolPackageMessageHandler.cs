using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Services.ServiceBus;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NuGet.Services.SymbolsImporter
{
    public class SymbolPackageMessageHandler : IMessageHandler<SymbolPackageMessage>
    {
        public ConcurrentQueue<string> onHandleAsyncTimes = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> timesToIngest = new ConcurrentQueue<string>();
        private ISymbolsImporter _symbolImporter;
        private ILogger<SymbolPackageMessageHandler> _logger;

        public SymbolPackageMessageHandler(ISymbolsImporter symbolImporter, ILogger<SymbolPackageMessageHandler> logger )
        {
            _symbolImporter = symbolImporter ?? throw new ArgumentNullException(nameof(symbolImporter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> HandleAsync(SymbolPackageMessage message)
        {
            var log1 = new JObject()
            {
                {"PackageId",message.PackageId },
                {"HandleAsyncUTCTime", DateTime.UtcNow},
                {"PickupToExecuteDelayInSeconds", (DateTime.UtcNow - message.UTCAppendDateTime["SymbolValidationSender"]).Seconds},
                {"RunId", 3 }
            };
            _logger.LogInformation(log1.ToString());

            onHandleAsyncTimes.Enqueue($"{message.PackageId}_{DateTime.UtcNow - message.UTCAppendDateTime["SymbolValidationSender"]}_{DateTime.UtcNow}");
            Stopwatch sw = new Stopwatch();
            sw.Start();
            int exitCode = -1;
            try
            {
                exitCode = await _symbolImporter.Import(new SymbolPackage(message));
            }
            catch(Exception e)
            {
                string m = e.Message;
            }
            long time = sw.ElapsedMilliseconds;
            sw.Stop();

            var log2 = new JObject()
            {
                {"PackageId",message.PackageId },
                {"IngestExitCode", exitCode},
                {"IngestTimeMilliseconds", time},
                {"RunId", 3 }
            };
            _logger.LogInformation(log2.ToString());

            timesToIngest.Enqueue($"{message.PackageId}_{time}_{exitCode}");
            return await Task.FromResult (exitCode == 0);
        }
    }
}
