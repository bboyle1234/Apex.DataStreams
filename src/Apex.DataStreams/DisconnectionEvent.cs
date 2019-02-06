using Apex.TimeStamps;
using System;
using System.Net;

namespace Apex.DataStreams {

    public class DisconnectionEvent {

        public TimeStamp TimeStamp;
        public string RemoteEndPoint;
        public Exception Exception;
    }
}
