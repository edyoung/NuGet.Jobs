using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.SymbolsImporter
{
    public interface ISymbolPackage
    {
        Uri Uri
        {
            get;
        }

        string Id
        {
            get;
        }

        string Version
        {
            get;
        }

        Task<bool> TryDownloadAsync(int downloadTimeOutInSeconds);

        void CreateSymbolRepositoryFile();

        Task<SymbolArguments> PrepareSymbolPackageForIngestAsync(JobConfiguration jobConfiguration);
    }
}
