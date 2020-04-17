using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams.Topics {

    /// <summary>
    /// A topic summary manager that uses the topic's last-sent message as the summary message.
    /// In other words, it assumes that the last-sent message contains ALL the information needed to bring a new client
    /// up-to-date with current data for this feed topic.
    /// </summary>
    public sealed class SimpleTopicSummary : ITopicSummary, IDisposable {

        object[] _lastMessage = new object[1] { null };

        public ValueTask OnMessage(object message) {
            _lastMessage[0] = message;
            return default;
        }

        public ValueTask<object[]> GetTopicSummary()
            => new ValueTask<object[]>(_lastMessage);

        public void Dispose() { }
    }
}
