using Apex.DataStreams.Definitions;
using Apex.ValueCompression;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Apex.DataStreams.Encoding {
    public sealed class CompressionEncoder : IEncoder {

        readonly IServiceProvider ServiceProvider;
        readonly ConcurrentDictionary<Type, object> Compressors;

        public CompressionEncoder(IServiceProvider serviceProvider) {
            ServiceProvider = serviceProvider;
            Compressors = new ConcurrentDictionary<Type, object>();
        }

        ICompressor GetCompressor(Type type) {
            return (ICompressor)Compressors.GetOrAdd(type, t => {
                var serviceType = typeof(ICompressor<>).MakeGenericType(t);
                return ServiceProvider.GetRequiredService(serviceType);
            });
        }

        IDecompressor GetDecompressor(Type type) {
            return (IDecompressor)Compressors.GetOrAdd(type, t => {
                var serviceType = typeof(IDecompressor<>).MakeGenericType(t);
                return ServiceProvider.GetRequiredService(serviceType);
            });
        }

        public MessageEnvelope Encode(DataStreamTopicDefinition topicDefinition, object message) {
            var messageType = message.GetType();
            if (!topicDefinition.TryGetMessageDefinition(messageType, out var messageDefinition))
                throw new Exception($"Could not resolve messageDefinition for message type '{messageType.FullName}' from topic '{topicDefinition.TopicName}({topicDefinition.TopicCode})'.");

            using (var ms = new MemoryStream(messageDefinition.MessageBufferSize)) {
                ms.Write(new byte[] { topicDefinition.TopicCode, messageDefinition.MessageCode, 0, 0 }, 0, 4);
                GetCompressor(messageType).Compress(ms, message);

                /// Now go back and fill in the length field, making sure that the message body length can be formatted as a UInt16 (2 bytes)
                ms.Seek(2, 0);
                var messageBodyLength = ms.Length - 4;
                if (messageBodyLength > ushort.MaxValue) throw new Exception($"Message body length {messageBodyLength} exceed maximum allowed {ushort.MaxValue}.");
                ms.Write(BitConverter.GetBytes((ushort)messageBodyLength), 0, 2);

                return new MessageEnvelope(topicDefinition.TopicCode, messageDefinition.MessageCode, messageType, ms.ToArray(), message);
            }
        }
        public async Task<MessageEnvelope> ReadMessageFromSocketAsync(DataStreamDefinition dataStreamDefinition, Socket socket) {

            /// First we read the header to get topicCode and messageTypeCode, but more importantly at this 
            /// stage, the length of the message body.
            var header = await socket.ReadAllBytes(4).ConfigureAwait(false);
            if (!dataStreamDefinition.TryGetMessageDefinition((topicCode: header[0], messageCode: header[1]), out var messageDefinition))
                throw new Exception($"Unable to resolve message definition from topicCode '{header[0]}' and messageTypeCode '{header[1]}'.");

            var decompressor = GetDecompressor(messageDefinition.MessageType);
            var messageBodyLength = BitConverter.ToUInt16(header, 2);

            /// Then we read the rest of the message body, deserializing it into the expected message type.
            var messageBytes = await socket.ReadAllBytes(messageBodyLength).ConfigureAwait(false);
            using (var ms = new MemoryStream(messageBytes)) {
                try {
                    var message = decompressor.Decompress(ms);
                    return new MessageEnvelope(header[0], header[1], messageDefinition.MessageType, messageBytes, message);
                } catch (Exception x) {
                    throw new Exception($"Exception thrown while deserializing topicCode '{header[0]}', messageTypeCode '{header[1]}', messageType '{messageDefinition.MessageType.FullName}'", x);
                }
            }
        }
    }
}
