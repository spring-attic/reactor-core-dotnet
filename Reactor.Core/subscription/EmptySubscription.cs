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
    /// Represents an empty subscription that ignores requests and cancellation.
    /// </summary>
    /// <typeparam name="T">The value type (no value is emitted)</typeparam>
    public sealed class EmptySubscription<T> : IQueueSubscription<T>
    {

        private EmptySubscription()
        {

        }

        private static readonly EmptySubscription<T> INSTANCE = new EmptySubscription<T>();

        /// <summary>
        /// Returns the singleton instance of the EmptySubscription class.
        /// </summary>
        public static EmptySubscription<T> Instance { get { return INSTANCE; } }

        /// <summary>
        /// Sets the empty instance on the ISubscriber and calls OnError with the Exception.
        /// </summary>
        /// <param name="s">The target ISubscriber</param>
        /// <param name="ex">The exception to send</param>
        public static void Error(ISubscriber<T> s, Exception ex)
        {
            s.OnSubscribe(Instance);
            s.OnError(ex);
        }

        /// <summary>
        /// Sets the empty instance on the ISubscriber and calls OnComplete.
        /// </summary>
        /// <param name="s">The target ISubscriber</param>
        public static void Complete(ISubscriber<T> s)
        {
            s.OnSubscribe(Instance);
            s.OnComplete();
        }

        /// <inheritdoc />
        public void Cancel()
        {
            // deliberately ignored
        }

        /// <inheritdoc />
        public void Clear()
        {
            // deliberately ignored
        }

        /// <inheritdoc />
        public bool IsEmpty()
        {
            return true;
        }

        /// <inheritdoc />
        public bool Offer(T value)
        {
            return FuseableHelper.DontCallOffer();
        }

        /// <inheritdoc />
        public bool Poll(out T value)
        {
            value = default(T);
            return false;
        }

        /// <inheritdoc />
        public void Request(long n)
        {
            // deliberately ignored
        }

        /// <inheritdoc />
        public int RequestFusion(int mode)
        {
            return mode & FuseableHelper.ASYNC;
        }
    }
}
