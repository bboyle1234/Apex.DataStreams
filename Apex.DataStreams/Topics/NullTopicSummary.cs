using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams.Topics {

    /// <summary>
    /// A topic summary manager that doesn't create any summary message for new client connections.
    /// Use this for streaming event notifications etc that don't require any history.
    /// </summary>
    public sealed class NullTopicSummary : ITopicSummary, IDisposable {

        public ValueTask OnMessage(object message) => default;
        public ValueTask<object[]> GetTopicSummary() => default;

        public void Dispose() { }
    }
}
