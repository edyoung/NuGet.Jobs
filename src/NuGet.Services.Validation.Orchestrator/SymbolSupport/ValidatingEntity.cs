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
    public class ValidatingEntity<T> : IValidatingEntity<T> where T : IEntity
    {
        public ValidatingEntity(T entity, PackageStatus status)
        {
            Status = status;
            EntityRecord = entity;
            Key = entity.Key;
        }

        public int Key { get; set; }

        public T EntityRecord { get; set; }

        public PackageStatus Status { get; set; }
    }
}
