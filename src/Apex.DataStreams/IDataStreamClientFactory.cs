using Apex.DataStreams.Encoding;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams {

    public interface IDataStreamClientFactory {

        IDataStreamClient Create(IEnumerable<DataStreamClientContext> contexts);
        IDataStreamClient Create(params DataStreamClientContext[] contexts);
    }
}
