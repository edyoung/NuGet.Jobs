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
    public interface IEntityService<T> where T : IEntity
    {
        //IValidatingEntity<T> FindById(int id);

        /// <summary>
        /// This probably will need to deleted
        /// </summary>
        /// <param name="id"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        ValidatingEntity<T> FindPackageByIdAndVersionStrict(string id, string version);

        ValidatingEntity<T> FindByKey(int key);

        Task UpdateStatusAsync(T entity, PackageStatus newStatus, bool commitChanges = true);

        //Task UpdateStreamMetadataAsync(IValidatingEntity package, JObject metadata, bool commitChanges = true);

            /// <summary>
            /// /
            /// </summary>
            /// <param name="entity"></param>
            /// <param name="metadata">other data to </param>
            /// <param name="commitChanges"></param>
            /// <returns></returns>
        Task UpdateMetadataAsync(T entity, object metadata, bool commitChanges = true);
    }
}
