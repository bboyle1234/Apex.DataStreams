using Apex.TimeStamps;
using System.Threading.Tasks;

namespace Apex.DataStreams.Operations {

    /// <summary>
    /// Represents an operation that is completed when the <see cref="IOperation.Task"/> is completed.
    /// </summary>
    public interface IOperation {

        /// <summary>
        /// The time that the operation started.
        /// </summary>
        TimeStamp EnqueuedAt { get; }

        /// <summary>
        /// The task that this operation represents.
        /// </summary>
        Task Task { get; }
    }
}
