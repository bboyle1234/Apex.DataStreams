using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Apex.DataStreams.Topics {

    /// <summary>
    /// Every <see cref="IDataStreamTopic"/> has an <see cref="ITopicSummary"/>.
    /// The <see cref="ITopicSummary"/> is responsible for bringing newly-connected clients up to date
    /// with the most recent information, giving them everything they need to know to immediately sync with all
    /// new <see cref="IDataStreamTopic"/> messages.
    /// </summary>
    public interface ITopicSummary : IDisposable {

        /// <summary>
        /// Called by the <see cref="ITopic"/> when a new message is being sent for this particular topic.
        /// The caller topic guarantees not to interleave concurrent calls to any method in this class.
        /// Use this method to implement a state-tracking system that can provide the topic's current state to any new client when it connects.
        /// </summary>
        ValueTask OnMessage<TMessage>(TMessage message);

        /// <summary>
        /// Called when a new client has connected to the topic, and the topic needs to send a summary message that brings the new client "up to date".
        /// Caller topic guarantees not to interleave concurrent calls to any method in this class.
        /// You can return null to indicate no summary messages need to be sent. 
        /// You can return an array that has null items if not all placeholders in the array have a message to be sent.
        /// </summary>
        /// <returns>
        /// An array of objects to be sent, in order, to the newly-connected client.
        /// You can return null to indicate no messages need to be sent.
        /// If the returned array contains null values, they will be ignored.
        /// </returns>
        ValueTask<object[]> GetTopicSummary();
    }
}
