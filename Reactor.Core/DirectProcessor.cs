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
        DirectSubscription[] subscribers = EMPTY;

        static DirectSubscription[] EMPTY = new DirectSubscription[0];
        static DirectSubscription[] TERMINATED = new DirectSubscription[0];

        Exception error;

        bool Add(DirectSubscription s)
        {
            var a = Volatile.Read(ref subscribers);
            for (;;)
            {
                if (a == TERMINATED)
                {
                    return false;
                }

                int n = a.Length;
                var b = new DirectSubscription[n + 1];
                Array.Copy(a, 0, b, 0, n);
                b[n] = s;

                var c = Interlocked.CompareExchange(ref subscribers, b, a);
                if (c == a)
                {
                    return true;
                }
                else
                {
                    a = c;
                }
            }
        }

        void Remove(DirectSubscription s)
        {
            var a = Volatile.Read(ref subscribers);

            for (;;)
            {
                if (a == EMPTY || a == TERMINATED)
                {
                    return;
                }

                int n = a.Length;
                int j = -1;
                for (int i = 0; i < n; i++)
                {
                    if (a[i] == s)
                    {
                        j = i;
                        break;
                    }
                }

                if (j < 0)
                {
                    break;
                }

                DirectSubscription[] b;

                if (n == 1)
                {
                    b = EMPTY;
                }
                else
                {
                    b = new DirectSubscription[n - 1];
                    Array.Copy(a, 0, b, 0, j);
                    Array.Copy(a, j + 1, b, j, n - j - 1);
                }

                var c = Interlocked.CompareExchange(ref subscribers, b, a);
                if (c == a)
                {
                    return;
                }
                else
                {
                    a = c;
                }
            }
        }

        /// <inheritdoc/>
        public void OnSubscribe(ISubscription s)
        {
            if (Volatile.Read(ref subscribers) == TERMINATED)
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
            foreach (var ds in Volatile.Read(ref subscribers))
            {
                ds.OnNext(t);
            }
        }

        /// <inheritdoc/>
        public void OnError(Exception e)
        {
            if (Volatile.Read(ref subscribers) != TERMINATED)
            {
                error = e;
                foreach (var ds in Interlocked.Exchange(ref subscribers, TERMINATED))
                {
                    ds.OnError(e);
                }
            }
        }

        /// <inheritdoc/>
        public void OnComplete()
        {
            foreach (var ds in Interlocked.Exchange(ref subscribers, TERMINATED))
            {
                ds.OnComplete();
            }
        }

        /// <inheritdoc/>
        public void Subscribe(ISubscriber<T> s)
        {
            DirectSubscription ds = new DirectSubscription(s, this);
            s.OnSubscribe(ds);

            if (Add(ds))
            {
                if (ds.IsCancelled())
                {
                    Remove(ds);
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

        /// <summary>
        /// True if this DirectProcessor has subscribers.
        /// </summary>
        public bool HasSubscribers
        {
            get { return Volatile.Read(ref subscribers).Length != 0; }
        }

        /// <summary>
        /// True if this DirectProcessor has been terminated with an Exception.
        /// </summary>
        /// <seealso cref="Exception"/>
        public bool HasException
        {
            get { return Volatile.Read(ref subscribers) == TERMINATED && error != null; }
        }

        /// <summary>
        /// True if this DirectProcessor has been completed normally.
        /// </summary>
        public bool IsComplete
        {
            get { return Volatile.Read(ref subscribers) == TERMINATED && error == null; }
        }

        /// <summary>
        /// Returns the exception that terminated this DirectProcessor, null otherwise.
        /// </summary>
        public Exception Exception
        {
            get
            {
                if (Volatile.Read(ref subscribers) == TERMINATED)
                {
                    return error;
                }
                return null;
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
                    parent.Remove(this);
                }
            }

            internal bool IsCancelled()
            {
                return Volatile.Read(ref cancelled) != 0;
            }
        }
    }
}
