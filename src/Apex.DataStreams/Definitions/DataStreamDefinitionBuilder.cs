using Apex.DataStreams.AdminMessages;
using Apex.DataStreams.Definitions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams.Definitions {

    public sealed class DataStreamDefinitionBuilder : IEnumerable {

        readonly string Name;
        readonly List<(string topicName, byte topicCode)> Topics;
        readonly List<(string topicName, byte messageCode, Type messageType, int messageBufferSize)> Messages;

        public DataStreamDefinitionBuilder(string name) {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("name cannot be empty.");
            Name = name;
            Topics = new List<(string topicName, byte topicCode)>();
            Messages = new List<(string topicName, byte messageCode, Type messageType, int messageBufferSize)>();
            Add("Administration", 255);
            Add("Administration", 0, typeof(HeartBeat), 16);
        }

        public DataStreamDefinitionBuilder Add(string topicName, byte topicCode) {
            if (Topics.Any(t => t.topicName == topicName)) throw new Exception($"Topic with name '{topicName}' already exists.");
            if (Topics.Any(t => t.topicCode == topicCode)) throw new Exception($"Topic with code '{topicCode}' already exists.");
            Topics.Add((topicName, topicCode));
            return this;
        }

        public DataStreamDefinitionBuilder Add(string topicName, byte messageCode, Type messageType, int messageBufferSize = 256) {
            if (!Topics.Any(t => t.topicName == topicName)) throw new Exception($"Topic with name '{topicName}' doesn't exist.");
            if (Messages.Any(m => m.topicName == topicName && m.messageCode == messageCode)) throw new Exception($"Message with topicName '{topicName}' and messageCode '{messageCode}' already exists.");
            if (Messages.Any(m => m.topicName == topicName && m.messageType == messageType)) throw new Exception($"Message with topicName '{topicName}' and messageType '{messageType.FullName}' already exists.");
            Messages.Add((topicName, messageCode, messageType, messageBufferSize));
            return this;
        }

        public IEnumerator GetEnumerator()
            => throw new NotImplementedException();

        public DataStreamDefinition Build() {
            var topicDefinitions = new List<DataStreamTopicDefinition>();
            foreach (var topic in Topics) {
                var messageDefinitions = new List<DataStreamMessageDefinition>();
                foreach (var message in Messages.Where(m => m.topicName == topic.topicName)) {
                    messageDefinitions.Add(new DataStreamMessageDefinition(message.messageCode, message.messageType, message.messageBufferSize));
                }
                var topicDefinition = new DataStreamTopicDefinition(topic.topicName, topic.topicCode, messageDefinitions);
                topicDefinitions.Add(topicDefinition);
            }
            var definition = new DataStreamDefinition(Name, topicDefinitions);
            return definition;
        }

        public static implicit operator DataStreamDefinition(DataStreamDefinitionBuilder builder)
            => builder.Build();
    }
}
