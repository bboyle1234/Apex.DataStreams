using Apex.DataStreams.Encoding;
using System;
using System.Threading.Tasks;

namespace Apex.DataStreams.Topics {

    /// <summary>
    /// Classes inheriting this interface are used to generate "Topic Summary" messages that
    /// are used to bring newly-connected clients up to date with the latest data state for the topic.
    /// After receiving the summary message, clients will be able to receive live messages from the topic
    /// and will be synchronized with the state of the topic's data.
    /// </summary>
    public interface IDataStreamTopicSummary : IDisposable {

        /// <summary>
        /// Called when a new message is being sent for this particular topic.
        /// Caller topic guarantees not to interleave concurrent calls to any method in this class.
        /// </summary>
        Task OnMessage(MessageEnvelope envelope);

        /// <summary>
        /// Called when a new client has connected to the topic, and the topic needs to send a summary message that brings the new client "up to date".
        /// Caller topic guarantees not to interleave concurrent calls to any method in this class.
        /// </summary>
        Task<MessageEnvelope> GetTopicSummary();
    }
}
