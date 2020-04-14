using Apex.DataStreams.Connections;
using Apex.ServiceUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams {

    public sealed class Client : ServiceBase, IClient {

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

        public async ValueTask<ClientStatus> GetStatus() {
            var tasks = ConnectionWorkers.Select(c => c.GetStatusAsync());
            await Task.WhenAll(tasks).ConfigureAwait(false);
#pragma warning disable AsyncFixer02 // Long running or blocking operations under an async method
            return new ClientStatus {
                Connections = tasks.Select(t => t.Result).ToList(),
            };
#pragma warning restore AsyncFixer02 // Long running or blocking operations under an async method
        }
    }
}
