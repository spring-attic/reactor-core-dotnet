using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reactor.Core;
using Reactive.Streams;
using Reactor.Core.subscription;

namespace Reactor.Core
{
    /// <summary>
    /// Base interface for parallel stream processing.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public interface IParallelFlux<out T>
    {
        /// <summary>
        /// True if the underlying implementation is ordered.
        /// </summary>
        bool IsOrdered { get; }

        /// <summary>
        /// The parallelism level of this IParallelFlux.
        /// </summary>
        int Parallelism { get; }

        /// <summary>
        /// Subscribes an array of ISubscribers, one for each rail.
        /// </summary>
        /// <param name="subscribers">The array of subscribers, its length must be equal
        /// to the <see cref="Parallelism"/> value.</param>
        void Subscribe(ISubscriber<T>[] subscribers);

    }
    
    /// <summary>
    /// Base class for unordered parallel stream processing.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public abstract class ParallelUnorderedFlux<T> : IParallelFlux<T>
    {
        /// <inheritdoc/>
        public bool IsOrdered { get { return false; } }

        /// <inheritdoc/>
        public abstract int Parallelism { get; }

        /// <inheritdoc/>
        public abstract void Subscribe(ISubscriber<T>[] subscribers);

    }

    /// <summary>
    /// Base class for ordered parallel stream processing.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public abstract class ParallelOrderedFlux<T> : IParallelFlux<T>
    {
        /// <inheritdoc/>
        public bool IsOrdered { get { return true; } }

        /// <inheritdoc/>
        public abstract int Parallelism { get; }

        /// <inheritdoc/>
        public void Subscribe(ISubscriber<T>[] subscribers)
        {
            if (!this.Validate(subscribers))
            {
                return;
            }
        }

        /// <summary>
        /// Subscribe to this ordered parallel stream with an array of ISubscribers
        /// that can consume IOrderedItems.
        /// </summary>
        /// <param name="subscribers">The array of IOrderedItem-receiving ISubscribers.</param>
        public abstract void Subscribe(ISubscriber<IOrderedItem<T>>[] subscribers);
    }

    internal static class ParallelFluxHelper
    {
        /// <summary>
        /// Validate that the parallelism of the IParallelFlux equals to the number of elements in
        /// the ISubscriber array. If not, each ISubscriber is notified with an ArgumentException.
        /// </summary>
        /// <typeparam name="T">The element type of the parallel flux</typeparam>
        /// <typeparam name="U">The element type of the ISubscriber array (could be T or IOrderedItem{T})</typeparam>
        /// <param name="pf">The parent IParallelFlux instance</param>
        /// <param name="subscribers">The ISubscriber array</param>
        /// <returns>True if the subscribers were valid</returns>
        internal static bool Validate<T, U>(this IParallelFlux<T> pf, ISubscriber<U>[] subscribers)
        {
            if (pf.Parallelism != subscribers.Length)
            {
                Exception ex = new ArgumentException("Parallelism(" + pf.Parallelism + ") != Subscriber count (" + subscribers.Length + ")");
                foreach (ISubscriber<U> s in subscribers)
                {
                    EmptySubscription<U>.Error(s, ex);
                }
                return false;
            }
            return true;
        }

    }

    internal sealed class RemoveOrdered<T> : ISubscriber<IOrderedItem<T>>, ISubscription
    {
        readonly ISubscriber<T> actual;

        ISubscription s;

        public RemoveOrdered(ISubscriber<T> actual)
        {
            this.actual = actual;
        }

        public void Cancel()
        {
            s.Cancel();
        }

        public void OnComplete()
        {
            actual.OnComplete();
        }

        public void OnError(Exception e)
        {
            actual.OnError(e);
        }

        public void OnNext(IOrderedItem<T> t)
        {
            actual.OnNext(t.Value);
        }

        public void OnSubscribe(ISubscription s)
        {
            this.s = s;
            actual.OnSubscribe(this);
        }

        public void Request(long n)
        {
            s.Request(n);
        }
    }

}
