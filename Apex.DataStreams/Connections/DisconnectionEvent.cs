using Apex.TimeStamps;
using System;
using System.Collections.Generic;
using System.Text;

namespace Apex.DataStreams.Connections {

    public class DisconnectionEvent {

        public TimeStamp TimeStamp;
        public string RemoteEndPoint;
        public Exception Exception;
    }
}
