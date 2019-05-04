using Apex.DataStreams.Definitions;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Apex.DataStreams.Encoding {

    internal interface IEncoder {

        MessageEnvelope Encode(DataStreamTopicDefinition topicDefinition, object message);
        Task<MessageEnvelope> ReadMessageFromSocketAsync(DataStreamDefinition dataStreamDefinition, Socket socket);
    }
}
