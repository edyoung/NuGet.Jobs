using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        private const string ConfigurationArgument = "SymbolServer";

        public Job()
        {
        }

        public override void Init(IDictionary<string, string> jobArgsDictionary)
        {
           // var configurationFilename = JobConfigurationManager.GetArgument(jobArgsDictionary, ConfigurationArgument);
            base.Init(jobArgsDictionary);
        }

        public override Task Run()
        {

            var serviceImporter = _serviceProvider.GetRequiredService<ISymbolsImporter>();

            //listen and start the start importing 
           
            SymbolPackage s = new SymbolPackage(new Uri("https://cmanutest.blob.core.windows.net/symbolpackages/Math1.0.0.zip"), "Math", "1.0.0");

            return serviceImporter.BeginImport(s);
        }

        protected override void ConfigureAutofacServices(ContainerBuilder containerBuilder)
        {
        }

        protected override void ConfigureJobServices(IServiceCollection services, IConfigurationRoot configurationRoot)
        {
            services.Configure<JobConfiguration>(configurationRoot.GetSection(ConfigurationArgument));
            services.AddTransient<ISymbolsImporter, SymbolsImporter>();
        }
    }
}
