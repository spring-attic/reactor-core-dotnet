using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core;
using System.Threading;
using Reactor.Core.flow;
using Reactor.Core.subscription;
using Reactor.Core.util;
using System.Collections.Concurrent;

namespace Reactor.Core.scheduler
{
    /// <summary>
    /// A scheduler that runs tasks on the task pool.
    /// </summary>
    public sealed class DefaultScheduler : TimedScheduler
    {

        private DefaultScheduler()
        {

        }

        private static readonly DefaultScheduler INSTANCE = new DefaultScheduler();

        /// <summary>
        /// Returns the singleton instance of this scheduler
        /// </summary>
        public static DefaultScheduler Instance { get { return INSTANCE; } }

        /// <inheritdoc/>
        public long NowUtc
        {
            get
            {
                return DateTimeOffset.UtcNow.UtcMillis();
            }
        }

        /// <inheritdoc/>
        public TimedWorker CreateTimedWorker()
        {
            return new DefaultWorker();
        }

        /// <inheritdoc/>
        public Worker CreateWorker()
        {
            return new DefaultWorker();
        }

        /// <inheritdoc/>
        public IDisposable Schedule(Action task)
        {
            return ScheduleNow(task);
        }

        /// <inheritdoc/>
        public IDisposable Schedule(Action task, TimeSpan delay)
        {
            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;

            Task.Delay(delay, ct).ContinueWith(t => task(), ct);

            return tokenSource;
        }

        /// <inheritdoc/>
        public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
        {
            IndexedMultipleDisposable d = new IndexedMultipleDisposable();

            long start = NowUtc + (long)initialDelay.TotalMilliseconds;
            PeriodicTask t = new PeriodicTask(task, d, start, period, this);

            d.Replace(Schedule(t.Run, initialDelay), 0);

            return d;
        }

        internal static IDisposable ScheduleNow(Action task)
        {
            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;

            Task.Run(task, ct);

            return tokenSource;
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
            // not supported
        }

        /// <inheritdoc/>
        public void Start()
        {
            // not supported
        }
    }

    sealed class DefaultWorker : TimedWorker
    {

        readonly ConcurrentQueue<DefaultTask> queue;

        bool shutdown;

        int wip;

        internal DefaultWorker()
        {
            this.queue = new ConcurrentQueue<DefaultTask>();
        }

        /// <inheritdoc/>
        public long NowUtc
        {
            get
            {
                return DateTimeOffset.UtcNow.UtcMillis();
            }
        }

        public void Dispose()
        {
            Volatile.Write(ref shutdown, true);

            clearQueue();
        }

        void clearQueue()
        {
            DefaultTask dummy;
            while (queue.TryDequeue(out dummy)) ;
        }

        internal bool lvShutdown()
        {
            return Volatile.Read(ref shutdown);
        }

        public IDisposable Schedule(Action task)
        {
            if (lvShutdown())
            {
                return SchedulerHelper.Rejected;
            }

            DefaultTask dt = new DefaultTask(task, this);

            queue.Enqueue(dt);

            if (lvShutdown())
            {
                clearQueue();
                return SchedulerHelper.Rejected;
            }

            if (QueueDrainHelper.Enter(ref wip))
            {
                Task.Run(() => this.Drain());
            }

            return dt;
        }

        void Drain()
        {
            DefaultTask dt;

            ConcurrentQueue<DefaultTask> q = queue;

            int missed = 1;

            for (;;)
            {

                for (;;)
                {
                    if (lvShutdown())
                    {
                        return;
                    }

                    if (q.TryDequeue(out dt))
                    {
                        dt.Run();
                    }
                    else
                    {
                        break;
                    }
                }

                if ((missed = QueueDrainHelper.Leave(ref wip, missed)) == 0)
                {
                    break;
                }
            }
        }

        /// <inheritdoc/>
        public IDisposable Schedule(Action task, TimeSpan delay)
        {
            var tokenSource = new CancellationTokenSource();
            CancellationToken ct = tokenSource.Token;

            Task.Delay(delay, ct).ContinueWith(t => {

                IDisposable d = Schedule(task);

                ct.Register(() => d.Dispose());
            }, ct);

            return tokenSource;
        }

        /// <inheritdoc/>
        public IDisposable Schedule(Action task, TimeSpan initialDelay, TimeSpan period)
        {
            IndexedMultipleDisposable d = new IndexedMultipleDisposable();

            long start = NowUtc + (long)initialDelay.TotalMilliseconds;
            PeriodicTask t = new PeriodicTask(task, d, start, period, this);

            d.Replace(Schedule(t.Run, initialDelay), 0);

            return d;
        }
    }

    /// <summary>
    /// The wrapper that has back reference to the worker and captures
    /// exceptions thrown by the wrapped Action.
    /// </summary>
    sealed class DefaultTask : IDisposable
    {
        readonly Action task;

        readonly DefaultWorker parent;

        bool disposed;

        public DefaultTask(Action task, DefaultWorker parent)
        {
            this.task = task;
            this.parent = parent;
        }

        public void Dispose()
        {
            Volatile.Write(ref disposed, true);
        }

        internal void Run()
        {
            if (!Volatile.Read(ref disposed) && !parent.lvShutdown())
            {
                try
                {
                    task();
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowIfFatal(ex);
                    ExceptionHelper.OnErrorDropped(ex);
                }
            }
        }
    }

    /// <summary>
    /// Wraps an action and manages a fixed-rate periodic execution of it.
    /// </summary>
    internal sealed class PeriodicTask
    {
        readonly Action task;

        readonly IndexedMultipleDisposable d;

        readonly TimeSpan period;

        readonly ITimedScheduling scheduler;

        readonly long start;

        long count;

        public PeriodicTask(Action task, IndexedMultipleDisposable d, long start, TimeSpan period, ITimedScheduling scheduler)
        {
            this.task = task;
            this.start = start;
            this.d = d;
            this.scheduler = scheduler;
        }

        internal void Run()
        {
            try
            {
                task();
            }
            catch (Exception ex)
            {
                ExceptionHelper.ThrowIfFatal(ex);
                ExceptionHelper.OnErrorDropped(ex);
                return;
            }

            long c = count + 1;
            count = c;
            long next = start + c * (long)period.TotalMilliseconds;

            long delay = next - scheduler.NowUtc;

            if (delay > 0L)
            {
                d.Replace(scheduler.Schedule(this.Run, TimeSpan.FromMilliseconds(delay)), c);
            }
            else
            {
                d.Replace(scheduler.Schedule(this.Run), c);
            }
        }
    }
}
