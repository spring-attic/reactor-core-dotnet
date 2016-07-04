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
    sealed class ParallelUnorderedRunOn<T> : ParallelUnorderedFlux<T>
    {
        readonly IParallelFlux<T> source;

        readonly Scheduler scheduler;

        readonly int prefetch;

        public override int Parallelism
        {
            get
            {
                return source.Parallelism;
            }
        }

        internal ParallelUnorderedRunOn(IParallelFlux<T> source, Scheduler scheduler,
            int prefetch)
        {
            this.source = source;
            this.scheduler = scheduler;
            this.prefetch = prefetch;
        }

        public override void Subscribe(ISubscriber<T>[] subscribers)
        {
            if (!this.Validate(subscribers))
            {
                return;
            }
            int n = subscribers.Length;

            var parents = new ISubscriber<T>[n];

            for(int i = 0; i < n; i++)
            {
                var worker = scheduler.CreateWorker();
                var s = subscribers[i];
                if (s is IConditionalSubscriber<T>)
                {
                    parents[i] = new ParallelObserveOnConditionalSubscriber((IConditionalSubscriber<T>)s, prefetch, worker);
                }
                else
                {
                    parents[i] = new ParallelObserveOnSubscriber(s, prefetch, worker);
                }
            }

            source.Subscribe(parents);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal abstract class BaseParallelObserveOn : ISubscriber<T>, ISubscription
        {
            protected readonly Worker worker;

            protected readonly int prefetch;

            protected readonly int limit;

            protected readonly IQueue<T> queue;

            protected ISubscription s;

            protected long requested;

            protected bool cancelled;

            protected bool done;
            protected Exception error;

            Pad128 p0;

            protected long emitted;
            protected long polled;

            Pad112 p1;

            protected int wip;

            Pad120 p2;

            internal BaseParallelObserveOn(Worker worker, int prefetch)
            {
                this.prefetch = prefetch;
                this.limit = prefetch - (prefetch >> 2);
                this.queue = QueueDrainHelper.CreateQueue<T>(prefetch);
                this.worker = worker;
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    SubscribeActual();

                    s.Request(prefetch);
                }
            }

            protected abstract void SubscribeActual();

            public void OnNext(T t)
            {
                if (!queue.Offer(t))
                {
                    s.Cancel();
                    OnError(BackpressureHelper.MissingBackpressureException());
                    return;
                }
                Schedule();
            }

            public void OnError(Exception e)
            {
                error = e;
                Volatile.Write(ref done, true);
                Schedule();
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                Schedule();
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                    Schedule();
                }
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                s.Cancel();

                if (QueueDrainHelper.Enter(ref wip))
                {
                    queue.Clear();
                }
            }

            void Schedule()
            {
                if (QueueDrainHelper.Enter(ref wip))
                {
                    worker.Schedule(Run);
                }
            }

            protected abstract void Run();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal sealed class ParallelObserveOnSubscriber : BaseParallelObserveOn
        {
            readonly ISubscriber<T> actual;

            internal ParallelObserveOnSubscriber(ISubscriber<T> actual, int prefetch, Worker worker)
                : base(worker, prefetch)
            {
                this.actual = actual;
            }

            protected override void SubscribeActual()
            {
                actual.OnSubscribe(this);
            }


            protected override void Run()
            {
                var q = queue;
                var a = actual;
                int missed = 1;
                int lim = limit;
                long e = emitted;
                long p = polled;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        if (d)
                        {
                            var ex = error;
                            if (ex != null)
                            {
                                q.Clear();
                                a.OnError(ex);

                                worker.Dispose();
                                return;
                            }
                        }

                        T v;

                        bool empty = !q.Poll(out v);

                        if (d && empty)
                        {
                            a.OnComplete();

                            worker.Dispose();
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        a.OnNext(v);

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
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        if (d)
                        {
                            var ex = error;
                            if (ex != null)
                            {
                                q.Clear();
                                a.OnError(ex);

                                worker.Dispose();
                                return;
                            }
                            if (q.IsEmpty())
                            {
                                a.OnComplete();

                                worker.Dispose();
                                return;
                            } 
                        }
                    }

                    emitted = e;
                    polled = p;
                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal sealed class ParallelObserveOnConditionalSubscriber : BaseParallelObserveOn
        {
            readonly IConditionalSubscriber<T> actual;

            internal ParallelObserveOnConditionalSubscriber(
                IConditionalSubscriber<T> actual, int prefetch, Worker worker)
                : base(worker, prefetch)
            {
                this.actual = actual;
                
            }

            protected override void SubscribeActual()
            {
                actual.OnSubscribe(this);
            }

            protected override void Run()
            {
                var q = queue;
                var a = actual;
                int missed = 1;
                int lim = limit;
                long e = emitted;
                long p = polled;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        if (d)
                        {
                            var ex = error;
                            if (ex != null)
                            {
                                q.Clear();
                                a.OnError(ex);

                                worker.Dispose();
                                return;
                            }
                        }

                        T v;

                        bool empty = !q.Poll(out v);

                        if (d && empty)
                        {
                            a.OnComplete();

                            worker.Dispose();
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        if (a.TryOnNext(v))
                        {
                            e++;
                        }

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
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        if (d)
                        {
                            var ex = error;
                            if (ex != null)
                            {
                                q.Clear();
                                a.OnError(ex);

                                worker.Dispose();
                                return;
                            }
                            if (q.IsEmpty())
                            {
                                a.OnComplete();

                                worker.Dispose();
                                return;
                            }
                        }
                    }

                    emitted = e;
                    polled = p;
                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }
        }
    }
}
