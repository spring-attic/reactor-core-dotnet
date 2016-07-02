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

namespace Reactor.Core
{
    /// <summary>
    /// An IProcessor with same input and output type that dispatches signals to ISubscribers.
    /// Consumers unable to keep up will receive an InvalidOperationException.
    /// </summary>
    /// <typeparam name="T">The input and output value type</typeparam>
    public sealed class DirectProcessor<T> : IFluxProcessor<T>
    {

        TrackingArray<DirectSubscription> subscribers;

        Exception error;

        /// <inheritDoc/>
        public bool HasSubscribers
        {
            get
            {
                return subscribers.Array().Length != 0;
            }
        }

        /// <inheritDoc/>
        public bool IsComplete
        {
            get
            {
                return subscribers.IsTerminated() && error == null;
            }
        }

        /// <inheritDoc/>
        public bool HasError
        {
            get
            {
                return subscribers.IsTerminated() && error != null;
            }
        }

        /// <inheritDoc/>
        public Exception Error
        {
            get
            {
                if (subscribers.IsTerminated())
                {
                    return error;
                }
                return null;
            }
        }

        /// <summary>
        /// Constructs a fresh DirectProcessor.
        /// </summary>
        public DirectProcessor()
        {
            subscribers.Init();
        }

        /// <inheritdoc/>
        public void OnSubscribe(ISubscription s)
        {
            if (subscribers.IsTerminated())
            {
                s.Cancel();
            }
            else
            {
                s.Request(long.MaxValue);
            }
        }

        /// <inheritdoc/>
        public void OnNext(T t)
        {
            foreach (var ds in subscribers.Array())
            {
                ds.OnNext(t);
            }
        }

        /// <inheritdoc/>
        public void OnError(Exception e)
        {
            if (!subscribers.IsTerminated())
            {
                error = e;
                foreach (var ds in subscribers.Terminate())
                {
                    ds.OnError(e);
                }
            }
        }

        /// <inheritdoc/>
        public void OnComplete()
        {
            foreach (var ds in subscribers.Terminate())
            {
                ds.OnComplete();
            }
        }

        /// <inheritdoc/>
        public void Subscribe(ISubscriber<T> s)
        {
            DirectSubscription ds = new DirectSubscription(s, this);
            s.OnSubscribe(ds);

            if (subscribers.Add(ds))
            {
                if (ds.IsCancelled())
                {
                    subscribers.Remove(ds);
                }
            }
            else
            {
                Exception ex = error;
                if (ex != null)
                {
                    s.OnError(ex);
                }
                else
                {
                    s.OnComplete();
                }
            }
        }

        sealed class DirectSubscription : ISubscription
        {
            readonly ISubscriber<T> actual;

            readonly DirectProcessor<T> parent;

            long requested;

            long produced;

            int cancelled;

            internal DirectSubscription(ISubscriber<T> actual, DirectProcessor<T> parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            internal void OnNext(T value)
            {
                long r = Volatile.Read(ref requested);
                long p = produced;
                if (r != p)
                {
                    produced = p + 1;
                    actual.OnNext(value);
                }
                else
                {
                    Cancel();
                    actual.OnError(BackpressureHelper.MissingBackpressureException());
                }
            }

            internal void OnError(Exception e)
            {
                actual.OnError(e);
            }

            internal void OnComplete()
            {
                actual.OnComplete();
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                }
            }

            public void Cancel()
            {
                if (Interlocked.CompareExchange(ref cancelled, 1, 0) == 0)
                {
                    parent.subscribers.Remove(this);
                }
            }

            internal bool IsCancelled()
            {
                return Volatile.Read(ref cancelled) != 0;
            }
        }
    }
}
