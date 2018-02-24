using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.SymbolsImporter
{
    public class JobConfiguration
    {
        public int ExpirationInDays { get; set; }

        public string PAT { get; set; }

        public string VSTSUri { get; set; }

        public int DownloadTimeoutInSeconds { get; set; }
    }
}
