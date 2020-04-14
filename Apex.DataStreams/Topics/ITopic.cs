using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams.Topics {

    public interface ITopic {

        /// <summary>
        /// Enqueues the message for sending to all clients.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the topic has been shutdown.</exception>
        ValueTask Enqueue(object message);
    }
}
