using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Apex.DataStreams.Encoding;

namespace Apex.DataStreams.Topics {

    /// <summary>
    /// A topic summary manager that uses the topic's last-sent message as the summary message.
    /// In other words, it assumes that the last-sent message contains ALL the information needed to bring a new client
    /// up-to-date with current data for this feed topic.
    /// </summary>
    public sealed class SimpleTopicSummary : IDataStreamTopicSummary, IDisposable {

        MessageEnvelope _lastMessage;

        /// <inheritdoc/>
        public Task OnMessage(MessageEnvelope envelope) {
            _lastMessage = envelope;
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<MessageEnvelope> GetTopicSummary()
            => Task.FromResult(_lastMessage);


        public void Dispose() { }
    }
}
