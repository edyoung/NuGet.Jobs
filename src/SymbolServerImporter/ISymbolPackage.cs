using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Services.SymbolsImporter
{
    /// <summary>
    /// It handles the connection with the Azure snupkg blob.
    /// </summary>
    public interface ISymbolPackage
    {
        /// <summary>
        /// The uri of the symbol package to ingest.
        /// </summary>
        Uri Uri
        {
            get;
        }

        /// <summary>
        /// The id of the symbol package to ingest.
        /// </summary>
        string Id
        {
            get;
        }

        /// <summary>
        /// Package version
        /// </summary>
        string Version
        {
            get;
        }

        /// <summary>
        /// In general this will be {Id}{Version}
        /// </summary>
        string Name
        {
            get;
        }

        /// <summary>
        /// Local path where the snupkg is downloaded.
        /// </summary>
        string DownloadFolderPath
        {
            get;
        }

        /// <summary>
        /// The path of the file that contains the pdbs to ingest.
        /// </summary>
        string SymbolRepositoryFilePath
        {
            get;
        }

        /// <summary>
        /// It download the snupkg
        /// </summary>
        /// <param name="downloadTimeOutInSeconds"></param>
        /// <returns></returns>
        Task<bool> TryDownloadAsync(int downloadTimeOutInSeconds);

        /// <summary>
        /// It creates the symbol repository file.
        /// </summary>
        void CreateSymbolRepositoryFile();
    }
}
