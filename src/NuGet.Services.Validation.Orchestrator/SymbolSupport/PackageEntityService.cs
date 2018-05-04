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
    public class PackageEntityService : IEntityService<Package>
    {
        ICorePackageService _galleryEntityService;

        public PackageEntityService(ICorePackageService galleryEntityService)
        {
            _galleryEntityService = galleryEntityService;
        }

        public ValidatingEntity<Package> FindPackageByIdAndVersionStrict(string id, string version)
        {
            var p = _galleryEntityService.FindPackageByIdAndVersionStrict(id, version);
            return new ValidatingEntity<Package>(p, p.PackageStatusKey);
        }

        public ValidatingEntity<Package> FindByKey(int key)
        {
            //ToDo need to have find by key
            var p =  _galleryEntityService.FindPackageByIdAndVersionStrict("", "");
            return new ValidatingEntity<Package>(p, p.PackageStatusKey);
        }

        public async Task UpdateStatusAsync(Package entity, PackageStatus newStatus, bool commitChanges = true)
        {
            //Package p = GetPackage(validatingEntity.Id); ;
            
            await _galleryEntityService.UpdatePackageStatusAsync(entity, newStatus, commitChanges);
        }

        public async Task UpdateMetadataAsync(Package entity, object metadata, bool commitChanges = true)
        {
            PackageStreamMetadata typedMetadata = metadata == null ? null : (PackageStreamMetadata)metadata;

            if (typedMetadata != null)
            {
                if (typedMetadata.Size != entity.PackageFileSize
                    || typedMetadata.Hash != entity.Hash
                    || typedMetadata.HashAlgorithm != entity.HashAlgorithm)

                {
                    await _galleryEntityService.UpdatePackageStreamMetadataAsync(entity, typedMetadata, commitChanges);
                }
            }
        }
    }
}
