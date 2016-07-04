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
using Reactor.Core.publisher;
using System.Runtime.InteropServices;

namespace Reactor.Core.parallel
{
    sealed class ParallelGroups<T> : IFlux<IGroupedFlux<int, T>>
    {
        readonly IParallelFlux<T> source;

        internal ParallelGroups(IParallelFlux<T> source)
        {
            this.source = source;
        }

        public void Subscribe(ISubscriber<IGroupedFlux<int, T>> s)
        {
            int n = source.Parallelism;

            var groups = new InnerGroup[n];

            for (int i = 0; i < n; i++)
            {
                groups[i] = new InnerGroup(i, Flux.BufferSize); // FIXME customizable?
            }

            s.OnSubscribe(new PublisherArray<IGroupedFlux<int, T>>.ArraySubscription(s, groups));

            source.Subscribe(groups);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class InnerGroup : IGroupedFlux<int, T>, ISubscriber<T>, ISubscription
        {
            readonly int key;

            readonly int prefetch;

            readonly int limit;

            ISubscriber<T> actual;

            int once;

            ISubscription s;

            IQueue<T> queue;

            bool done;
            Exception error;

            bool cancelled;

            long requested;

            Pad128 p0;

            int wip;

            Pad120 p1;

            long emitted;
            long polled;

            Pad112 p2;

            public int Key
            {
                get
                {
                    return key;
                }
            }

            internal InnerGroup(int key, int prefetch)
            {
                this.key = key;
                this.prefetch = prefetch;
                this.limit = prefetch - (prefetch >> 2);
                this.queue = QueueDrainHelper.CreateQueue<T>(prefetch);
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                SubscriptionHelper.Cancel(ref s);

                if (QueueDrainHelper.Enter(ref wip))
                {
                    actual = null;
                    queue.Clear();
                }
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnError(Exception e)
            {
                error = e;
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnNext(T t)
            {
                if (!queue.Offer(t))
                {
                    Cancel();
                    OnError(BackpressureHelper.MissingBackpressureException("Queue full?!"));
                    return;
                }
                Drain();
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    s.Request(prefetch < 0 ? long.MaxValue : prefetch);
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                    Drain();
                }
            }

            public void Subscribe(ISubscriber<T> s)
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    s.OnSubscribe(this);
                    Volatile.Write(ref actual, s);
                    if (Volatile.Read(ref cancelled))
                    {
                        actual = null;
                        return;
                    }
                    Drain();
                }
                else
                {
                    EmptySubscription<T>.Error(s, new InvalidOperationException("The group can be subscribed to at most once"));
                }
            }

            void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                var q = queue;
                var a = Volatile.Read(ref actual);
                int missed = 1;
                long e = emitted;
                long p = polled;
                int lim = limit;

                for (;;)
                {
                    if (a != null)
                    {
                        long r = Volatile.Read(ref requested);

                        while (e != r)
                        {
                            if (Volatile.Read(ref cancelled))
                            {
                                actual = null;
                                queue.Clear();
                                return;
                            }

                            bool d = Volatile.Read(ref done);

                            T t;

                            bool empty = !q.Poll(out t);

                            if (d && empty)
                            {
                                actual = null;
                                var ex = error;
                                if (ex != null)
                                {
                                    a.OnError(ex);
                                }
                                else
                                {
                                    a.OnComplete();
                                }
                                return;
                            }

                            if (empty)
                            {
                                break;
                            }

                            a.OnNext(t);

                            e++;

                            if (++p == lim)
                            {
                                p = 0;
                                s.Request(lim);
                            }
                        }

                        if (e == r)
                        {
                            if (Volatile.Read(ref cancelled))
                            {
                                actual = null;
                                queue.Clear();
                                return;
                            }

                            bool d = Volatile.Read(ref done);

                            bool empty = q.IsEmpty();

                            if (d && empty)
                            {
                                actual = null;
                                var ex = error;
                                if (ex != null)
                                {
                                    a.OnError(ex);
                                }
                                else
                                {
                                    a.OnComplete();
                                }
                                return;
                            }
                        }

                        emitted = e;
                        polled = p;
                    }

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                    if (a == null)
                    {
                        a = Volatile.Read(ref actual);
                    }
                }
            }
        }
    }
}
