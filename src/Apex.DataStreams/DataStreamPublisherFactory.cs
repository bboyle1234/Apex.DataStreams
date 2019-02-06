using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams {

    internal sealed class DataStreamPublisherFactory : IDataStreamPublisherFactory {

        readonly IServiceProvider ServiceProvider;

        public DataStreamPublisherFactory(IServiceProvider serviceProvider) {
            ServiceProvider = serviceProvider;
        }

        public IDataStreamPublisher Create(DataStreamPublisherConfiguration configuration, Func<IDataStreamPublisher, Exception, Task> errorCallback)
            => new DataStreamPublisher(ServiceProvider, configuration, errorCallback);
    }
}
