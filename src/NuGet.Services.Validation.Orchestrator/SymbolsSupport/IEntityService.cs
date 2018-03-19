using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public interface IEntityService
    {
        IValidatingEntity FindById(JObject id);


        Task UpdateStatusAsync(IValidatingEntity validatingEntity, PackageStatus newStatus, bool commitChanges = true);


        Task UpdateStreamMetadataAsync(IValidatingEntity package, JObject metadata, bool commitChanges = true);
    }
}
