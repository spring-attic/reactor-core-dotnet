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

namespace Reactor.Core.parallel
{
    sealed class ParallelOrderedJoin<T> : IFlux<T>
    {
        readonly ParallelOrderedFlux<T> source;

        readonly int prefetch;

        internal ParallelOrderedJoin(ParallelOrderedFlux<T> source, int prefetch)
        {
            this.source = source;
            this.prefetch = prefetch;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            var parent = new OrderedJoin(s, source.Parallelism, prefetch);
            s.OnSubscribe(parent);
            source.SubscribeMany(parent.subscribers);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class OrderedJoin : ISubscription
        {
            static readonly IOrderedItem<T> FINISHED = new OrderedItem<T>(long.MaxValue, default(T));

            readonly ISubscriber<T> actual;

            internal readonly InnerSubscriber[] subscribers;

            readonly IOrderedItem<T>[] peek;

            Exception error;

            long requested;

            bool cancelled;

            Pad128 p0;

            int wip;

            Pad120 p1;

            long emitted;

            Pad120 p2;

            internal OrderedJoin(ISubscriber<T> actual, int n, int prefetch)
            {
                this.actual = actual;
                var a = new InnerSubscriber[n];
                for (int i = 0; i < n; i++)
                {
                    a[i] = new InnerSubscriber(this, prefetch);
                }
                this.subscribers = a;
                this.peek = new IOrderedItem<T>[n];
            } 

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                CancelAll();

                if (QueueDrainHelper.Enter(ref wip))
                {
                    ClearAll();
                }
            }

            void CancelAll()
            {
                foreach (var inner in subscribers)
                {
                    inner.Cancel();
                }
            }

            void ClearAll()
            {
                var a = subscribers;
                int n = a.Length;
                for (int i = 0; i < n; i++)
                {
                    peek[i] = null;
                    a[i].Clear();
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

            internal void InnerError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    CancelAll();
                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            internal void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                var b = actual;
                var vs = peek;
                var a = subscribers;
                int n = a.Length;
                int missed = 1;

                long e = emitted;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);

                    for (;;)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            ClearAll();
                            return;
                        }

                        var ex = Volatile.Read(ref error);
                        if (ex != null)
                        {
                            ex = ExceptionHelper.Terminate(ref error);
                            CancelAll();
                            ClearAll();

                            b.OnError(ex);
                            return;
                        }

                        bool fullRow = true;
                        int finished = 0;

                        IOrderedItem<T> min = null;
                        int minIndex = -1;

                        for (int i = 0; i < n; i++)
                        {
                            var inner = a[i];

                            bool d = Volatile.Read(ref inner.done);

                            var q = Volatile.Read(ref inner.queue);
                            if (q != null)
                            {
                                var vsi = vs[i];
                                if (vsi == null)
                                {
                                    bool hasValue;

                                    try
                                    {
                                        hasValue = q.Poll(out vsi);
                                    }
                                    catch (Exception exc)
                                    {
                                        ExceptionHelper.AddError(ref error, exc);
                                        ex = ExceptionHelper.Terminate(ref error);
                                        CancelAll();
                                        ClearAll();

                                        b.OnError(ex);
                                        return;
                                    }
                                    if (hasValue)
                                    {
                                        vs[i] = vsi;
                                        if (min == null || min.CompareTo(vsi) > 0)
                                        {
                                            min = vsi;
                                            minIndex = i;
                                        }
                                    } else
                                    {
                                        if (d)
                                        {
                                            vs[i] = FINISHED;
                                            finished++;
                                        }
                                        else
                                        {
                                            fullRow = false;
                                        }
                                    }
                                }
                                else
                                {
                                    if (vsi == FINISHED)
                                    {
                                        finished++;
                                    }
                                    else
                                    if (min == null || min.CompareTo(vsi) > 0)
                                    {
                                        min = vsi;
                                        minIndex = i;
                                    }
                                }
                            } else
                            {
                                fullRow = false;
                            }
                        }

                        if (finished == n)
                        {
                            b.OnComplete();
                            return;
                        }

                        if (!fullRow || e == r || min == null)
                        {
                            break;
                        }

                        vs[minIndex] = null;

                        b.OnNext(min.Value);

                        e++;
                        a[minIndex].RequestOne();
                    }


                    emitted = e;
                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }
        }

        sealed class InnerSubscriber : ISubscriber<IOrderedItem<T>>
        {
            readonly OrderedJoin parent;

            readonly int prefetch;

            readonly int limit;

            ISubscription s;

            internal IQueue<IOrderedItem<T>> queue;

            internal bool done;

            int fusionMode;

            int produced;

            internal InnerSubscriber(OrderedJoin parent, int prefetch)
            {
                this.parent = parent;
                this.prefetch = prefetch;
                this.limit = prefetch - (prefetch >> 2);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    var qs = s as IQueueSubscription<IOrderedItem<T>>;
                    if (qs != null)
                    {
                        int m = qs.RequestFusion(FuseableHelper.ANY);
                        if (m == FuseableHelper.SYNC)
                        {
                            fusionMode = m;
                            Volatile.Write(ref queue, qs);
                            Volatile.Write(ref done, true);

                            parent.Drain();
                            return;
                        }

                        if (m == FuseableHelper.ASYNC)
                        {
                            fusionMode = m;
                            Volatile.Write(ref queue, qs);
                            s.Request(prefetch < 0 ? long.MaxValue : prefetch);
                            return;
                        }
                    }

                    Volatile.Write(ref queue, QueueDrainHelper.CreateQueue<IOrderedItem<T>>(prefetch));

                    s.Request(prefetch < 0 ? long.MaxValue : prefetch);
                }
            }

            public void OnNext(IOrderedItem<T> t)
            {
                if (fusionMode == FuseableHelper.NONE)
                {
                    if (!queue.Offer(t))
                    {
                        OnError(BackpressureHelper.MissingBackpressureException("Queue full?!"));
                        return;
                    }
                }
                parent.Drain();
            }

            public void OnError(Exception e)
            {
                parent.InnerError(e);
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                parent.Drain();
            }

            internal void Cancel()
            {
                SubscriptionHelper.Cancel(ref s);
            }

            internal void Clear()
            {
                queue?.Clear();
            }

            internal void RequestOne()
            {
                if (fusionMode != FuseableHelper.SYNC)
                {
                    int p = produced + 1;
                    if (p == limit)
                    {
                        produced = 0;
                        s.Request(p);
                    }
                    else
                    {
                        produced = p;
                    }
                }
            }
        }
    }
}
