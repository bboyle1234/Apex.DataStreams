using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CT = System.Threading.CancellationToken;
using CTS = System.Threading.CancellationTokenSource;
using static System.String;
using System.Net;
using Apex.DataStreams.Definitions;

namespace Apex.DataStreams {

    /// <summary>
    /// Contains everything a publisher needs to begin publishing a DataStream.
    /// </summary>
    public class DataStreamPublisherConfiguration {

        /// <summary>
        /// The definition of the data stream. Helps the publisher encode and decode messages correctly.
        /// </summary>
        public DataStreamDefinition DataStreamDefinition { get; set; }

        /// <summary>
        /// The port that the data stream publisher should listen on for accepting incoming client connection requests.
        /// </summary>
        public int ListenPort { get; set; }
    }
}
