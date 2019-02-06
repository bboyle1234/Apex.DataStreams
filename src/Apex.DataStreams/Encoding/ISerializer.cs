using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

namespace Apex.DataStreams.Encoding {

    public interface ISerializer {

        void Serialize(JsonWriter writer, object message);
        object Deserialize(JsonReader reader, Type messageType);
    }
}
