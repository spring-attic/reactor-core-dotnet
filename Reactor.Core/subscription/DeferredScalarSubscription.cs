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
    /// Can hold onto a single value that may appear later
    /// and emits it on request.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public class DeferredScalarSubscription<T> : IQueueSubscription<T>
    {
        /// <summary>
        /// The actual downstream subscriber.
        /// </summary>
        protected readonly ISubscriber<T> actual;

        /// <summary>
        /// The current state.
        /// </summary>
        protected int state;

        static readonly int NO_REQUEST_NO_VALUE = 0;
        static readonly int NO_REQUEST_HAS_VALUE = 1;
        static readonly int HAS_REQUEST_NO_VALUE = 2;
        static readonly int HAS_REQUEST_HAS_VALUE = 3;
        /// <summary>
        /// Indicates a cancelled state.
        /// </summary>
        protected static readonly int CANCELLED = 4;

        int fusionState;

        static readonly int EMPTY = 1;
        static readonly int HAS_VALUE = 2;
        static readonly int COMPLETE = 3;

        /// <summary>
        /// The value storage.
        /// </summary>
        protected T value;

        /// <summary>
        /// Constructs a DeferredScalarSubscription with the target ISubscriber.
        /// </summary>
        /// <param name="actual">The ISubscriber to send signals to.</param>
        public DeferredScalarSubscription(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        /// <summary>
        /// Signal an exception to the downstream ISubscriber.
        /// </summary>
        /// <param name="ex">The exception to signal</param>
        public virtual void Error(Exception ex)
        {
            actual.OnError(ex);
        }

        /// <summary>
        /// Signal a valueless completion to the downsream ISubscriber.
        /// </summary>
        public virtual void Complete()
        {
            actual.OnComplete();
        }

        /// <summary>
        /// Complete with the single value and emit it if
        /// there is request for it.
        /// This should be called at most once.
        /// </summary>
        /// <param name="v">The value to signal</param>
        public void Complete(T v)
        {
            for (;;)
            {
                int s = Volatile.Read(ref state);
                if (s == CANCELLED || s == NO_REQUEST_HAS_VALUE || s == HAS_REQUEST_HAS_VALUE)
                {
                    return;
                }
                if (s == HAS_REQUEST_NO_VALUE)
                {
                    if (fusionState == EMPTY)
                    {
                        value = v;
                        fusionState = HAS_VALUE;
                    }

                    actual.OnNext(v);

                    if (Volatile.Read(ref state) != CANCELLED)
                    {
                        actual.OnComplete();
                    }

                    return;
                }
                value = v;
                if (Interlocked.CompareExchange(ref state, NO_REQUEST_HAS_VALUE, NO_REQUEST_NO_VALUE) == HAS_REQUEST_HAS_VALUE)
                {
                    break;
                }
            }
        }

        /// <inheritdoc/>
        public void Request(long n)
        {
            if (!SubscriptionHelper.Validate(n))
            {
                return;
            }
            for (;;)
            {
                int s = Volatile.Read(ref state);
                if (s == CANCELLED || s == HAS_REQUEST_NO_VALUE || s == HAS_REQUEST_HAS_VALUE)
                {
                    return;
                }
                if (s == NO_REQUEST_HAS_VALUE)
                {
                    if (Interlocked.CompareExchange(ref state, HAS_REQUEST_HAS_VALUE, NO_REQUEST_HAS_VALUE) == NO_REQUEST_HAS_VALUE)
                    {
                        T v = value;

                        if (fusionState == EMPTY)
                        {
                            fusionState = HAS_VALUE;
                        }

                        actual.OnNext(v);

                        if (Volatile.Read(ref state) != CANCELLED)
                        {
                            actual.OnComplete();
                        }

                        return;
                    }
                    return;
                }
                if (Interlocked.CompareExchange(ref state, HAS_REQUEST_NO_VALUE, NO_REQUEST_NO_VALUE) == NO_REQUEST_NO_VALUE)
                {
                    break;
                }
            }
        }


        /// <inheritdoc/>
        public int RequestFusion(int mode)
        {
            int m = mode & FuseableHelper.ASYNC;
            if (m != 0)
            {
                fusionState = EMPTY;
            }
            return m;
        }

        /// <inheritdoc/>
        public bool Offer(T value)
        {
            return FuseableHelper.DontCallOffer();
        }

        /// <inheritdoc/>
        public bool Poll(out T value)
        {
            if (fusionState == HAS_VALUE)
            {
                fusionState = COMPLETE;
                value = this.value;
                return true;
            }
            value = default(T);
            return false;
        }

        /// <inheritdoc/>
        public bool IsEmpty()
        {
            return fusionState != HAS_VALUE;
        }

        /// <inheritdoc/>
        public void Clear()
        {
            value = default(T);
            fusionState = COMPLETE;
        }

        /// <inheritdoc/>
        public virtual void Cancel()
        {
            Volatile.Write(ref state, CANCELLED);
        }
    }
}
