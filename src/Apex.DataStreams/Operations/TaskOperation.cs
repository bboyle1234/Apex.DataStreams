using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Apex.TimeStamps;

namespace Apex.DataStreams.Operations {

    /// <summary>
    /// Use this class to create an <see cref="IOperation"/> object that completes when the given task is completed.
    /// </summary>
    public sealed class TaskOperation : IOperation {

        /// <inheritdoc />
        public TimeStamp EnqueuedAt { get; } = TimeStamp.Now;

        /// <inheritdoc />
        public Task Task { get; }

        /// <summary>
        /// Creates a <see cref="TaskOperation"/> from the given task object.
        /// </summary>
        /// <param name="task">The task that is to be represented as an operation.</param>
        public TaskOperation(Task task) {
            Task = task;
        }

        /// <summary>
        /// Use this method to create a ready-made <see cref="TaskOperation"/> that is already completed succesfully. 
        /// You don't need to supply a task object for this.
        /// </summary>
        public static TaskOperation Success()
            => new TaskOperation(Task.CompletedTask);

        /// <summary>
        /// Use this method to create a ready-made <see cref="TaskOperation"/> that is already completed with an exception. 
        /// You don't need to supply a task object for this.
        /// </summary>
        public static TaskOperation FromException(Exception x)
            => new TaskOperation(Task.FromException(x));
    }
}
