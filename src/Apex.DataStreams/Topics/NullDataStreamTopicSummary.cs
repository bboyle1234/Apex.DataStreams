using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Apex.DataStreams.Encoding;

namespace Apex.DataStreams.Topics {

    /// <summary>
    /// A topic summary manager that doesn't create any summary message for new client connections.
    /// Use this for streaming event notifications etc that don't require any history.
    /// </summary>
    public sealed class NullDataStreamTopicSummary : IDataStreamTopicSummary, IDisposable {

        /// <inheritdoc/>
        public Task OnMessage(MessageEnvelope envelope) => Task.CompletedTask;

        /// <inheritdoc/>
        public Task<MessageEnvelope> GetTopicSummary() => Task.FromResult<MessageEnvelope>(null);


        public void Dispose() { }
    }
}
