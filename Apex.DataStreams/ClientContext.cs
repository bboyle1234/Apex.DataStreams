using Apex.DataStreams.Definitions;
using Apex.DataStreams.Encoding;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams {

    public class ClientContext {

        /// <summary>
        /// The definition of the data stream. Helps the client encode and decode messages correctly.
        /// </summary>
        public DataStreamDefinition DataStreamDefinition;

        public int MaxMessageSendTimeInSeconds;

        /// <summary>
        /// Format [hostname|ip]:port
        /// </summary>
        public string PublisherEndPoint;

        /// <summary>
        /// The queue that messages from the publisher will be sent to.
        /// </summary>
        public AsyncProducerConsumerQueue<MessageEnvelope> ReceiveQueue;
    }
}
