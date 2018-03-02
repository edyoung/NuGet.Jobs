using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.SymbolsImporter
{
    public class SymbolPackageMessage
    {
        public Dictionary<string,DateTime> UTCAppendDateTime { get; }

        public Uri SymbolPackageUri { get; }

        public string PackageVersion { get; }

        public string PackageId { get; }

        public SymbolPackageMessage( string symbolPackageUri, string packageVersion, string packageId, Dictionary<string, DateTime> utcAppendDateTime)
        {
            SymbolPackageUri = new Uri(symbolPackageUri);
            PackageVersion = packageVersion;
            PackageId = packageId;
            UTCAppendDateTime = utcAppendDateTime;
        }
    }
}
