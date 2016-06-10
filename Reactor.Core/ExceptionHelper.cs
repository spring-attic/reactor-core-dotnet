using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Reactor.Core
{
    /// <summary>
    /// Hosts callbacks to override default behaviors at runtime.
    /// </summary>
    public static class ExceptionHelper
    {
        static bool lockdown;

        static readonly Exception TERMINATED = new Exception("Sequence terminated");

        /// <summary>
        /// Check if the plugins are in lockdown.
        /// </summary>
        /// <returns>True if the plugins are locked down and can't be changed anymore.</returns>
        public static bool IsLockdown()
        {
            return Volatile.Read(ref lockdown);
        }

        /// <summary>
        /// When called, the callbacks can no longer be changed.
        /// </summary>
        public static void Lockdown()
        {
            Volatile.Write(ref lockdown, true);
        }

        static Action<Exception> onErrorHandler;

        /// <summary>
        /// Handler for undeliverable Exceptions (which can not be sent via OnError due to
        /// <see cref="Reactive.Streams.ISubscriber{T}">ISubscriber</see> lifecycle requirements).
        /// </summary>
        public static Action<Exception> OnErrorHandler
        {
            get
            {
                return Volatile.Read(ref onErrorHandler);
            }
            set
            {
                if (!IsLockdown())
                {
                    Volatile.Write(ref onErrorHandler, value);
                }
            }
        }

        /// <summary>
        /// Signal to a global handler if an Exception can't be delivered through the
        /// regular <see cref="Reactive.Streams.ISubscriber{T}.OnError(Exception)"/> method.
        /// </summary>
        /// <param name="e">The Exception that occurred.</param>
        public static void OnErrorDropped(Exception e)
        {
            var a = OnErrorHandler;
            if (a != null)
            {
                a(e);
            } else
            {
                System.Diagnostics.Debug.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// Rethrows an exception if it is considered fatal.
        /// </summary>
        /// <param name="ex">The exception to rethrow</param>
        public static void ThrowIfFatal(Exception ex)
        {
            if (ex is System.OutOfMemoryException)
            {
                throw ex;
            }
            if (ex is System.PlatformNotSupportedException)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Singals the error to the subscriber and sets a done flag if the
        /// Exception is non fatal.
        /// </summary>
        /// <typeparam name="T">The value type of the ISubscriber.</typeparam>
        /// <param name="ex">The exception to signal.</param>
        /// <param name="subscriber">The target ISubscriber</param>
        /// <param name="done">the flag to check if false and set it to true</param>
        public static void ReportError<T>(Exception ex, ISubscriber<T> subscriber, ref bool done)
        {
            ThrowIfFatal(ex);
            if (done)
            {
                OnErrorDropped(ex);
                return;
            }
            done = true;
            subscriber.OnError(ex);
        }
        /// <summary>
        /// Cancels the ISubscription, singals the error to the subscriber and sets a done flag if the
        /// Exception is non fatal.
        /// </summary>
        /// <typeparam name="T">The value type of the ISubscriber.</typeparam>
        /// <param name="ex">The exception to signal.</param>
        /// <param name="subscriber">The target ISubscriber</param>
        /// <param name="done">the flag to check if false and set it to true</param>
        /// <param name="s">The ISubscription to cancel.</param>
        public static void ReportError<T>(Exception ex, ISubscriber<T> subscriber, ref bool done, ISubscription s)
        {
            ThrowIfFatal(ex);
            s.Cancel();
            if (done)
            {
                OnErrorDropped(ex);
                return;
            }
            done = true;
            subscriber.OnError(ex);
        }

        /// <summary>
        /// Throw if a fatal exception or else drop it.
        /// </summary>
        /// <param name="ex">The exception.</param>
        public static void ThrowOrDrop(Exception ex)
        {
            ThrowIfFatal(ex);
            OnErrorDropped(ex);
        }

        /// <summary>
        /// Atomically sets the given Exception on the target or combines the existing
        /// Exception with the provided through an AggregateException.
        /// </summary>
        /// <param name="error">The target field.</param>
        /// <param name="ex">The new exception to add</param>
        /// <returns>True if successful, false if the target field already contained the terminated instance.</returns>
        public static bool AddError(ref Exception error, Exception ex)
        {
            var e = Volatile.Read(ref error);
            for (;;)
            {
                if (e == TERMINATED)
                {
                    return false;
                }

                Exception u;
                if (e == null)
                {
                    u = ex;
                }
                else
                {
                    u = new AggregateException(e, ex);
                }

                var f = Interlocked.CompareExchange(ref error, u, e);
                if (f == e)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Atomically swaps in the terminated Exception instance if not already done
        /// and returns the previous Exception the target field held.
        /// </summary>
        /// <param name="error">The target field</param>
        /// <returns>The previous Exception instance or the terminated instance.</returns>
        public static Exception Terminate(ref Exception error)
        {
            var e = Volatile.Read(ref error);
            if (e != TERMINATED)
            {
                e = Interlocked.Exchange(ref error, TERMINATED);
            }
            return e;
        }
    }
}
