using System;
using System.Threading.Tasks;
using Apex.DataStreams.Encoding;
using Apex.TimeStamps;

namespace Apex.DataStreams.Operations {

    public class SucceedFailOperation : IOperation {

        public TimeStamp EnqueuedAt { get; } = TimeStamp.Now;
        readonly TaskCompletionSource<object> TCS = new TaskCompletionSource<object>(TaskContinuationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Gets a task representing the completion of the <see cref="SucceedFailOperation"/>.
        /// </summary>
        public Task Task => TCS.Task;

        // Reduce allocations by using a pre-allocated static readonly action reference instead of using a lambda in the ContinueWith method call below.
        static readonly Action<Task> ObserveException = t => _ = t.Exception;

        public SucceedFailOperation() {
            /// TODO: Test and see if this is really necessary to prevent unobserved task exceptions,
            /// since to my limited knowledge, the TCS does not involve the TaskScheduler, I think this should not be necessary.
            TCS.Task.ContinueWith(ObserveException, TaskContinuationOptions.OnlyOnFaulted);
        }

        public void Succeed() {
            TCS.TrySetResult(null);
        }

        public void Fail(Exception x) {
            TCS.TrySetException(x);
        }
    }
}
