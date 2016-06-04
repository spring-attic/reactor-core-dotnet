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
    /// Interface indicating a Time source that can tell the current time in UTC
    /// milliseconds
    /// </summary>
    public interface ITimeSource
    {
        /// <summary>
        /// The current UTC time.
        /// </summary>
        long NowUtc { get; }
    }

    /// <summary>
    /// Interface to indicate support for non-delayed scheduling of tasks.
    /// </summary>
    public interface IScheduling
    {
        /// <summary>
        /// Schedules the given task on this scheduler/worker non-delayed execution.
        /// </summary>
        /// <param name="task">task the task to execute</param>
        /// <returns>The Cancellation instance that let's one cancel this particular task.
        /// If the Scheduler has been shut down, the <see cref="SchedulerHelper.Rejected"/> instance is returned.</returns>
        IDisposable Schedule(Action task);
    }

    /// <summary>
    /// Interface to indicate support for delayed and periodic scheduling of tasks.
    /// </summary>
    public interface ITimedScheduling : ITimeSource, IScheduling
    {
        /// <summary>
        /// Schedules the execution of the given task with the given delay amount.
        /// </summary>
        /// <param name="task">The task to execute.</param>
        /// <param name="delay">The delay amount</param>
        /// <returns>The IDisposable to cancel the task</returns>
        IDisposable Schedule(Action task, TimeSpan delay);

        /// <summary>
        /// Schedules a periodic execution of the given task with the given initial delay and period.
        /// </summary>
        /// <param name="task">The tast to execute periodically</param>
        /// <param name="initialDelay">The initial delay</param>
        /// <param name="period">The period amount</param>
        /// <returns>The IDisposable to cancel the periodic task</returns>
        IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period);
    }


    /// <summary>
    /// Provides an abstract asychronous boundary to operators.
    /// </summary>
    public interface Scheduler : IScheduling
    {
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
    public interface Worker : IDisposable, IScheduling
    {
    }

    /// <summary>
    /// Provides an abstract, timed asychronous boundary to operators.
    /// </summary>
    public interface TimedScheduler : Scheduler, ITimedScheduling
    {
        /// <summary>
        /// Creates a timed worker of this Scheduler that executed task in a strict
        /// FIFO order, guaranteed non-concurrently with each other.
        /// </summary>
        /// <returns></returns>
        TimedWorker CreateTimedWorker();

    }

    /// <summary>
    /// A timed worker representing an asynchronous boundary that executes tasks in
    /// a FIFO order, possibly delayed and guaranteed non-concurrently with respect
    /// to each other (delayed or non-delayed alike).
    /// </summary>
    public interface TimedWorker : Worker, ITimedScheduling
    {
    }

    /// <summary>
    /// Utility methods to help with schedulers.
    /// </summary>
    public static class SchedulerHelper
    {
        private static readonly RejectedDisposable REJECTED = new RejectedDisposable();

        private static DateTimeOffset Epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// The singleton instance of a rejected IDisposable indicator.
        /// </summary>
        public static IDisposable Rejected { get { return REJECTED; } }

        /// <summary>
        /// Converts the DateTimeOffset into total number of milliseconds since the epoch.
        /// </summary>
        /// <param name="dt"></param>
        /// <returns></returns>
        public static long UtcMillis(this DateTimeOffset dt)
        {
            return (long)(dt - Epoch).TotalMilliseconds;
        }
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
