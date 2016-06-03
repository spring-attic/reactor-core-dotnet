using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core;
using System.Threading;
using Reactor.Core.flow;

namespace Reactor.Core
{
    /// <summary>
    /// Provides an abstract asychronous boundary to operators.
    /// </summary>
    public interface Scheduler
    {
        /// <summary>
        /// chedules the given task on this scheduler non-delayed execution.
        /// </summary>
        /// <param name="task">task the task to execute</param>
        /// <returns>The Cancellation instance that let's one cancel this particular task.
        /// If the Scheduler has been shut down, the <see cref="SchedulerHelper.Rejected"/> instance is returned.</returns>
        IDisposable Schedule(Action task);

        /// <summary>
        /// Instructs this Scheduler to prepare itself for running tasks
        /// directly or through its Workers.
        /// </summary>
        void Start();

        /// <summary>
        /// Instructs this Scheduler to release all resources and reject
        /// any new tasks to be executed.
        /// </summary>
        void Shutdown();

        /// <summary>
        /// Creates a worker of this Scheduler that executed task in a strict
        /// FIFO order, guaranteed non-concurrently with each other.
        /// </summary>
        /// <returns>The Worker instance.</returns>
        Worker CreateWorker();
    }

    /// <summary>
    /// A worker representing an asynchronous boundary that executes tasks in
    /// a FIFO order, guaranteed non-concurrently with respect to each other.
    /// </summary>
    public interface Worker : IDisposable
    {
        /// <summary>
        /// Schedules the task on this worker.
        /// </summary>
        /// <param name="task">task the task to schedule</param>
        /// <returns>The IDisposable instance that let's one cancel this particular task.
        /// If the Scheduler has been shut down, the <see cref="SchedulerHelper.Rejected"/> instance is returned.
        /// </returns>
        IDisposable Schedule(Action task);
    }

    /// <summary>
    /// Utility methods to help with schedulers.
    /// </summary>
    public static class SchedulerHelper
    {
        private static readonly RejectedDisposable REJECTED = new RejectedDisposable();

        /// <summary>
        /// The singleton instance of a rejected IDisposable indicator.
        /// </summary>
        public static IDisposable Rejected { get { return REJECTED; } }
    }

    /// <summary>
    /// The class representing a rejected task's IDisposable
    /// </summary>
    sealed class RejectedDisposable : IDisposable
    {
        public void Dispose()
        {
            // ignored
        }
    }
}
