using System;
using System.Threading.Tasks;
using Apex.DataStreams.Encoding;
using Apex.TimeStamps;

namespace Apex.DataStreams.Operations {

    /// <summary>
    /// Use this class to represent an operation that you will manually complete by calling the 
    /// <see cref="Succeed"/> or <see cref="Fail(Exception)"/> methods.
    /// </summary>
    public class SucceedFailOperation : IOperation {

        readonly TaskCompletionSource<object> TCS = new TaskCompletionSource<object>();

        /// <inheritdoc />
        public TimeStamp EnqueuedAt { get; } = TimeStamp.Now;

        /// <inheritdoc />
        public Task Task => TCS.Task;

        public SucceedFailOperation() {
            /// TODO: Test and see if this is really necessary to prevent unobserved task exceptions,
            /// since to my limited knowledge, the TCS does not involve the TaskScheduler, I think this should not be necessary.
            TCS.Task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }

        public void Succeed() {
            TCS.TrySetResult(null);
        }

        public void Fail(Exception x) {
            TCS.TrySetException(x);
        }
    }
}
