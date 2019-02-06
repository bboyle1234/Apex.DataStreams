using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Apex.DataStreams.Encoding;
using Nito.AsyncEx;

namespace Apex.DataStreams {

    internal sealed class DataStreamClientFactory : IDataStreamClientFactory {

        readonly IServiceProvider ServiceProvider;

        public DataStreamClientFactory(IServiceProvider serviceProvider) {
            ServiceProvider = serviceProvider;
        }

        public IDataStreamClient Create(IEnumerable<DataStreamClientContext> contexts)
            => new DataStreamClient(ServiceProvider, contexts);

        public IDataStreamClient Create(params DataStreamClientContext[] contexts)
            => Create(contexts);
    }
}
