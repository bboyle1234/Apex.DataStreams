using Apex.TimeStamps;
using System;
using System.Net;

namespace Apex.DataStreams.Connections {

    public class DisconnectionEvent {

        public TimeStamp TimeStamp;
        public string RemoteEndPoint;
        public Exception Exception;
    }
}
