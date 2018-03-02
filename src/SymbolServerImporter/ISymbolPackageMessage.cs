using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.SymbolsImporter
{
    /// <summary>
    /// The message that will be serilaized on the service bus.
    /// </summary>
    public interface ISymbolPackageMessage
    {
        /// <summary>
        /// The times that will be appended by different steps in the pipeline.
        /// </summary>
        Dictionary<string, DateTime> UTCAppendDateTime { get; }

        /// <summary>
        /// Symbol package uri.
        /// </summary>
        Uri SymbolPackageUri { get; }

        /// <summary>
        /// Package version.
        /// </summary>
        string PackageVersion { get; }

        /// <summary>
        /// Package Id.
        /// </summary>
        string PackageId { get; }
    }
}
