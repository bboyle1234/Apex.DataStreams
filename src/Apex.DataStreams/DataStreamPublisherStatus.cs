using Apex.DataStreams.Connections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams {

    public class DataStreamPublisherStatus {

        public List<DataStreamConnectionStatus> Connections;
        public List<DisconnectionEvent> RecentDisconnections;
    }
}
