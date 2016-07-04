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
    sealed class ParallelUnorderedJoin<T> : IFlux<T>
    {
        readonly IParallelFlux<T> source;

        readonly int prefetch;

        internal ParallelUnorderedJoin(IParallelFlux<T> source, int prefetch)
        {
            this.source = source;
            this.prefetch = prefetch;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            var parent = new JoinSubscription(s, source.Parallelism, prefetch);
            s.OnSubscribe(parent);

            source.Subscribe(parent.subscribers);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class JoinSubscription : ISubscription
        {
            readonly ISubscriber<T> actual;

            internal readonly JoinInnerSubscriber[] subscribers;

            long requested;

            bool cancelled;

            int done;
            Exception error;

            Pad128 p0;

            int wip;

            Pad120 p1;

            internal JoinSubscription(ISubscriber<T> actual, int n, int prefetch)
            {
                this.actual = actual;
                var a = new JoinInnerSubscriber[n];
                for (int i = 0; i < n; i++)
                {
                    a[i] = new JoinInnerSubscriber(this, prefetch);
                }
                this.subscribers = a;
                Volatile.Write(ref done, n);
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                    Drain();
                }
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);

                CancelAll();

                if (QueueDrainHelper.Enter(ref wip))
                {
                    Cleanup();
                }
            }

            void CancelAll()
            {
                foreach (var a in subscribers)
                {
                    a.Cancel();
                }
            }

            void Cleanup()
            {
                foreach (var a in subscribers)
                {
                    a.queue = null;
                }
            }

            internal void InnerNext(JoinInnerSubscriber inner, T value)
            {
                if (QueueDrainHelper.TryEnter(ref wip))
                {
                    long r = Volatile.Read(ref requested);
                    if (r != 0L)
                    {
                        actual.OnNext(value);
                        if (r != long.MaxValue)
                        {
                            Interlocked.Decrement(ref requested);
                        }
                        inner.RequestOne();

                    } else
                    {
                        var q = inner.Queue();

                        if (!q.Offer(value))
                        {
                            InnerError(BackpressureHelper.MissingBackpressureException("Queue full?!"));
                            return;
                        }
                    }

                    if (QueueDrainHelper.Leave(ref wip, 1) == 0)
                    {
                        return;
                    }
                } else
                {
                    var q = inner.Queue();

                    if (!q.Offer(value))
                    {
                        InnerError(BackpressureHelper.MissingBackpressureException("Queue full?!"));
                        return;
                    }

                    if (!QueueDrainHelper.Enter(ref wip))
                    {
                        return;
                    }
                }

                DrainLoop();
            }

            internal void InnerError(Exception ex)
            {
                if (ExceptionHelper.AddError(ref error, ex))
                {
                    CancelAll();
                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(ex);
                }
            }

            internal void InnerComplete()
            {
                Interlocked.Decrement(ref done);
                Drain();
            }

            void Drain()
            {
                if (QueueDrainHelper.Enter(ref wip))
                {
                    DrainLoop();
                }
            }

            void DrainLoop()
            {
                int missed = 1;
                var a = actual;
                var array = subscribers;
                int n = array.Length;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    long e = 0L;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Cleanup();
                            return;
                        }

                        var ex = Volatile.Read(ref error);
                        if (ex != null)
                        {
                            ex = ExceptionHelper.Terminate(ref error);
                            Cleanup();
                            a.OnError(ex);
                            return;
                        }

                        bool d = Volatile.Read(ref done) == 0;

                        bool empty = true;

                        bool full = false;

                        foreach (var inner in array)
                        {
                            var q = Volatile.Read(ref inner.queue);

                            if (q != null)
                            {
                                T v;

                                bool hasValue;

                                try
                                {
                                    hasValue = q.Poll(out v);
                                }
                                catch (Exception exc)
                                {
                                    ExceptionHelper.AddError(ref error, exc);
                                    ex = ExceptionHelper.Terminate(ref error);
                                    CancelAll();
                                    Cleanup();
                                    a.OnError(ex);
                                    return;
                                }

                                if (hasValue)
                                {
                                    empty = false;
                                    a.OnNext(v);
                                    inner.RequestOne();
                                    if (++e == r)
                                    {
                                        full = true;
                                        break;
                                    }
                                }
                            }
                        }

                        if (full)
                        {
                            break;
                        }

                        if (d && empty)
                        {
                            a.OnComplete();
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Cleanup();
                            return;
                        }

                        var ex = Volatile.Read(ref error);
                        if (ex != null)
                        {
                            ex = ExceptionHelper.Terminate(ref error);
                            Cleanup();
                            a.OnError(ex);
                            return;
                        }

                        bool d = Volatile.Read(ref done) == 0;

                        bool empty = true;

                        foreach (var inner in array)
                        {
                            var q = Volatile.Read(ref inner.queue);
                            if (q != null && !q.IsEmpty())
                            {
                                empty = false;
                                break;
                            }
                        }

                        if (d && empty)
                        {
                            a.OnComplete();
                            return;
                        }
                    }

                    if (e != 0L && r != long.MaxValue)
                    {
                        Interlocked.Add(ref requested, -e);
                    }

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            internal sealed class JoinInnerSubscriber : ISubscriber<T>
            {
                readonly JoinSubscription parent;

                readonly int prefetch;

                readonly int limit;

                ISubscription s;

                bool done;

                internal IQueue<T> queue;

                long produced;

                int fusionMode;

                internal JoinInnerSubscriber(JoinSubscription parent, int prefetch)
                {
                    this.parent = parent;
                    this.prefetch = prefetch;
                    this.limit = prefetch - (prefetch >> 2);
                }

                public void Cancel()
                {
                    SubscriptionHelper.Cancel(ref s);
                }

                public void OnComplete()
                {

                    parent.InnerComplete();
                }

                public void OnError(Exception e)
                {
                    parent.InnerError(e);
                }

                public void OnNext(T t)
                {
                    if (fusionMode == FuseableHelper.NONE)
                    {
                        parent.InnerNext(this, t);
                    }
                    else
                    {
                        parent.Drain();
                    }
                }

                public void OnSubscribe(ISubscription s)
                {
                    if (SubscriptionHelper.SetOnce(ref this.s, s))
                    {
                        var qs = s as IQueueSubscription<T>;
                        if (qs != null)
                        {
                            int m = qs.RequestFusion(FuseableHelper.ANY);
                            if (m == FuseableHelper.SYNC)
                            {
                                fusionMode = m;
                                Volatile.Write(ref queue, qs);
                                Volatile.Write(ref done, true);
                                parent.InnerComplete();
                                return;
                            }
                            if (m == FuseableHelper.ASYNC)
                            {
                                fusionMode = m;
                                Volatile.Write(ref queue, qs);
                            }
                        }

                        s.Request(prefetch < 0 ? long.MaxValue : prefetch);
                    }
                }

                internal IQueue<T> Queue()
                {
                    var q = Volatile.Read(ref queue);
                    if (q == null)
                    {
                        q = QueueDrainHelper.CreateQueue<T>(prefetch);
                        Volatile.Write(ref queue, q);
                    }
                    return q;
                }

                public void RequestOne()
                {
                    if (fusionMode != FuseableHelper.SYNC)
                    {
                        long p = produced + 1;
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
}
