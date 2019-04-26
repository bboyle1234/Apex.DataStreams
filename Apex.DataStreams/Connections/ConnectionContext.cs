using Nito.AsyncEx;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Apex.DataStreams.Definitions;
using Apex.DataStreams.Encoding;

namespace Apex.DataStreams.Connections {

    internal sealed class ConnectionContext {

        public DataStreamDefinition Definition;
        public IEncoder Encoder;
        public Socket Socket;
        public AsyncProducerConsumerQueue<MessageEnvelope> ReceiveQueue;
        public Func<Connection, Exception, Task> DisconnectedCallback;
        public int MaxMessageSendTimeInSeconds;
    }
}
