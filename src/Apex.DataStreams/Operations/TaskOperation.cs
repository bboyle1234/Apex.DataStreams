using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Apex.TimeStamps;

namespace Apex.DataStreams.Operations {

    public sealed class TaskOperation : IOperation {

        public TimeStamp EnqueuedAt { get; } = TimeStamp.Now;
        public Task Task { get; }

        public TaskOperation(Task task) {
            Task = task;
        }

        public static TaskOperation Success()
            => new TaskOperation(Task.CompletedTask);

        public static TaskOperation FromException(Exception x)
            => new TaskOperation(Task.FromException(x));
    }
}
