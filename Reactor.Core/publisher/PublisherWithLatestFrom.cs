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

namespace Reactor.Core.publisher
{
    sealed class PublisherWithLatestFrom<T, R> : IFlux<R>
    {
        readonly IPublisher<T> source;

        readonly IPublisher<T>[] others;

        readonly Func<T[], R> combiner;

        internal PublisherWithLatestFrom(IPublisher<T> source, IPublisher<T>[] others, Func<T[], R> combiner)
        {
            this.source = source;
            this.others = others;
            this.combiner = combiner;
        }

        public void Subscribe(ISubscriber<R> s)
        {
            WithLatestFromHelper parent;
            if (s is IConditionalSubscriber<R>)
            {
                parent = new WithLatestFromConditionalSubscriber((IConditionalSubscriber<R>)s, others.Length, combiner);
            }
            else
            {
                parent = new WithLatestFromSubscriber(s, others.Length, combiner);
            }

            s.OnSubscribe(parent);

            parent.Subscribe(others);

            source.Subscribe(parent);
        }

        interface WithLatestFromHelper : IConditionalSubscriber<T>, ISubscription
        {
            void InnerNext(int index, T value);

            void InnerError(Exception ex);

            void InnerComplete();

            void InnerHasValue();

            void Subscribe(IPublisher<T>[] others);
        }

        abstract class BaseWithLatestFomrSubscriber : WithLatestFromHelper
        {
            protected readonly IDisposable[] subscribers;

            protected readonly Node[] values;

            protected readonly Func<T[], R> combiner;

            protected readonly int n;

            protected int ready;

            protected ISubscription s;

            long requested;

            protected HalfSerializerStruct serializer;

            internal BaseWithLatestFomrSubscriber(int n, Func<T[], R> combiner)
            {
                var s = new IDisposable[n];
                for (int i = 0; i < n; i++)
                {
                    s[i] = new InnerSubscriber(this, i);
                }
                this.subscribers = s;
                this.values = new Node[n];
                this.n = n;
                this.combiner = combiner;
            }

            public void Subscribe(IPublisher<T>[] others)
            {
                var a = subscribers;
                int n = this.n;
                for (int i = 0; i < n; i++)
                {
                    if (SubscriptionHelper.IsCancelled(ref s))
                    {
                        return;
                    }
                    others[i].Subscribe((InnerSubscriber)a[i]);
                }
            }

            public void OnSubscribe(ISubscription s)
            {
                BackpressureHelper.DeferredSetOnce(ref this.s, ref requested, s);
            }

            public void Request(long n)
            {
                BackpressureHelper.DeferredRequest(ref this.s, ref requested, n);
            }

            public void Cancel()
            {
                SubscriptionHelper.Cancel(ref s);
                CancelOthers();
            }

            protected void CancelOthers()
            {
                var array = subscribers;
                int n = this.n;
                for (int i = 0; i < n; i++)
                {
                    DisposableHelper.Dispose(ref array[i]);
                }
            }

            public void InnerNext(int index, T value)
            {
                Interlocked.Exchange(ref values[index], new Node(value));
            }

            public void InnerHasValue()
            {
                Interlocked.Increment(ref ready);
            }

            public void OnNext(T t)
            {
                if (TryOnNext(t))
                {
                    s.Request(1);
                }
            }

            public abstract void InnerError(Exception ex);

            public abstract void InnerComplete();

            public abstract void OnError(Exception e);

            public abstract void OnComplete();

            public abstract bool TryNext(R v);

            public bool TryOnNext(T t)
            {
                var values = this.values;
                int n = this.n;
                if (n == Volatile.Read(ref ready))
                {
                    T[] a = new T[n + 1];
                    a[0] = t;
                    for (int i = 0; i < n; i++)
                    {
                        a[i + 1] = Volatile.Read(ref values[i]).value;
                    }

                    R v;

                    try
                    {
                        v = combiner(a);
                    }
                    catch (Exception ex)
                    {
                        ExceptionHelper.ThrowIfFatal(ex);
                        InnerError(ex);
                        return false;
                    }

                    return TryNext(v);
                }
                return false;
            }
        }

        sealed class WithLatestFromSubscriber : BaseWithLatestFomrSubscriber
        {
            readonly ISubscriber<R> actual;

            internal WithLatestFromSubscriber(ISubscriber<R> actual, int n, Func<T[], R> combiner)
                : base(n, combiner)
            {
                this.actual = actual;
            }

            public override void OnError(Exception e)
            {
                CancelOthers();
                serializer.OnError(actual, e);
            }

            public override void OnComplete()
            {
                CancelOthers();
                serializer.OnComplete(actual);
            }

            public override void InnerError(Exception ex)
            {
                Cancel();
                serializer.OnError(actual, ex);
            }

            public override void InnerComplete()
            {
                int n = this.n;
                if (n != Volatile.Read(ref ready))
                {
                    Cancel();
                    serializer.OnComplete(actual);
                }
            }

            public override bool TryNext(R v)
            {
                serializer.OnNext(actual, v);
                return true;
            }
        }

        sealed class WithLatestFromConditionalSubscriber : BaseWithLatestFomrSubscriber
        {
            readonly IConditionalSubscriber<R> actual;

            internal WithLatestFromConditionalSubscriber(IConditionalSubscriber<R> actual, int n, Func<T[], R> combiner)
                : base(n, combiner)
            {
                this.actual = actual;
            }

            public override void OnError(Exception e)
            {
                CancelOthers();
                serializer.OnError(actual, e);
            }

            public override void OnComplete()
            {
                CancelOthers();
                serializer.OnComplete(actual);
            }

            public override void InnerError(Exception ex)
            {
                Cancel();
                serializer.OnError(actual, ex);
            }

            public override void InnerComplete()
            {
                int n = this.n;
                if (n != Volatile.Read(ref ready))
                {
                    Cancel();
                    serializer.OnComplete(actual);
                }
            }

            public override bool TryNext(R v)
            {
                return serializer.TryOnNext(actual, v);
            }
        }

        sealed class Node
        {
            internal readonly T value;

            internal Node(T value)
            {
                this.value = value;
            }
        }

        sealed class InnerSubscriber : ISubscriber<T>, IDisposable
        {
            readonly WithLatestFromHelper parent;

            readonly int index;

            ISubscription s;

            bool once;

            internal InnerSubscriber(WithLatestFromHelper parent, int index)
            {
                this.parent = parent;
                this.index = index;
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    s.Request(long.MaxValue);
                }
            }

            public void OnNext(T t)
            {
                parent.InnerNext(index, t);
                if (!once)
                {
                    once = true;
                    parent.InnerHasValue();
                }
            }

            public void OnError(Exception e)
            {
                parent.InnerError(e);
            }

            public void OnComplete()
            {
                parent.InnerComplete();
            }

            public void Dispose()
            {
                SubscriptionHelper.Cancel(ref s);
            }
        }
    }
}
