using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams {

    public interface IDataStreamClient : IDisposable {

        void Start();
        Task<ClientStatus> GetStatusAsync();
    }
}
