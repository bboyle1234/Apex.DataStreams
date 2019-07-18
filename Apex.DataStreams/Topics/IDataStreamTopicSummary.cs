using Apex.DataStreams.Encoding;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Apex.DataStreams.Topics {

    public interface IDataStreamTopicSummary : IDisposable {

        /// <summary>
        /// Called when a new message is being sent for this particular topic.
        /// Caller topic guarantees not to interleave concurrent calls to any method in this class.
        /// </summary>
        Task OnMessage(MessageEnvelope envelope);

        /// <summary>
        /// Called when a new client has connected to the topic, and the topic needs to send a summary message that brings the new client "up to date".
        /// Caller topic guarantees not to interleave concurrent calls to any method in this class.
        /// You can return null to indicate no summary messages need to be sent. 
        /// You can return an array that has null items if not all placeholders in the array have a message to be sent.
        /// </summary>
        Task<object[]> GetTopicSummary();
    }
}
