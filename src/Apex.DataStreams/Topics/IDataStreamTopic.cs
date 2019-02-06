using Apex.DataStreams.Connections;
using Apex.DataStreams.Definitions;
using Apex.DataStreams.Encoding;
using Apex.DataStreams.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams.Topics {

    public interface IDataStreamTopic {

        /// <summary>
        /// Enqueues the message for sending to all clients.
        /// </summary>
        /// <returns>IOperations representing the sending completion to each client.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the topic has been shutdown.</exception>
        Task<IEnumerable<IOperation>> EnqueueAsync(object message);

    }
}
