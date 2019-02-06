using Apex.DataStreams.Definitions;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Apex.DataStreams.Encoding {

    public interface IEncoder {

        MessageEnvelope Encode(DataStreamTopicDefinition topicDefinition, object message);
        Task<MessageEnvelope> ReadMessageFromSocketAsync(DataStreamDefinition dataStreamDefinition, Socket socket);
    }
}
