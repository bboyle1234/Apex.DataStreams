using Apex.DataStreams.Definitions;
using Apex.DataStreams.Topics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams {

    public interface IDataStreamPublisher : IDisposable {

        PublisherConfiguration Configuration { get; }

        /// <summary>
        /// Makes the publisher start listening for incoming connections.
        /// </summary>
        void Start();

        /// <summary>
        /// Creates an ITopic for the given topic definition. You can then use the ITopic to enqueue messages for sending
        /// to clients. The topic will maintain a summary of its current "state" using the given topicManager so that it can 
        /// immediately update newly-connecting clients with the current state of this topic. You can create an unlimited number
        /// of topics with the same definition. Each maintains its own state.
        /// </summary>
        Task<IDataStreamTopic> CreateTopicAsync(DataStreamTopicDefinition definition, IDataStreamTopicSummary topicManager);

        /// <summary>
        /// Remove the ITopic to stop it automatically sending its status summary messages to newly-connecting clients.
        /// After removal, you will no longer be able to Enqueue messages on that ITopic.
        /// </summary>
        Task RemoveTopicAsync(IDataStreamTopic topic);

        /// <summary>
        /// Remove all topics from the publisher.
        /// </summary>
        Task ClearTopicsAsync();

        Task<PublisherStatus> GetStatusAsync();
    }
}
