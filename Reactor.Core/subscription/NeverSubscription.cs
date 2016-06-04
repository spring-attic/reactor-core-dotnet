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
    public sealed class NeverSubscription<T> : IQueueSubscription<T>
    {

        private NeverSubscription()
        {

        }

        private static readonly NeverSubscription<T> INSTANCE = new NeverSubscription<T>();

        /// <summary>
        /// Returns the singleton instance of the EmptySubscription class.
        /// </summary>
        public static NeverSubscription<T> Instance { get { return INSTANCE; } }

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
            // deliberately ignored
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
            if ((mode & FuseableHelper.ASYNC) != 0)
            {
                return FuseableHelper.ASYNC;
            }
            return FuseableHelper.NONE;
        }
    }
}
