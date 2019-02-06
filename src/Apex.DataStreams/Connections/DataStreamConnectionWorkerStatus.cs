using System;
using System.Collections.Generic;

namespace Apex.DataStreams.Connections {

    public class DataStreamConnectionWorkerStatus {

        public string EndPoint;
        public DataStreamConnectionStatus CurrentConnection;
        public List<DisconnectionEvent> RecentDisconnections;
    }
}
