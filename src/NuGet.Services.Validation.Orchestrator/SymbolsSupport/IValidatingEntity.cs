using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using NuGetGallery;


namespace NuGet.Services.Validation.Orchestrator
{
    public interface IValidatingEntity
    {
        JObject Id { get; set;}

        //string PId { get;}

        //string NormalizedVersion{ get; }

        int Key { get; set; }

        JObject Metadata { get; set; }

        PackageStatus Status { get; set; }

        bool IsMetadataEquals(JObject metadataOther);

    }
}
