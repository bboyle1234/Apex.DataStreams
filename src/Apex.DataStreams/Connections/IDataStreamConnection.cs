using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Apex.DataStreams.Encoding;
using Apex.DataStreams.Operations;

namespace Apex.DataStreams.Connections {

    public interface IDataStreamConnection : IDisposable {
        EndPoint RemoteEndPoint { get; }
        Task<IOperation> EnqueueAsync(MessageEnvelope message);
        Task<DataStreamConnectionStatus> GetStatusAsync();
    }
}
