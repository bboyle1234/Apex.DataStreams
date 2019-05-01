using Apex.DataStreams.Definitions;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Threading.Tasks;

namespace Apex.DataStreams.Encoding {

    /// <summary>
    /// Encodes and decodes messages with the following format:
    /// HEADER:
    ///   1 byte:  topicCode
    ///   1 byte:  messageTypeCode
    ///   2 bytes: length of body (x), expressed as UInt16 (short)
    /// BODY:
    ///   x bytes: Bson representation of Json serialization of the message object.
    /// </summary>
    public sealed class BsonEncoder : IEncoder {

        readonly ISerializer Serializer;

        public BsonEncoder(ISerializer serializer) {
            Serializer = serializer;
        }

        /// <summary>
        /// Encodes the given topic/message pair into an <see cref="MessageEnvelope"/> ready for sending to multiple sockets.
        /// </summary>
        public MessageEnvelope Encode(DataStreamTopicDefinition topicDefinition, Object message) {
            var messageType = message.GetType();
            if (!topicDefinition.TryGetMessageDefinition(messageType, out var messageDefinition))
                throw new Exception($"Could not resolve messageDefinition for message type '{messageType.FullName}' from topic '{topicDefinition.TopicName}({topicDefinition.TopicCode})'.");

            /// Make the MemoryStream create the expected storage size upfront to reduce the number of byte[] allocations it performs.
            using (var ms = new MemoryStream(messageDefinition.MessageBufferSize)) {
                /// Create the four-byte header, including space for the length field, which is not known yet.
                /// We do it this way to avoid creating and splicing multiple arrays together later when the message body size becomes known.
                ms.Write(new byte[] { topicDefinition.TopicCode, messageDefinition.MessageCode, 0, 0 }, 0, 4);

                using (var writer = new BsonDataWriter(ms)) {
                    Serializer.Serialize(writer, message);
                    writer.Flush();

                    /// Now go back and fill in the length field, making sure that the message body length can be formatted as a UInt16 (2 bytes)
                    ms.Seek(2, 0);
                    var messageBodyLength = ms.Length - 4;
                    if (messageBodyLength > ushort.MaxValue) throw new Exception($"Message body length {messageBodyLength} exceed maximum allowed {ushort.MaxValue}.");
                    ms.Write(BitConverter.GetBytes((ushort)messageBodyLength), 0, 2);
                }

                return new MessageEnvelope(topicDefinition.TopicCode, messageDefinition.MessageCode, messageType, ms.ToArray(), message);
            }
        }

        /// <summary>
        /// Reads a <see cref="DecodedMessageEnvelope"/> directly from the given socket.
        /// </summary>
        public async Task<MessageEnvelope> ReadMessageFromSocketAsync(DataStreamDefinition dataStreamDefinition, Socket socket) {

            /// First we read the header to get topicCode and messageTypeCode, but more importantly at this 
            /// stage, the length of the message body.
            var header = await socket.ReadAllBytes(4).ConfigureAwait(false);
            if (!dataStreamDefinition.TryGetMessageDefinition((topicCode: header[0], messageCode: header[1]), out var messageDefinition))
                throw new Exception($"Unable to resolve message definition from topicCode '{header[0]}' and messageTypeCode '{header[1]}'.");

            var messageBodyLength = BitConverter.ToUInt16(header, 2);

            /// Then we read the rest of the message body, deserializing it into the expected message type.
            var messageBytes = await socket.ReadAllBytes(messageBodyLength).ConfigureAwait(false);
            using (var ms = new MemoryStream(messageBytes))
            using (var reader = new BsonDataReader(ms)) {
                try {
                    var message = Serializer.Deserialize(reader, messageDefinition.MessageType);
                    return new MessageEnvelope(header[0], header[1], messageDefinition.MessageType, messageBytes, message);
                } catch (Exception x) {
                    throw new Exception($"Exception thrown while deserializing topicCode '{header[0]}', messageTypeCode '{header[1]}', messageType '{messageDefinition.MessageType.FullName}'", x);
                }
            }
        }
    }
}
