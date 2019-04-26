using System;
using System.Runtime.Serialization;

namespace Apex.DataStreams.Definitions {

    public sealed class DataStreamMessageDefinition {

        public byte MessageCode { get; }
        public Type MessageType { get; }
        public int MessageBufferSize { get; }

        internal DataStreamMessageDefinition(byte messageCode, Type messageType, int messageBufferSize) {
            MessageCode = messageCode;
            MessageType = messageType;
            MessageBufferSize = messageBufferSize;
        }
    }
}
