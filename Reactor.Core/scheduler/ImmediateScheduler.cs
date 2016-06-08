using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core;
using System.Threading;
using Reactor.Core.flow;
using Reactor.Core.subscriber;
using Reactor.Core.subscription;
using Reactor.Core.util;

namespace Reactor.Core.scheduler
{
    /// <summary>
    /// A basic scheduler that executes actions immediately on the caller thread.
    /// Useful in conjunction with <see cref="Flux.PublishOn{T}(IFlux{T}, Scheduler, int, bool)"/>
    /// to rebatch downstream requests.
    /// </summary>
    public sealed class ImmediateScheduler : Scheduler, Worker
    {
        static readonly ImmediateScheduler INSTANCE = new ImmediateScheduler();

        /// <summary>
        /// Returns the singleton instance of this scheduler.
        /// </summary>
        public static ImmediateScheduler Instance { get { return INSTANCE; } }

        /// <inheritdoc/>
        public Worker CreateWorker()
        {
            return this;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // ignored
        }

        /// <inheritdoc/>
        public IDisposable Schedule(Action task)
        {
            task();
            return DisposableHelper.Disposed;
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
            // ignored
        }

        /// <inheritdoc/>
        public void Start()
        {
            // ignored
        }
    }
}
