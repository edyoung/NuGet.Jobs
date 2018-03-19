using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NuGetGallery;
using NuGetGallery.Packaging;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// This is coupled with the package
    /// </summary>
    public class PackageEntityService : IEntityService
    {
        ICorePackageService _galleryEntityService;

        public PackageEntityService(ICorePackageService galleryEntityService)
        {
            _galleryEntityService = galleryEntityService;
        }

        public IValidatingEntity FindById(JObject id)
        {
            Package p = GetPackage(id);
            JObject metadata = GetPackageMetadata(p); ;
            return new ValidatingEntity(id, metadata, p.PackageStatusKey, p.Key);
        }


        private Package GetPackage(JObject id)
        {
            string pId = (string)id.SelectToken("Id");
            string pNVersion = (string)id.SelectToken("NormalizedVersion");
            Package p = _galleryEntityService.FindPackageByIdAndVersionStrict(pId, pNVersion);
            return p;
        }

        private JObject GetPackageMetadata(JObject id)
        {
            Package p = GetPackage(id); 
            return GetPackageMetadata(p);
        }

        private JObject GetPackageMetadata(Package p)
        {
            JObject metadata = new JObject { { "Hash", p.Hash }, { "HashAlgorithm", p.HashAlgorithm }, { "Size", p.PackageFileSize } };
            return metadata;
        }

        public static string GetPacakgeId(IValidatingEntity entity)
        {
            JObject id = entity.Id;
            string pId = (string)id.SelectToken("Id");
            return pId;
        }

        public static string GetPacakgeNormalizedVersion(IValidatingEntity entity)
        {
            JObject id = entity.Id;
            string pNVersion = (string)id.SelectToken("NormalizedVersion");
            return pNVersion;
        }

        public async Task UpdateStatusAsync(IValidatingEntity validatingEntity, PackageStatus newStatus, bool commitChanges = true)
        {
            Package p = GetPackage(validatingEntity.Id); ;
            await _galleryEntityService.UpdatePackageStatusAsync(p, newStatus, commitChanges);
        }

        public async Task UpdateStreamMetadataAsync(IValidatingEntity entity, JObject metadata,  bool commitChanges = true)
        {
            Package p = GetPackage(entity.Id);
            string hash = (string)metadata.SelectToken("Hash");
            string hashAlgorithm = (string)metadata.SelectToken("HashAlgorithm");
            long size = (long)metadata.SelectToken("Size");

            PackageStreamMetadata pmetadata = new PackageStreamMetadata()
            {
                Hash = hash,
                HashAlgorithm = hashAlgorithm,
                Size = size
            };
         
            await _galleryEntityService.UpdatePackageStreamMetadataAsync(p, pmetadata, commitChanges);
        }
    }
}
