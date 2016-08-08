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

namespace Reactor.Core.util
{
    /// <summary>
    /// Performs half-serialization of events, that is, only one
    /// source calls OnNext but any other source may call OnError and
    /// OnComplete concurrently.
    /// </summary>
    internal struct HalfSerializerStruct
    {
        /// <summary>
        /// The work-in-progress counter.
        /// </summary>
        int wip;

        /// <summary>
        /// The exception to terminate with or null
        /// </summary>
        Exception error;

        /// <summary>
        /// Signal an OnNext value and terminate if there was a concurrent OnError
        /// or OnComplete call. Should be called from one thread at a time.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="actual">The target ISubscriber</param>
        /// <param name="value">The value to signal</param>
        internal void OnNext<T>(ISubscriber<T> actual, T value)
        {
            if (Volatile.Read(ref wip) == 0 && Interlocked.CompareExchange(ref wip, 1, 0) == 0)
            {
                actual.OnNext(value);
                if (Interlocked.CompareExchange(ref wip, 0, 1) != 1)
                {
                    var ex = ExceptionHelper.Terminate(ref error);
                    if (ex != null)
                    {
                        actual.OnError(ex);
                    }
                    else
                    {
                        actual.OnComplete();
                    }
                }
            }
        }

        /// <summary>
        /// Signal an OnNext value conditionally and terminate if 
        /// there was a concurrent OnError or OnComplete call.
        /// Should be called from one thread at a time.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="actual">The target ISubscriber</param>
        /// <param name="value">The value to signal</param>
        /// <returns>What the TryOnNext returns or false if terminated</returns>
        internal bool TryOnNext<T>(IConditionalSubscriber<T> actual, T value)
        {
            if (Volatile.Read(ref wip) == 0 && Interlocked.CompareExchange(ref wip, 1, 0) == 0)
            {
                bool b = actual.TryOnNext(value);
                if (Interlocked.CompareExchange(ref wip, 0, 1) != 1)
                {
                    var ex = ExceptionHelper.Terminate(ref error);
                    if (ex != null)
                    {
                        actual.OnError(ex);
                    }
                    else
                    {
                        actual.OnComplete();
                    }
                    return false;
                }
                return b;
            }
            return false;
        }

        internal void OnError<T>(ISubscriber<T> actual, Exception e)
        {
            if (Volatile.Read(ref wip) == 0 && Interlocked.CompareExchange(ref wip, 1, 0) == 0)
            {
                actual.OnError(e);
            }
            else
            {
                if (ExceptionHelper.AddError(ref this.error, e))
                {
                    if (Interlocked.Increment(ref wip) == 1)
                    {
                        e = ExceptionHelper.Terminate(ref this.error);
                        actual.OnError(e);
                    }
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

        }

        internal void OnComplete<T>(ISubscriber<T> actual)
        {
            if (Volatile.Read(ref wip) == 0 && Interlocked.CompareExchange(ref wip, 1, 0) == 0)
            {
                actual.OnComplete();
            }
            else
            {
                if (Interlocked.Increment(ref wip) == 1)
                {
                    var e = ExceptionHelper.Terminate(ref this.error);
                    if (e != null)
                    {
                        actual.OnError(e);
                    }
                    else
                    {
                        actual.OnComplete();
                    }
                }
            }
        }
    }
}
