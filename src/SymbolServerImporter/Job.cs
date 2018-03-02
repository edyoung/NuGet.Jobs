using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NuGet.Jobs.Validation;
using NuGet.Services.ServiceBus;



//using NuGet.Services.Configuration;
//using NuGet.Services.KeyVault;
////using NuGet.Services.Logging;
//using NuGet.Services.ServiceBus;
//using NuGetGallery.Services;
//using NuGet.Jobs.Validation;

namespace NuGet.Services.SymbolsImporter
{
    public class Job : JsonConfigurationJob
    {
        private const string SymbolServerConfigurationArgument = "SymbolServer";
        private const string ServiceBusConfigurationArgument = "ServiceBus";

        public Job()
        {
        }

        public override void Init(IDictionary<string, string> jobArgsDictionary)
        {
           // var configurationFilename = JobConfigurationManager.GetArgument(jobArgsDictionary, ConfigurationArgument);
            base.Init(jobArgsDictionary);
        }

        public override async Task Run()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.CancelAfter(30 * 60 * 1000);

            var serviceImporter = _serviceProvider.GetRequiredService<ISymbolsImporter>();

            //listen and start the start importing 

            //SymbolPackage s = new SymbolPackage(new Uri("https://cmanutest.blob.core.windows.net/symbolpackages/Math1.0.0.zip"), "Math", "1.0.0");

            //return serviceImporter.Import(s);

            //SymbolProcessor should estimate the number of receivers
            SymbolProcessor processor = new SymbolProcessor(serviceImporter, 10, LoggerFactory);
            var t = Task.WhenAll(processor.RegisterReceivers(cts.Token));

            Task.Delay(30 * 60 * 1000).Wait();
            processor.Print();
            //phase2: if there are is no more data to process downsize the receivers
            //the SymbolProcessor can do look at the topic and decide if the number of subscribers needed based on the number of messages
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<JobConfiguration>(configurationRoot.GetSection(SymbolServerConfigurationArgument));
            services.Configure<ServiceBusConfiguration>(configurationRoot.GetSection(ServiceBusConfigurationArgument));

            services.AddTransient<ISymbolsImporter, SymbolsImporter>();
            services.AddTransient<ISymbolsImporter, SymbolsImporter>();

            //services.AddTransient<ISubscriptionClient>(serviceProvider =>
            //{
            //    var configuration = serviceProvider.GetRequiredService<IOptionsSnapshot<ServiceBusConfiguration>>().Value;
            //    return new SubscriptionClientWrapper(configuration.ConnectionString, configuration.TopicPath, configuration.SubscriptionName);
            //});
        }
    }
}
