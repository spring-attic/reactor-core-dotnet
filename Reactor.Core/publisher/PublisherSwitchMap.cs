using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core.flow;
using Reactor.Core.subscriber;
using Reactor.Core.subscription;
using Reactor.Core.util;
using System.Threading;
using System.Runtime.InteropServices;

namespace Reactor.Core.publisher
{
    sealed class PublisherSwitchMap<T, R> : IFlux<R>
    {
        readonly IPublisher<T> source;

        readonly Func<T, IPublisher<R>> mapper;

        readonly int prefetch;

        internal PublisherSwitchMap(IPublisher<T> source, Func<T, IPublisher<R>> mapper, int prefetch)
        {
            this.source = source;
            this.mapper = mapper;
            this.prefetch = prefetch;
        }

        public void Subscribe(ISubscriber<R> s)
        {
            source.Subscribe(new SwitchMapSubscriber(s, mapper, prefetch));
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class SwitchMapSubscriber : ISubscriber<T>, ISubscription
        {
            readonly ISubscriber<R> actual;

            readonly Func<T, IPublisher<R>> mapper;

            readonly int prefetch;

            ISubscription s;

            Exception error;

            bool done;

            bool cancelled;

            Pad128 p0;

            int wip;

            Pad120 p1;

            long requested;

            Pad120 p2;

            SwitchMapInnerSubscriber inner;
            long index;

            Pad112 p3;

            internal SwitchMapSubscriber(ISubscriber<R> actual, Func<T, IPublisher<R>> mapper, int prefetch)
            {
                this.actual = actual;
                this.mapper = mapper;
                this.prefetch = prefetch;
            }

            internal void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                int missed = 1;

                var a = actual;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    long e = 0L;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            inner = null;
                            return;
                        }

                        Exception ex = Volatile.Read(ref error);
                        if (ex != null)
                        {
                            ex = ExceptionHelper.Terminate(ref error);

                            inner = null;
                            a.OnError(ex);
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        var inr = Volatile.Read(ref inner);

                        d = d && (inr == null || inr.IsDone());

                        R v;

                        bool empty;

                        if (inr != null)
                        {
                            try
                            {
                                empty = !inr.queue.Poll(out v);
                            }
                            catch (Exception exc)
                            {
                                ExceptionHelper.ThrowIfFatal(exc);

                                ExceptionHelper.AddError(ref error, exc);

                                s.Cancel();
                                inner = null;

                                exc = ExceptionHelper.Terminate(ref error);

                                a.OnError(exc);
                                return;
                            }
                        }
                        else
                        {
                            v = default(R);
                            empty = true;
                        }

                        if (d && empty)
                        {
                            inner = null;
                            a.OnComplete();
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        a.OnNext(v);

                        e++;
                        inner.RequestOne();
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            inner = null;
                            return;
                        }

                        Exception ex = Volatile.Read(ref error);
                        if (ex != null)
                        {
                            ex = ExceptionHelper.Terminate(ref error);

                            inner = null;
                            a.OnError(ex);
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        var inr = Volatile.Read(ref inner);

                        d = d && (inr == null || inr.IsDone());

                        bool empty = inr == null || inr.queue.IsEmpty();

                        if (d && empty)
                        {
                            inner = null;

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

            internal void InnerNext(long index)
            {
                if (Volatile.Read(ref this.index) == index)
                {
                    Drain();
                }
            }

            internal void InnerError(long index, SwitchMapInnerSubscriber inner, Exception ex)
            {
                if (Volatile.Read(ref this.index) == index)
                {
                    if (ExceptionHelper.AddError(ref error, ex))
                    {
                        s.Cancel();
                        inner.SetDone();
                        Drain();
                        return;
                    }
                }

                ExceptionHelper.OnErrorDropped(ex);
            }

            internal void InnerComplete(long index)
            {
                if (Volatile.Read(ref this.index) == index)
                {
                    Drain();
                }
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    actual.OnSubscribe(this);

                    s.Request(long.MaxValue);
                }
            }

            public void OnNext(T t)
            {
                if (done)
                {
                    return;
                }

                long idx = Interlocked.Increment(ref index);

                var current = Volatile.Read(ref inner);
                current?.Cancel();

                IPublisher<R> p;

                try
                {
                    p = mapper(t);
                    if (p == null)
                    {
                        throw new NullReferenceException("The mapper returned a null IPublisher");
                    }
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowIfFatal(ex);
                    s.Cancel();
                    if (ExceptionHelper.AddError(ref error, ex))
                    {
                        Volatile.Write(ref done, true);
                        Drain();
                    }
                    else
                    {
                        Volatile.Write(ref done, true);
                        ExceptionHelper.OnErrorDropped(ex);
                    }
                    return;
                }

                var inr = new SwitchMapInnerSubscriber(this, prefetch, idx);

                if (Interlocked.CompareExchange(ref inner, inr, current) == current)
                {
                    p.Subscribe(inr);
                }
            }

            public void OnError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    Volatile.Write(ref done, true);
                    CancelInner();
                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                Drain();
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                    Drain();
                }
            }

            void CancelInner()
            {
                var a = Volatile.Read(ref inner);
                if (a != TERMINATED)
                {
                    a = Interlocked.Exchange(ref inner, TERMINATED);
                    if (a != TERMINATED)
                    {
                        a?.Cancel();
                    }
                }
            }

            public void Cancel()
            {
                s.Cancel();
                CancelInner();
            }
        }

        /// <summary>
        /// The terminated instance
        /// </summary>
        static readonly SwitchMapInnerSubscriber TERMINATED = new SwitchMapInnerSubscriber(null, 0, long.MaxValue);

        sealed class SwitchMapInnerSubscriber : ISubscriber<R>
        {
            readonly SwitchMapSubscriber parent;

            readonly int prefetch;

            readonly int limit;

            readonly long index;

            bool done;

            long consumed;

            int fusionMode;

            internal IQueue<R> queue;

            ISubscription s;

            internal SwitchMapInnerSubscriber(SwitchMapSubscriber parent, int prefetch, long index)
            {
                this.parent = parent;
                this.prefetch = prefetch;
                this.limit = prefetch - (prefetch >> 2);
                this.index = index;
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    var qs = s as IQueueSubscription<R>;
                    if (qs != null)
                    {
                        int m = qs.RequestFusion(FuseableHelper.ANY);
                        if (m == FuseableHelper.SYNC)
                        {
                            fusionMode = m;
                            queue = qs;
                            Volatile.Write(ref done, true);

                            parent.Drain();
                        }
                        else
                        if (m == FuseableHelper.ASYNC)
                        {
                            fusionMode = m;
                            queue = qs;

                            s.Request(prefetch < 0 ? long.MaxValue : prefetch);

                            return;
                        }
                    }

                    queue = QueueDrainHelper.CreateQueue<R>(prefetch);

                    s.Request(prefetch < 0 ? long.MaxValue : prefetch);
                }
            }

            public void OnNext(R t)
            {
                if (fusionMode != FuseableHelper.ASYNC)
                {
                    queue.Offer(t);
                }
                parent.InnerNext(index);
            }

            public void OnError(Exception e)
            {
                parent.InnerError(index, this, e);
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                parent.InnerComplete(index);
            }

            internal void RequestOne()
            {
                if (fusionMode != FuseableHelper.SYNC)
                {
                    long p = consumed + 1;
                    if (p == limit)
                    {
                        consumed = 0;
                        s.Request(p);
                    }
                    else
                    {
                        consumed = p;
                    }
                }
            }

            internal void Cancel()
            {
                SubscriptionHelper.Cancel(ref s);
            }

            internal bool IsDone()
            {
                return Volatile.Read(ref done);
            }

            internal void SetDone()
            {
                Volatile.Write(ref done, true);
            }
        }
    }
}
