using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams {

    public interface IDataStreamPublisherFactory {

        IDataStreamPublisher Create(DataStreamPublisherConfiguration configuration, Func<IDataStreamPublisher, Exception, Task> errorCallback);
    }
}
