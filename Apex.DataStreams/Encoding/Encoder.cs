using Apex.KeyedServices;
using Apex.DataStreams.Definitions;
using Apex.ValueCompression;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;
using Apex.ValueCompression.Compressors;

namespace Apex.DataStreams.Encoding {

    internal sealed class Encoder : IEncoder {

        readonly ICompressorFactory CompressorFactory;

        public Encoder(IServiceProvider serviceProvider) {
            CompressorFactory = serviceProvider.GetRequiredService<ICompressorFactory>();
        }

        public MessageEnvelope Encode(DataStreamTopicDefinition topicDefinition, object message) {
            var messageType = message.GetType();
            if (!topicDefinition.TryGetMessageDefinition(messageType, out var messageDefinition))
                throw new Exception($"Could not resolve messageDefinition for message type '{messageType.FullName}' from topic '{topicDefinition.TopicName}({topicDefinition.TopicCode})'.");
            using (var ms = new MemoryStream(messageDefinition.MessageBufferSize)) {
                ms.Write(new byte[] { topicDefinition.TopicCode, messageDefinition.MessageCode, 0, 0 }, 0, 4);
                try {
                    CompressorFactory.GetRequiredCompressor(messageType).Compress(ms, message);
                } catch (Exception x) {
                    throw new Exception($"Exception thrown while serializing topicCode '{topicDefinition.TopicCode}', messageTypeCode '{messageDefinition.MessageCode}', messageType '{messageDefinition.MessageType.FullName}'", x);
                }
                var messageBodyLength = ms.Length - 4;
                if (messageBodyLength > ushort.MaxValue) throw new Exception($"Message body length {messageBodyLength} exceed maximum allowed {ushort.MaxValue}.");
                ms.Seek(2, 0);
                ms.Write(BitConverter.GetBytes((ushort)messageBodyLength), 0, 2);
                return new MessageEnvelope(topicDefinition.TopicCode, messageDefinition.MessageCode, messageType, ms.ToArray(), message);
            }
        }

        public async Task<MessageEnvelope> ReadMessageFromSocketAsync(DataStreamDefinition dataStreamDefinition, Socket socket) {
            var header = await socket.ReadAllBytes(4).ConfigureAwait(false);
            if (!dataStreamDefinition.TryGetMessageDefinition((topicCode: header[0], messageCode: header[1]), out var messageDefinition))
                throw new Exception($"Unable to resolve message definition from topicCode '{header[0]}' and messageTypeCode '{header[1]}'.");
            var messageBodyLength = BitConverter.ToUInt16(header, 2);
            var messageBytes = await socket.ReadAllBytes(messageBodyLength).ConfigureAwait(false);
            using (var ms = new MemoryStream(messageBytes)) {
                try {
                    var message = CompressorFactory.GetRequiredDecompressor(messageDefinition.MessageType).Decompress(ms);
                    return new MessageEnvelope(header[0], header[1], messageDefinition.MessageType, messageBytes, message);
                } catch (Exception x) {
                    throw new Exception($"Exception thrown while deserializing topicCode '{header[0]}', messageTypeCode '{header[1]}', messageType '{messageDefinition.MessageType.FullName}'", x);
                }
            }
        }
    }
}
