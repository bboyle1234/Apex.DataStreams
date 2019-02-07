using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams {

    /// <summary>
    /// Maintains connections to a number of publisher end points according to the <see cref="DataStreamClientContext"> objects
    /// that are passed into its creator.
    /// Create IDataStreamClients via the service provider.
    /// <code>
    /// // Add data streams to the service provider.
    /// var serviceProvider = new ServiceCollection().AddDataStreams().BuildServiceProvider();
    /// // Get the factory that can be used to create clients.
    /// var factory = serviceProvider.GetService<IDataStreamClientFactory>();
    /// // Create a client that will connect to three publisher end points.
    /// var client = factory.Create(context1, context2, context3);
    /// </code>
    /// </summary>
    public interface IDataStreamClient : IDisposable {

        void Start();
        Task<DataStreamClientStatus> GetStatusAsync();
    }
}
