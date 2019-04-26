using Apex.DataStreams.Connections;
using Apex.DataStreams.Definitions;
using Apex.DataStreams.Encoding;
using Apex.ServiceUtilities;
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

    internal sealed class Client : ServiceBase, IDataStreamClient {

        readonly List<ClientConnectionWorker> ConnectionWorkers;

        public Client(IServiceProvider serviceProvider, IEnumerable<ClientContext> contexts) {
            ConnectionWorkers = contexts
                .Select(c => new ClientConnectionWorker(serviceProvider, c))
                .ToList();
        }

        protected override void CustomStart() {
            ConnectionWorkers.ForEach(c => c.Start());
        }
        protected override void CustomDispose(bool disposing) {
            ConnectionWorkers.ForEach(w => w.Dispose());
        }

        public async Task<ClientStatus> GetStatusAsync() {
            var tasks = ConnectionWorkers.Select(c => c.GetStatusAsync());
            await Task.WhenAll(tasks).ConfigureAwait(false);
            return new ClientStatus {
                Connections = tasks.Select(t => t.Result).ToList(),
            };
        }
    }
}