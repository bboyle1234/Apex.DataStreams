using Apex.DataStreams.Connections;
using System;
using System.Collections.Generic;
using System.Text;

namespace Apex.DataStreams {

    public class PublisherStatus {

        public List<ConnectionStatus> Connections;
        public List<DisconnectionEvent> RecentDisconnections;
    }
}
