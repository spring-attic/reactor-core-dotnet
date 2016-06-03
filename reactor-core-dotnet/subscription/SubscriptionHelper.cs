using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core;
using System.Threading;
using Reactor.Core.flow;

namespace Reactor.Core.subscription
{
    /// <summary>
    /// Utility methods for working with Subscriptions.
    /// </summary>
    public static class SubscriptionHelper
    {
        /// <summary>
        /// Verify that the request amount is positive, signal an ArgumentOutOfRangeException
        /// to ReactorCorePlugins.
        /// </summary>
        /// <param name="n">The request amount.</param>
        /// <returns>True if the amount is valid, false otherwise</returns>
        public static bool Validate(long n)
        {
            if (n <= 0)
            {
                ReportInvalidRequest(n);
                return false;
            }
            return true;
        }

        static void ReportInvalidRequest(long n)
        {
            ExceptionHelper.OnErrorDropped(new ArgumentOutOfRangeException("n", n, "request amount must be positive"));
        }

        /// <summary>
        /// Verifies that the target field is null and sets it to the non-null ISubscription provided
        /// or signal an InvalidOperationException if the target field is not null and not the Cancelled
        /// instance. In these cases, the new ISubscription is cancelled.
        /// </summary>
        /// <param name="current">The target field</param>
        /// <param name="s">The new ISubscription instance to set</param>
        /// <returns>True if successful, false if the target field is not empty</returns>
        public static bool Validate(ref ISubscription current, ISubscription s)
        {
            if (s == null)
            {
                ReportSubscriptionNull();
                return false;
            }

            if (current != null)
            {
                s.Cancel();
                if (current != Cancelled)
                {
                    ReportSubscriptionSet();
                }
                return false;
            }
            current = s;
            return true;
        }

        static void ReportSubscriptionSet()
        {
            ExceptionHelper.OnErrorDropped(new InvalidOperationException("ISubscription already set"));
        }

        static void ReportSubscriptionNull()
        {
            ExceptionHelper.OnErrorDropped(new ArgumentNullException("s", "OnSubscribe must be called once with a non-null ISubscriber"));
        }

        /// <summary>
        /// Check if the given reference contains the Cancelled instance.
        /// </summary>
        /// <param name="s">The source field</param>
        /// <returns>True if it contains the Cancelled instance.</returns>
        public static bool IsCancelled(ref ISubscription s)
        {
            return s == Cancelled;
        }

        /// <summary>
        /// Check if the given value is the Cancelled instance.
        /// </summary>
        /// <param name="s">The source field</param>
        /// <returns>True if it is the Cancelled instance.</returns>
        public static bool IsCancelled(ISubscription s)
        {
            return s == Cancelled;
        }

        /// <summary>
        /// Atomically set the given ISubscription on a null target or cancel it if the
        /// target is not empty or contains the Cancelled instance.
        /// </summary>
        /// <param name="current">The target field</param>
        /// <param name="s">The new value</param>
        /// <returns>True if successful</returns>
        public static bool SetOnce(ref ISubscription current, ISubscription s)
        {
            if (s == null)
            {
                ReportSubscriptionNull();
                return false;
            }

            for (;;)
            {
                var a = Volatile.Read(ref current);
                if (a == Cancelled)
                {
                    s.Cancel();
                    return false;
                }
                if (a != null)
                {
                    s.Cancel();
                    ReportSubscriptionSet();
                    return false;
                }
                if (Interlocked.CompareExchange(ref current, s, null) == null)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Atomically replace the ISubscription in the target field or cancel
        /// the new ISubscription if the target contains the Cancelled instance.
        /// </summary>
        /// <param name="current">The target field</param>
        /// <param name="s">The new ISubscription instance</param>
        /// <returns>True if successful</returns>
        public static bool Replace(ref ISubscription current, ISubscription s)
        {
            for (;;)
            {
                var a = Volatile.Read(ref current);
                if (a == Cancelled)
                {
                    s?.Cancel();
                    return false;
                }
                if (Interlocked.CompareExchange(ref current, s, null) == null)
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// Atomically set the ISubscription in the target field, cancelling
        /// the previous value or cancel the new ISubscription if the target 
        /// contains the Cancelled instance.
        /// </summary>
        /// <param name="current">The target field</param>
        /// <param name="s">The new ISubscription instance</param>
        /// <returns>True if successful</returns>
        public static bool Set(ref ISubscription current, ISubscription s)
        {
            for (;;)
            {
                var a = Volatile.Read(ref current);
                if (a == Cancelled)
                {
                    s?.Cancel();
                    return false;
                }
                if (Interlocked.CompareExchange(ref current, s, a) == null)
                {
                    a?.Cancel();
                    return true;
                }
            }
        }

        /// <summary>
        /// Atomically cancel the target field of ISubscription.
        /// </summary>
        /// <param name="current">The target field</param>
        /// <returns>True if the caller thread was the one successfully cancelling the content of the field</returns>
        public static bool Cancel(ref ISubscription current)
        {
            var a = Volatile.Read(ref current);
            if (a != Cancelled)
            {
                a = Interlocked.Exchange(ref current, Cancelled);
                if (a != Cancelled)
                {
                    a?.Cancel();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The singleton instance of a ISubscription denoting a cancelled state.
        /// </summary>
        internal static readonly CancelledSubscription Cancelled = new CancelledSubscription();

        /// <summary>
        /// Class representing a no-op cancelled ISubscription.
        /// </summary>
        internal sealed class CancelledSubscription : ISubscription
        {

            public void Cancel()
            {
                // deliberately ignored
            }

            public void Request(long n)
            {
                // deliberately ignored
            }
        }
    }
}
