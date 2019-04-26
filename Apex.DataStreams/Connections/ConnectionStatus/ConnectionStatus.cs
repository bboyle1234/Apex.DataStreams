using Apex.TimeStamps;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams.Connections {

    /// <summary>
    /// Contains status data for a single data stream connection, used by both publisher and client.
    /// </summary>
    public class ConnectionStatus {

        public TimeStamp ConnectedAt;
        public EndPoint RemoteEndPoint;

        /// <summary>
        /// Number of bytes sent in the last minute, including admin topic bytes.
        /// </summary>
        public long BytesSent;

        /// <summary>
        /// Number of bytes received in the last minute, including admin topic bytes.
        /// </summary>
        public long BytesReceived;

        /// <summary>
        /// Number of messages sent in the last minute, NOT including admin messages
        /// </summary>
        public long MessagesSent;

        /// <summary>
        /// Number of messages sent in the last minute, NOT including admin messages
        /// </summary>
        public long MessagesReceived;


        /// <summary>
        /// The error that message that caused this particular connection to disconnect.
        /// </summary>
        public string DisconnectionErrorMessage;

        /// <summary>
        /// The number of bytes waiting to be sent.
        /// </summary>
        public long SendQueueLength;
    }
}
