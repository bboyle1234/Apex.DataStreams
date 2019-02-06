using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Apex.DataStreams.Definitions {

    public sealed class DataStreamDefinition {

        public readonly string Name;
        public IEnumerable<DataStreamTopicDefinition> Topics { get; }

        readonly Dictionary<(byte topicCode, byte messageCode), DataStreamMessageDefinition> ByCodes;

        internal DataStreamDefinition(string name, IEnumerable<DataStreamTopicDefinition> topics) {
            Name = name;
            Topics = topics;
            ByCodes = new Dictionary<(byte topicCode, byte messageCode), DataStreamMessageDefinition>();
            foreach (var topic in topics) {
                foreach (var message in topic.Messages) {
                    ByCodes[(topic.TopicCode, message.MessageCode)] = message;
                }
            }
        }

        internal bool TryGetMessageDefinition((byte topicCode, byte messageCode) codes, out DataStreamMessageDefinition messageDefinition)
            => ByCodes.TryGetValue(codes, out messageDefinition);

        internal DataStreamTopicDefinition GetAdminTopic()
            => Topics.FirstOrDefault(t => t.TopicName == "Administration");

        public DataStreamTopicDefinition GetTopicByName(string topicName) {
            var topic = Topics.SingleOrDefault(t => t.TopicName == topicName);
            if (null == topic) throw new Exception($"Topic with name '{topicName}' does not exist.");
            return topic;
        }

        public static DataStreamDefinitionBuilder Create(string name)
            => new DataStreamDefinitionBuilder(name);
    }
}
