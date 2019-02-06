using System;

namespace Apex.DataStreams.Encoding {

    public sealed class MessageEnvelope {

        public readonly byte TopicCode;
        public readonly byte MessageTypeCode;
        public readonly Type MessageType;
        public readonly ArraySegment<byte> MessageBytes;
        public readonly object Message;

        public MessageEnvelope(byte topicCode, byte messageTypeCode, Type messageType, ArraySegment<byte> messageBytes, object message) {
            TopicCode = topicCode;
            MessageTypeCode = messageTypeCode;
            MessageType = messageType;
            MessageBytes = messageBytes;
            Message = message;
        }
    }


}
