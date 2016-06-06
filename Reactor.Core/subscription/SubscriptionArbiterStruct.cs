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
using System.Runtime.InteropServices;

namespace Reactor.Core.subscription
{
    /// <summary>
    /// Arbitrates the requests between subsequent ISubscriptions.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct SubscriptionArbiterStruct
    {
        ISubscription current;

        long requested;

        ISubscription missedSubscription;

        long missedRequested;

        long missedProduced;

        Pad128 p0;

        int wip;

        Pad120 p1;

        /// <summary>
        /// Clears the current ISubscription with plain access.
        /// Use this to prevent cancelling a contained ISubscription that has terminated.
        /// </summary>
        public void Clear()
        {
            current = null;
        }

        /// <summary>
        /// Atomically set the next ISubscription on this arbiter.
        /// </summary>
        /// <param name="s">The new ISubscription instance.</param>
        public void Set(ISubscription s)
        {
            if (Volatile.Read(ref wip) == 0 && Interlocked.CompareExchange(ref wip, 1, 0) == 0)
            {

                var c = current;

                if (SubscriptionHelper.IsCancelled(c))
                {
                    s?.Cancel();
                }
                else
                {
                    c?.Cancel();

                    current = s;

                    long r = requested;
                    if (r != 0L)
                    {
                        s.Request(r);
                    }
                }

                if (Interlocked.Decrement(ref wip) == 0)
                {
                    return;
                }
            }
            else
            {
                ISubscription c = Interlocked.Exchange(ref missedSubscription, s);
                c?.Cancel();
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }
            }
            Drain();
        }

        /// <summary>
        /// Request the specified amount from the current ISubscription if present
        /// or accumulate it until an ISubscription is set.
        /// </summary>
        /// <param name="n">The request amount, positive (not validated)</param>
        public void Request(long n)
        {
            if (Volatile.Read(ref wip) == 0 && Interlocked.CompareExchange(ref wip, 1, 0) == 0)
            {
                long r = requested;

                if (r != long.MaxValue)
                {
                    requested = BackpressureHelper.AddCap(r, n);
                }

                current?.Request(n);

                if (Interlocked.Decrement(ref wip) == 0)
                {
                    return;
                }
            }
            else
            {
                BackpressureHelper.GetAndAddCap(ref missedRequested, n);
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }
            }
            Drain();
        }

        /// <summary>
        /// Indicate the number of items produced and subtract it from
        /// the current requested amount (if not unbounded).
        /// </summary>
        /// <param name="n">The produced amount, positive (not verified).</param>
        public void Produced(long n)
        {
            if (Volatile.Read(ref wip) == 0 && Interlocked.CompareExchange(ref wip, 1, 0) == 0)
            {
                long r = requested;
                if (r != long.MaxValue)
                {
                    r -= n;
                    if (r < 0L)
                    {
                        ExceptionHelper.OnErrorDropped(new InvalidOperationException("More produced than requested: " + r));
                        r = 0L;
                    }
                    requested = r;
                }

                if (Interlocked.Decrement(ref wip) == 0)
                {
                    return;
                }
            }
            else
            {
                BackpressureHelper.GetAndAddCap(ref missedProduced, n);
                if (Interlocked.Increment(ref wip) != 1)
                {
                    return;
                }
            }
            Drain();
        }

        /// <summary>
        /// Cancel the current any subsequent ISubscription.
        /// </summary>
        public void Cancel()
        {
            Set(SubscriptionHelper.Cancelled);
        }

        void Drain()
        {
            int missed = 1;
            for (;;)
            {
                long mRequested = Volatile.Read(ref missedRequested);

                if (mRequested != 0L)
                {
                    mRequested = Interlocked.Exchange(ref missedRequested, 0L);
                }

                long mProduced = Volatile.Read(ref missedProduced);
                if (mProduced != 0L)
                {
                    mProduced = Interlocked.Exchange(ref missedProduced, 0L);
                }

                long r = requested;

                if (r != long.MaxValue)
                {
                    long u = BackpressureHelper.AddCap(r, mRequested);

                    if (u != long.MaxValue)
                    {
                        long v = u - mProduced;

                        if (v < 0L)
                        {
                            ExceptionHelper.OnErrorDropped(new InvalidOperationException("More produced than requested: " + v));
                            v = 0L;
                        }

                        requested = v;
                        r = v;
                    }
                    else
                    {
                        requested = u;
                        r = u;
                    }
                }


                ISubscription mSubscription = Volatile.Read(ref missedSubscription);
                if (mSubscription != null)
                {
                    mSubscription = Interlocked.Exchange(ref missedSubscription, null);
                }

                var c = current;

                if (SubscriptionHelper.IsCancelled(c))
                {
                    mSubscription?.Cancel();
                }
                else
                {
                    if (mSubscription != null)
                    {
                        current?.Cancel();

                        current = mSubscription;

                        if (r != 0L)
                        {
                            mSubscription.Request(r);
                        }
                    }
                    else
                    {
                        if (mRequested != 0L)
                        {
                            current.Request(mRequested);
                        }
                    }
                }

                missed = QueueDrainHelper.Leave(ref wip, missed);
                if (missed == 0)
                {
                    break;
                }
            }
        }
    }
}
