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

namespace Reactor.Core.util
{
    /// <summary>
    /// Helper methods for working with backpressure request amounts.
    /// </summary>
    public static class BackpressureHelper
    {
        /// <summary>
        /// Add two positive integers and cap the sum at long.MaxValue.
        /// </summary>
        /// <param name="a">The first value, non-negative (not verified)</param>
        /// <param name="b">The second value, non-negative (not verified)</param>
        /// <returns></returns>
        public static long AddCap(long a, long b)
        {
            long u = a + b;
            if (u < 0)
            {
                return long.MaxValue;
            }
            return u;
        }

        /// <summary>
        /// Multiplies two long values and caps the result at long.MaxValue.
        /// </summary>
        /// <param name="a">The first value, non-negative (not verified).</param>
        /// <param name="b">The second value, non-negative (not verified).</param>
        /// <returns>The product of <paramref name="a"/> and <paramref name="b"/> capped at <see cref="long.MaxValue"/></returns>
        public static long MultiplyCap(long a, long b)
        {
            long c = a * b;

            if (((a | b) >> 31) != 0L)
            {
                if (b != 0L && c / b != a)
                {
                    return long.MaxValue;
                }
            }

            return c;
        }

        /// <summary>
        /// Atomically add the given number to the requested field and
        /// cap the sum at long.MaxValue.
        /// </summary>
        /// <param name="requested">The target requested field</param>
        /// <param name="n">The request amount. Not validated</param>
        /// <returns>The previous value of the requested field</returns>
        public static long GetAndAddCap(ref long requested, long n)
        {
            long r = Volatile.Read(ref requested);
            for (;;)
            {
                if (r == long.MaxValue)
                {
                    return long.MaxValue;
                }
                long u = AddCap(r, n);
                long v = Interlocked.CompareExchange(ref requested, u, r);
                if (v == r)
                {
                    return v;
                }
                else
                {
                    r = v;
                }
            }
        }

        static void ReportMoreProduced(long n)
        {
            ExceptionHelper.OnErrorDropped(new InvalidOperationException("More produced than requested: " + n));
        }

        /// <summary>
        /// Atomically decrement the target requested field by the given positive number
        /// and return the new value.
        /// </summary>
        /// <param name="requested">The target field</param>
        /// <param name="n">The value to subtract, positive (not verified)</param>
        /// <returns>The new field value after the subtraction.</returns>
        public static long Produced(ref long requested, long n)
        {
            long r = Volatile.Read(ref requested);
            for (;;)
            {
                if (r == long.MaxValue)
                {
                    return long.MaxValue;
                }
                long u = r - n;
                if (u < 0)
                {
                    ReportMoreProduced(u);
                    u = 0;
                }
                long v = Interlocked.CompareExchange(ref requested, u, r);
                if (v == r)
                {
                    return u;
                } else
                {
                    r = v;
                }
            }
        }

        /// <summary>
        /// Atomically set the new ISubscription once on the current field and request
        /// any accumulated value.
        /// </summary>
        /// <param name="current">The current ISubscription field</param>
        /// <param name="requested">The requested amount field</param>
        /// <param name="s">The new ISubscription to set once</param>
        public static void DeferredSetOnce(ref ISubscription current, ref long requested, ISubscription s)
        {
            if (SubscriptionHelper.SetOnce(ref current, s))
            {
                long r = Interlocked.Exchange(ref requested, 0L);
                if (r != 0L)
                {
                    s.Request(r);
                }
            }
        }

        /// <summary>
        /// Accumulate the request amounts until the current field is null or
        /// request directly from the ISubscription.
        /// </summary>
        /// <param name="current">The current ISubscription field</param>
        /// <param name="requested">The requested amount field</param>
        /// <param name="n">The requested amount to request directly or accumulate until the current field is not null. Validated</param>
        public static void DeferredRequest(ref ISubscription current, ref long requested, long n)
        {
            var a = Volatile.Read(ref current);
            if (a != null)
            {
                a.Request(n);
            }
            else
            {
                if (SubscriptionHelper.Validate(n))
                {
                    GetAndAddCap(ref requested, n);
                    a = Volatile.Read(ref current);
                    if (a != null)
                    {
                        long r = Interlocked.Exchange(ref requested, 0L);
                        if (r != 0L)
                        {
                            a.Request(r);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns an exception instance indicating the lack of requests.
        /// </summary>
        /// <param name="message">The optional message</param>
        /// <returns>The Exception instance.</returns>
        public static Exception MissingBackpressureException(string message = "Could not signal value due to lack of requests.")
        {
            if (message.Length == 0)
            {
                return new InvalidOperationException("Please consider applying one of the OnBackpressureXXX operators.");
            }
            return new InvalidOperationException(message + " Please consider applying one of the OnBackpressureXXX operators.");
        }

        /// <summary>
        /// Validates and atomically adds the amount to the requested field.
        /// </summary>
        /// <param name="requested">The target requested field.</param>
        /// <param name="n">The amount to add, positive (validated).</param>
        /// <returns>The amount of the requested field before the addition.</returns>
        public static long ValidateAndAddCap(ref long requested, long n)
        {
            if (SubscriptionHelper.Validate(n))
            {
                return GetAndAddCap(ref requested, n);
            }
            return Volatile.Read(ref requested);
        }

        /// <summary>
        /// Atomically add to the requested amount and drain the queue
        /// if in post-complete state.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="requested">The requested field.</param>
        /// <param name="n">The request amount, positive (not validated).</param>
        /// <param name="s">The target ISubscriber</param>
        /// <param name="queue">The queue to drain.</param>
        /// <param name="cancelled">The cancelled field</param>
        /// <returns>True if in post-complete mode.</returns>
        public static bool PostCompleteRequest<T>(ref long requested, long n, ISubscriber<T> s, IQueue<T> queue, ref bool cancelled)
        {
            long r = Volatile.Read(ref requested);
            for (;;)
            {
                long c = r & COMPLETE_MASK;
                long u = r & REQUESTED_MASK;

                long v = AddCap(u, n) | c;

                long w = Interlocked.CompareExchange(ref requested, v, r);
                if (w == r)
                {
                    if (r == COMPLETE_MASK)
                    {
                        PostCompleteDrain(ref requested, s, queue, ref cancelled);
                    }
                    return c != 0L;
                }
                else
                {
                    r = w;
                }

            }
        }

        static long COMPLETE_MASK = long.MinValue;
        static long REQUESTED_MASK = long.MaxValue;

        /// <summary>
        /// Atomically switches to post-complete mode and drains the queue.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="requested">The requested field.</param>
        /// <param name="s">The ISubscriber</param>
        /// <param name="queue">The queue</param>
        /// <param name="cancelled">The cancelled field.</param>
        public static void PostComplete<T>(ref long requested, ISubscriber<T> s, IQueue<T> queue, ref bool cancelled)
        {
            long r = Volatile.Read(ref requested);
            for (;;)
            {
                if ((r & COMPLETE_MASK) != 0)
                {
                    return;
                }
                long u = r | COMPLETE_MASK;
                long v = Interlocked.CompareExchange(ref requested, u, r);
                if (v == r)
                {
                    if (r != 0L)
                    {
                        PostCompleteDrain(ref requested, s, queue, ref cancelled);
                    }
                    return;
                }
                else
                {
                    r = v;
                }
            }
        }

        static void PostCompleteDrain<T>(ref long requested, ISubscriber<T> s, IQueue<T> queue, ref bool cancelled)
        {
            long r = Volatile.Read(ref requested);
            long e = COMPLETE_MASK;
            for (;;)
            {
                while (e != r)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        queue.Clear();
                        return;
                    }

                    T t;

                    if (queue.Poll(out t))
                    {
                        s.OnNext(t);
                        e++;
                    }
                    else
                    {
                        s.OnComplete();
                        return;
                    }
                }

                if (e == r)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        queue.Clear();
                        return;
                    }

                    if (queue.IsEmpty())
                    {
                        s.OnComplete();
                        return;
                    }
                }

                r = Volatile.Read(ref requested);
                if (r == e)
                {
                    r = Interlocked.Add(ref requested, -(e & REQUESTED_MASK));
                    if (r == COMPLETE_MASK)
                    {
                        break;
                    }
                    e = COMPLETE_MASK;
                }
            }
        }
    }
}
