using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Threading;
using System.Threading.Tasks;

namespace NuGet.Services.SymbolsImporter
{
    public class SymbolPackage : ISymbolPackage
    {
        private const string AppDataFolder = "SymbolsImporter";

        public Uri Uri
        {
            get;
        }

        public string Id
        {
            get;
        }

        public string Version
        {
            get;
        }

        //public SymbolPackage(Uri symbolPackageURI, string id, string version)
        //{
        //    Uri = symbolPackageURI;
        //    Id = id;
        //    Version = version;
        //}

        public SymbolPackage(SymbolPackageMessage symbolPackageMessage)
        {
            Uri = symbolPackageMessage.SymbolPackageUri;
            Id = symbolPackageMessage.PackageId;
            Version = symbolPackageMessage.PackageVersion;
        }

        public string DownloadFolderPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolder, Name);
            }
        }

        public string SymbolRepositoryFilePath
        {
            get
            {
                return Path.Combine(DownloadFolderPath, $"SymbolRepository.txt");
            }
        }
    
        public string Name
        {
            get
            {
                return $"{Id}{Version}";
            }
        }

        public async Task<bool> TryDownloadAsync(int downloadTimeOutInSeconds)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(downloadTimeOutInSeconds);
                    var httpResponseMessage = await client.GetAsync(Uri);

                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        using (Stream stream = await httpResponseMessage.Content.ReadAsStreamAsync())
                        {
                            ZipArchive zipArchive = new ZipArchive(stream);
                            DeleteDirectory(DownloadFolderPath);
                            zipArchive.ExtractToDirectory(DownloadFolderPath);
                        }
                        //log success 
                        return true;
                    }
                    else
                    {
                        // Log failure
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                //Log exception 
                return false;
            }
        }

        void DeleteDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                foreach (var subdir in Directory.GetDirectories(path))
                {
                    DeleteDirectory(subdir);
                }
                Directory.Delete(path, true);
            }
        }

        public void CreateSymbolRepositoryFile()
        {
            var localSymbolPath = DownloadFolderPath;
            //read all the pdb files and add them to a temporary repository file
            var symbolFiles = new DirectoryInfo(localSymbolPath).GetFiles("*.pdb", SearchOption.AllDirectories).Select(fi => fi.FullName).ToArray() ;
            File.WriteAllLines(SymbolRepositoryFilePath, symbolFiles);
        }

        //public async Task<SymbolArguments> PrepareSymbolPackageForIngestAsync(JobConfiguration serverConfiguration)
        //{
        //    if( await ((ISymbolPackage)this).TryDownloadAsync(serverConfiguration.DownloadTimeoutInSeconds) )
        //    {
        //        ((ISymbolPackage)this).CreateSymbolRepositoryFile();
        //        return new SymbolArguments(serverConfiguration.VSTSUri, GetName, GetDownloadFolderPath, serverConfiguration.ExpirationInDays, serverConfiguration.PAT, GetSymbolRepositoryFilePath);
        //    }
        //    return null;
        //}
    }
}
