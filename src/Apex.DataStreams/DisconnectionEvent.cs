using Apex.TimeStamps;
using System;
using System.Net;

namespace Apex.DataStreams {

    /// <summary>
    /// Represents a disconnection event when an individual DataStreamClient was disconnected from a publisher.
    /// Used in status objects to report the health of the system.
    /// </summary>
    public class DisconnectionEvent {

        /// <summary>
        /// The time of the disconnection.
        /// </summary>
        public TimeStamp TimeStamp;

        /// <summary>
        /// The endpoint of the publisher that the client had connected to.
        /// Format [host|ip]:port
        /// </summary>
        public string RemoteEndPoint;

        /// <summary>
        /// The exception that caused the disconnection.
        /// </summary>
        public Exception Exception;
    }
}
