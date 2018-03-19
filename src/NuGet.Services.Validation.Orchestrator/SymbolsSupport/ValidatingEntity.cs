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
    public class ValidatingEntity : IValidatingEntity
    {
        public ValidatingEntity(JObject id, JObject metadata, PackageStatus status, int key)
        {
            Id = id;
            Status = status;
            Metadata = metadata;
            Key = key;

            string pId = (string)id.SelectToken("Id");
            string pNVersion = (string)id.SelectToken("NormalizedVersion");
        }

        public JObject Id { get ; set ; }

        //public string PId { get;  }

        //public string NormalizedVersion { get;  }

        public int Key{ get; set; }

        public JObject Metadata { get; set; }

        public PackageStatus Status { get ; set; }

        public bool IsMetadataEquals(JObject metadataOther)
        {
            return JToken.DeepEquals(Metadata, metadataOther);
        }
    }
}
