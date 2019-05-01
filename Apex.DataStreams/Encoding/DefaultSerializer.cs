using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Apex.DataStreams.Encoding {

    public sealed class DefaultSerializer : ISerializer {

        JsonSerializer JsonSerializer = new JsonSerializer();

        public void Serialize(JsonWriter writer, object message) 
            => JsonSerializer.Serialize(writer, message);

        public object Deserialize(JsonReader reader, Type messageType)
            => JsonSerializer.Deserialize(reader, messageType);
    }
}
