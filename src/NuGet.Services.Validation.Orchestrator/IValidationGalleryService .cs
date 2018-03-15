using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public interface IValidationGalleryService
    {
        IValidatingEntity FindEntityByIdAndVersionStrict(string id, string version);

        //Task UpdateIsLatestAsync(PackageRegistration packageRegistration, bool commitChanges = true);
        Task UpdateEntityStatusAsync(IValidatingEntity entity, PackageStatus newPackageStatus);
        //Task UpdatePackageStreamMetadataAsync(Package package, PackageStreamMetadata metadata, bool commitChanges = true);


        bool IsReadyForValidation(IValidatingEntity entity);
    }

    public class ValidationCorePackageService : CorePackageService, IValidationGalleryService
    {
       // ICorePackageService _packageService;

        public ValidationCorePackageService(IEntityRepository<Package> repository) : base(repository)
        {
        }

        public IValidatingEntity FindEntityByIdAndVersionStrict(string id, string version)
        {
            //var ct = typeof(T);
            //Type ct2 = typeof(Package);
            //if(ct == typeof(Package))
            //{
            //    var p = _packageService.FindPackageByIdAndVersionStrict(id, version);
            //    return new ValidatingEntity(p.PackageRegistration.Id, p.Version, p.NormalizedVersion, p.Key, p.PackageStatusKey);
            //}

            var p = FindPackageByIdAndVersionStrict(id, version);
            if (p != null) { return new ValidatingEntity(p.PackageRegistration.Id, p.Version, p.NormalizedVersion, p.Key, p.PackageStatusKey); }
            return null;
        }

        public Task UpdateEntityStatusAsync(IValidatingEntity entity, PackageStatus newPackageStatus)
        {
            return UpdatePackageStatusAsync(FindPackageByIdAndVersionStrict(entity.Id, entity.Version), newPackageStatus, true);
        }

        public bool IsReadyForValidation(IValidatingEntity entity)
        {
            return FindEntityByIdAndVersionStrict(entity.Id, entity.Version) != null;
        }
    }

    //public class GalleryServiceFactory
    //{
    //    ICorePackageService _packageService;

    //    public GalleryServiceFactory(ICorePackageService pacakgeService)
    //    {
    //        _packageService = pacakgeService;
    //    }
    //    public ICorePackageService 
    //}


}
