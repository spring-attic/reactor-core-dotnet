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
using System.Runtime.InteropServices;

namespace Reactor.Core.publisher
{
    sealed class PublisherAmb<T> : IFlux<T>
    {
        readonly IPublisher<T>[] sources;

        readonly IEnumerable<IPublisher<T>> sourcesEnumerable;

        internal PublisherAmb(IPublisher<T>[] sources, IEnumerable<IPublisher<T>> sourcesEnumerable)
        {
            this.sources = sources;
            this.sourcesEnumerable = sourcesEnumerable;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            int n;
            IPublisher<T>[] a;

            if (!MultiSourceHelper.ToArray(sources, sourcesEnumerable, s, out n, out a))
            {
                return;
            }

            if (n == 0)
            {
                EmptySubscription<T>.Complete(s);
                return;
            }
            else
            if (n == 1)
            {
                a[0].Subscribe(s);
                return;
            }

            if (s is IConditionalSubscriber<T>)
            {
                AmbConditionalSubscription parent = new AmbConditionalSubscription((IConditionalSubscriber<T>)s, n);

                s.OnSubscribe(parent);

                parent.Subscribe(a, n);
            }
            else
            {
                AmbSubscription parent = new AmbSubscription(s, n);

                s.OnSubscribe(parent);

                parent.Subscribe(a, n);
            }

        }

        internal IFlux<T> AmbWith(IPublisher<T> other)
        {
            if (sources == null)
            {
                return null;
            }

            IPublisher<T>[] a = MultiSourceHelper.AppendLast(this.sources, other);

            return new PublisherAmb<T>(a, null);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class AmbSubscription : ISubscription
        {
            readonly AmbInnerSubscriber[] subscribers;

            Pad112 p0;

            int winner;

            bool cancelled;

            Pad112 p1;

            internal AmbSubscription(ISubscriber<T> actual, int n)
            {
                var a = new AmbInnerSubscriber[n];

                for (int i = 0; i < n; i++)
                {
                    a[i] = new AmbInnerSubscriber(this, actual, i);
                }

                winner = -1;
                subscribers = a;
            }

            internal void Subscribe(IPublisher<T>[] sources, int n)
            {
                var a = subscribers;
                for (int i = 0; i < n; i++)
                {
                    if (Volatile.Read(ref cancelled) || Volatile.Read(ref winner) != -1)
                    {
                        break;
                    }

                    sources[i].Subscribe(a[i]);
                }
            }

            public void Cancel()
            {
                if (!Volatile.Read(ref cancelled))
                {
                    Volatile.Write(ref cancelled, true);
                    foreach (var inner in subscribers)
                    {
                        inner.Cancel();
                    }
                }
            }

            public void Request(long n)
            {
                int w = Volatile.Read(ref winner);
                if (w != -1)
                {
                    subscribers[w].Request(n);
                }
                else
                {
                    var a = subscribers;
                    int m = a.Length;
                    for (int i = 0; i < m; i++)
                    {
                        a[i].Request(n);
                    }
                }
            }

            public bool TryWin(int index)
            {
                int w = Volatile.Read(ref winner);
                if (w == -1 && Interlocked.CompareExchange(ref winner, -1, index) == -1)
                {
                    var a = subscribers;
                    int n = a.Length;
                    for (int i = 0; i < n; i++)
                    {
                        if (i != index)
                        {
                            a[i].Cancel();
                        }
                    }
                    return true;
                }
                return false;
            }
        }

        sealed class AmbInnerSubscriber : ISubscriber<T>, ISubscription
        {
            readonly ISubscriber<T> actual;

            readonly AmbSubscription parent;

            readonly int index;

            bool won;

            long requested;

            ISubscription s;

            internal AmbInnerSubscriber(AmbSubscription parent, ISubscriber<T> actual, int index)
            {
                this.parent = parent;
                this.actual = actual;
                this.index = index;
            }

            public void OnSubscribe(ISubscription s)
            {
                BackpressureHelper.DeferredSetOnce(ref this.s, ref requested, s);
            }

            public void OnNext(T t)
            {
                if (won)
                {
                    actual.OnNext(t);
                }
                else
                if (parent.TryWin(index))
                {
                    won = true;
                    actual.OnNext(t);
                }
                else
                {
                    Cancel();
                }
            }

            public void OnError(Exception e)
            {
                if (won)
                {
                    actual.OnError(e);
                }
                else
                if (parent.TryWin(index))
                {
                    actual.OnError(e);
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public void OnComplete()
            {
                if (won)
                {
                    actual.OnComplete();
                }
                else
                if (parent.TryWin(index))
                {
                    actual.OnComplete();
                }
            }

            public void Cancel()
            {
                SubscriptionHelper.Cancel(ref s);
            }

            public void Request(long n)
            {
                BackpressureHelper.DeferredRequest(ref s, ref requested, n);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class AmbConditionalSubscription : ISubscription
        {
            readonly AmbInnerConditionalSubscriber[] subscribers;

            Pad112 p0;

            int winner;

            bool cancelled;

            Pad112 p1;

            internal AmbConditionalSubscription(IConditionalSubscriber<T> actual, int n)
            {
                var a = new AmbInnerConditionalSubscriber[n];

                for (int i = 0; i < n; i++)
                {
                    a[i] = new AmbInnerConditionalSubscriber(this, actual, i);
                }

                winner = -1;
                subscribers = a;
            }

            internal void Subscribe(IPublisher<T>[] sources, int n)
            {
                var a = subscribers;
                for (int i = 0; i < n; i++)
                {
                    if (Volatile.Read(ref cancelled) || Volatile.Read(ref winner) != -1)
                    {
                        break;
                    }

                    sources[i].Subscribe(a[i]);
                }
            }

            public void Cancel()
            {
                if (!Volatile.Read(ref cancelled))
                {
                    Volatile.Write(ref cancelled, true);
                    foreach (var inner in subscribers)
                    {
                        inner.Cancel();
                    }
                }
            }

            public void Request(long n)
            {
                int w = Volatile.Read(ref winner);
                if (w != -1)
                {
                    subscribers[w].Request(n);
                }
                else
                {
                    var a = subscribers;
                    int m = a.Length;
                    for (int i = 0; i < m; i++)
                    {
                        a[i].Request(n);
                    }
                }
            }

            public bool TryWin(int index)
            {
                int w = Volatile.Read(ref winner);
                if (w == -1 && Interlocked.CompareExchange(ref winner, -1, index) == -1)
                {
                    var a = subscribers;
                    int n = a.Length;
                    for (int i = 0; i < n; i++)
                    {
                        if (i != index)
                        {
                            a[i].Cancel();
                        }
                    }
                    return true;
                }
                return false;
            }
        }

        sealed class AmbInnerConditionalSubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IConditionalSubscriber<T> actual;

            readonly AmbConditionalSubscription parent;

            readonly int index;

            bool won;

            long requested;

            ISubscription s;

            internal AmbInnerConditionalSubscriber(AmbConditionalSubscription parent, IConditionalSubscriber<T> actual, int index)
            {
                this.parent = parent;
                this.actual = actual;
                this.index = index;
            }

            public void OnSubscribe(ISubscription s)
            {
                BackpressureHelper.DeferredSetOnce(ref this.s, ref requested, s);
            }

            public void OnNext(T t)
            {
                if (!TryOnNext(t))
                {
                    s.Request(1);
                }
            }

            public bool TryOnNext(T t)
            {
                if (won)
                {
                    return actual.TryOnNext(t);
                }
                else
                if (parent.TryWin(index))
                {
                    won = true;
                    return actual.TryOnNext(t);
                }
                else
                {
                    Cancel();
                    return false;
                }

            }

            public void OnError(Exception e)
            {
                if (won)
                {
                    actual.OnError(e);
                }
                else
                if (parent.TryWin(index))
                {
                    actual.OnError(e);
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public void OnComplete()
            {
                if (won)
                {
                    actual.OnComplete();
                }
                else
                if (parent.TryWin(index))
                {
                    actual.OnComplete();
                }
            }

            public void Cancel()
            {
                SubscriptionHelper.Cancel(ref s);
            }

            public void Request(long n)
            {
                BackpressureHelper.DeferredRequest(ref s, ref requested, n);
            }
        }
    }
}
