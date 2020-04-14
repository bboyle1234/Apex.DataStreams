using System;
using System.Collections.Generic;
using System.Text;

namespace Apex.DataStreams.Connections {

    /// <summary>
    /// Contains the status data for a connection worker, which includes the status of the current connection,
    /// plus an account of recent disconnections. Used by both publisher and client.
    /// </summary>
    public class ConnectionWorkerStatus {

        public string EndPoint;
        public ConnectionStatus CurrentConnection;
        public List<DisconnectionEvent> RecentDisconnections;
    }
}
