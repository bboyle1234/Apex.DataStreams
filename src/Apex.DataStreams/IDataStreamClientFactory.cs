using Apex.DataStreams.Encoding;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams {

    /// <summary>
    /// Use this class to create data stream clients. Access the class via your service provider.
    /// <code>
    /// var serviceProvider = new ServiceCollection().AddDataStreams().Build();
    /// var clientFactory = serviceProvider.GetService<IDataStreamClientFactory>();
    /// </code>
    /// </summary>
    public interface IDataStreamClientFactory {

        IDataStreamClient Create(IEnumerable<DataStreamClientContext> contexts);
        IDataStreamClient Create(params DataStreamClientContext[] contexts);
    }
}
