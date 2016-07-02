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

namespace Reactor.Core.subscriber
{
    /// <summary>
    /// Base class for operators that want to signal one extra value after the 
    /// upstream has completed.
    /// Call <seealso cref="Complete(R)"/> to signal the post-complete value.
    /// </summary>
    /// <typeparam name="T">The input value type.</typeparam>
    /// <typeparam name="R">The output value type</typeparam>
    internal abstract class BasicSinglePostCompleteSubscriber<T, R> : BasicSubscriber<T, R>, IQueue<R>
    {
        R last;

        bool hasValue;

        long requested;

        bool cancelled;

        /// <summary>
        /// Tracks the amount produced before completion.
        /// </summary>
        protected long produced;

        public BasicSinglePostCompleteSubscriber(ISubscriber<R> actual) : base(actual)
        {
        }

        /// <summary>
        /// Prepares the value to be the post-complete value.
        /// </summary>
        /// <param name="value">The value to signal post-complete.</param>
        public void Complete(R value)
        {
            long p = produced;
            if (p != 0L)
            {
                Produced(p);
            }
            last = value;
            hasValue = true;
            BackpressureHelper.PostComplete(ref requested, actual, this, ref cancelled);
        }

        public sealed override void Request(long n)
        {
            if (SubscriptionHelper.Validate(n))
            {
                if (!BackpressureHelper.PostCompleteRequest(ref requested, n, actual, this, ref cancelled))
                {
                    s.Request(n);
                }
            }
        }

        /// <summary>
        /// Atomically subtract the given amount from the requested amount.
        /// </summary>
        /// <param name="n">The value to subtract, positive (not verified)</param>
        protected void Produced(long n)
        {
            Interlocked.Add(ref requested, -n);
        }

        public bool Offer(R value)
        {
            return FuseableHelper.DontCallOffer();
        }

        public bool Poll(out R value)
        {
            if (hasValue)
            {
                hasValue = false;
                value = last;
                last = default(R);
                return true;
            }
            value = default(R);
            return false;
        }

        public bool IsEmpty()
        {
            return !hasValue;
        }

        public void Clear()
        {
            last = default(R);
            hasValue = false;
        }

        public sealed override void Cancel()
        {
            Volatile.Write(ref cancelled, true);
            base.Cancel();
        }
    }
}
