using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Services.ServiceBus;
using Newtonsoft.Json;

namespace NuGet.Services.SymbolsImporter
{
    public class SymbolPackageBrokerMessageSerializer : IBrokeredMessageSerializer<SymbolPackageMessage>
    {
        public SymbolPackageBrokerMessageSerializer()
        {

        }
        public SymbolPackageMessage Deserialize(IBrokeredMessage message)
        {
            var body = message.GetBody();
            var deserializedbody = JsonConvert.DeserializeObject<SymbolPackageMessage>(body);
            return deserializedbody;
        }

        public IBrokeredMessage Serialize(SymbolPackageMessage message)
        {
            string json = JsonConvert.SerializeObject(message);
            return new BrokeredMessageWrapper(json);
        }
    }
}
