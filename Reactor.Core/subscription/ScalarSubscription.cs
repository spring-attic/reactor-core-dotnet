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

namespace Reactor.Core.subscription
{
    /// <summary>
    /// A fuseable subscription that emits a single value on request or poll.
    /// </summary>
    /// <typeparam name="T">The value type</typeparam>
    public sealed class ScalarSubscription<T> : IQueueSubscription<T>
    {
        readonly ISubscriber<T> actual;

        readonly T value;

        int state;

        static readonly int READY = 0;

        static readonly int REQUESTED = 1;

        static readonly int CANCELLED = 2;

        /// <summary>
        /// Constructs a ScalarSubscription with the target ISubscriber and value to
        /// emit on request.
        /// </summary>
        /// <param name="actual">The target ISubscriber</param>
        /// <param name="value">The value to emit</param>
        public ScalarSubscription(ISubscriber<T> actual, T value)
        {
            if (value == null)
            {
                throw new ArgumentNullException("value");
            }
            this.actual = actual;
            this.value = value;
        }

        /// <inheritdoc/>
        public int RequestFusion(int mode)
        {
            if ((mode & FuseableHelper.SYNC) != 0)
            {
                return FuseableHelper.SYNC;
            }
            return FuseableHelper.NONE;
        }

        /// <inheritdoc/>
        public bool Offer(T value)
        {
            return FuseableHelper.DontCallOffer();
        }

        /// <inheritdoc/>
        public bool Poll(out T value)
        {
            int s = state;
            if (s == READY)
            {
                state = REQUESTED;
                value = this.value;
                return true;
            }
            value = default(T);
            return false;
        }

        /// <inheritdoc/>
        public bool IsEmpty()
        {
            return state != READY;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            state = REQUESTED;
        }

        /// <inheritdoc/>
        public void Request(long n)
        {
            if (SubscriptionHelper.Validate(n))
            {
                if (Interlocked.CompareExchange(ref state, REQUESTED, READY) == READY)
                {
                    actual.OnNext(value);
                    if (Volatile.Read(ref state) != CANCELLED)
                    {
                        actual.OnComplete();
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void Cancel()
        {
            Volatile.Write(ref state, CANCELLED);
        }
    }
}
