using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;

namespace Apex.DataStreams {

    public class ClientContext {

        public IServiceProvider Services;

        /// <summary>
        /// The definition of the data stream. Helps the client encode and decode messages correctly.
        /// </summary>
        public Schema Schema;

        /// <summary>
        /// Format [hostname|ip]:port
        /// </summary>
        public string PublisherEndPoint;

        /// <summary>
        /// The queue to place the messgges that the client receives.
        /// </summary>
        public ChannelWriter<object> ReceiveQueue;
    }
}
