using System;
using System.Threading.Tasks;
using Apex.DataStreams.Encoding;
using Apex.TimeStamps;

namespace Apex.DataStreams.Operations {

    public class SucceedFailOperation : IOperation {

        public TimeStamp EnqueuedAt { get; } = TimeStamp.Now;
        readonly TaskCompletionSource<object> TCS = new TaskCompletionSource<object>();

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
