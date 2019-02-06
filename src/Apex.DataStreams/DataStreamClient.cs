using Apex.DataStreams.Connections;
using Apex.DataStreams.Definitions;
using Apex.DataStreams.Encoding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CT = System.Threading.CancellationToken;
using CTS = System.Threading.CancellationTokenSource;

namespace Apex.DataStreams {

    internal sealed class DataStreamClient : IDataStreamClient, IDisposable {

        readonly List<DataStreamConnectionWorker> ConnectionWorkers;

        long _started = 0;
        long _disposed = 0;

        public DataStreamClient(IServiceProvider serviceProvider, IEnumerable<DataStreamClientContext> contexts) {
            ConnectionWorkers = contexts
                .Select(c => new DataStreamConnectionWorker(serviceProvider, c.PublisherEndPoint, c.DataStreamDefinition, c.ReceiveQueue))
                .ToList();
        }

        public void Start() {
            if (Interlocked.CompareExchange(ref _started, 1, 0) == 1) throw new InvalidOperationException("Cannot be started more than once.");
            ConnectionWorkers.ForEach(c => c.Start());
        }

        public async Task<DataStreamClientStatus> GetStatusAsync() {
            var tasks = ConnectionWorkers.Select(c => c.GetStatusAsync());
            await Task.WhenAll(tasks);
            return new DataStreamClientStatus {
                Connections = tasks.Select(t => t.Result).ToList(),
            };
        }

        #region IDisposable

        public void Dispose() {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1) return;
            ConnectionWorkers.ForEach(w => w.Dispose());
            GC.SuppressFinalize(this);
        }

        ~DataStreamClient() => Dispose();

        #endregion
    }
}