using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Apex.DataStreams.Definitions {

    public sealed class DataStreamTopicDefinition {

        public string TopicName { get; }
        public byte TopicCode { get; }
        public IEnumerable<DataStreamMessageDefinition> Messages { get; }

        readonly Dictionary<Type, DataStreamMessageDefinition> ByMessageType;

        internal DataStreamTopicDefinition(string topicName, byte topicCode, IEnumerable<DataStreamMessageDefinition> messages) {
            TopicName = topicName;
            TopicCode = topicCode;
            Messages = messages;
            ByMessageType = messages.ToDictionary(m => m.MessageType);
        }

        internal bool TryGetMessageDefinition(Type messageType, out DataStreamMessageDefinition messageDefinition)
            => ByMessageType.TryGetValue(messageType, out messageDefinition);
    }
}
