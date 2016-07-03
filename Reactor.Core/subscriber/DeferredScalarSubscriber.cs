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

namespace Reactor.Core.subscriber
{
    /// <summary>
    /// A subscriber that takes values and produces a single output in a fuseable,
    /// backpressure aware manner.
    /// </summary>
    /// <typeparam name="T">The input value type</typeparam>
    /// <typeparam name="R">The single output value type</typeparam>
    public abstract class DeferredScalarSubscriber<T, R> : DeferredScalarSubscription<R>, ISubscriber<T>
    {
        /// <summary>
        /// The ISubscription to the upstream.
        /// </summary>
        protected ISubscription s;

        /// <summary>
        /// Constructs an instance with the given actual downstream subscriber.
        /// </summary>
        /// <param name="actual">The downstream subscriber</param>
        public DeferredScalarSubscriber(ISubscriber<R> actual) : base(actual)
        {
        }

        /// <inheritdoc/>
        public abstract void OnComplete();

        /// <inheritdoc/>
        public abstract void OnError(Exception e);

        /// <inheritdoc/>
        public abstract void OnNext(T t);

        /// <inheritdoc/>
        public void OnSubscribe(ISubscription s)
        {
            if (SubscriptionHelper.Validate(ref this.s, s))
            {
                actual.OnSubscribe(this);

                OnStart();
            }
        }

        /// <summary>
        /// Called after this instance has been sent to downstream via OnSubscribe.
        /// </summary>
        protected virtual void OnStart()
        {

        }

        /// <inheritdoc/>
        public override void Cancel()
        {
            base.Cancel();
            s.Cancel();
        }

        /// <summary>
        /// Set the done flag and signal the exception.
        /// </summary>
        /// <param name="ex">The exception to signal</param>
        public override void Error(Exception ex)
        {
            if (Volatile.Read(ref state) == CANCELLED)
            {
                ExceptionHelper.OnErrorDropped(ex);
                return;
            }
            base.Cancel();
            base.Error(ex);
        }

        /// <summary>
        /// Rethrow the exception if fatal, otherwise cancel the subscription
        /// and signal the exception via <see cref="Error(Exception)"/>.
        /// </summary>
        /// <param name="ex">The exception to check and signal</param>
        protected void Fail(Exception ex)
        {
            ExceptionHelper.ThrowIfFatal(ex);
            s.Cancel();
            OnError(ex);
        }
    }
}
