using Apex.TimeStamps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams.Connections {

    public class DataStreamConnectionStatus {

        public TimeStamp ConnectedAt;
        public EndPoint RemoteEndPoint;
        public long BytesSent;
        public long BytesReceived;
        public long MessagesSent;
        public long MessagesReceived;
        public string DisconnectionErrorMessage;
        public long SendQueueLength;
    }
}
