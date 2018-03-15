using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

//this should not be here 
using NuGetGallery;


namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// The entity to be validated
    /// </summary>
    public interface IValidatingEntity
    {
        string Id { get; }

        string Version { get; }

        string NormalizedVersion { get;}

        int Key { get; }

        PackageStatus PackageStatusKey { get; }

    }

    public class ValidatingEntity : IValidatingEntity
    {
        public ValidatingEntity(string id, string version, string normalizedVersion, int key, PackageStatus packageStatus)
        {
            Id = id;
            Version = version;
            NormalizedVersion = normalizedVersion;
            Key = key;
            PackageStatusKey = packageStatus;
        }


        public string Id { get; }
        public string Version { get ; }
        public string NormalizedVersion { get; }

        public int Key { get; }

        public PackageStatus PackageStatusKey { get; }
    }

}
