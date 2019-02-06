using Apex.TimeStamps;
using System.Threading.Tasks;

namespace Apex.DataStreams.Operations {

    public interface IOperation {

        TimeStamp EnqueuedAt { get; }
        Task Task { get; }
    }
}
