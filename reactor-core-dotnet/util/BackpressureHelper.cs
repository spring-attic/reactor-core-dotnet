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
        /// Atomically add the given number to the requested field and
        /// cap the sum at long.MaxValue.
        /// </summary>
        /// <param name="requested">The target requested field</param>
        /// <param name="n">The request amount.</param>
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
    }
}
