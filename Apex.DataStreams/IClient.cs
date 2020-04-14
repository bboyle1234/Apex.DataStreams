using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Apex.DataStreams {

    public interface IClient : IDisposable {
        void Start();
        ValueTask<ClientStatus> GetStatus();
    }
}
