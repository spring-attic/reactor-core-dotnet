using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core;
using System.Threading;
using Reactor.Core.flow;
using Reactor.Core.subscription;
using Reactor.Core.util;
using System.Runtime.InteropServices;

namespace Reactor.Core.parallel
{
    internal sealed class ParallelUnorderedFork<T> : ParallelUnorderedFlux<T>
    {
        readonly IPublisher<T> source;

        readonly int parallelism;

        readonly int prefetch;


        internal ParallelUnorderedFork(IPublisher<T> source, int parallelism, int prefetch)
        {
            this.source = source;
            this.parallelism = parallelism;
            this.prefetch = prefetch;
        }

        public override int Parallelism
        {
            get
            {
                return parallelism;
            }
        }

        public override void Subscribe(ISubscriber<T>[] subscribers)
        {
            if (!this.Validate(subscribers))
            {
                return;
            }

            source.Subscribe(new UnorderedDispatcher(subscribers, prefetch));
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class UnorderedDispatcher : ISubscriber<T>
        {
            readonly ISubscriber<T>[] subscribers;

            readonly long[] requests;

            readonly long[] emissions;

            readonly int prefetch;

            readonly int limit;

            ISubscription s;

            IQueue<T> queue;

            bool done;
            Exception error;

            bool cancelled;

            int produced;

            int sourceMode;

            int index;

            int subscriberCount;

            Pad128 p0;

            int wip;

            Pad120 p1;

            internal UnorderedDispatcher(ISubscriber<T>[] subscribers, int prefetch)
            {
                this.subscribers = subscribers;
                int n = subscribers.Length;
                this.requests = new long[n];
                this.emissions = new long[n];
                this.prefetch = prefetch;
                this.limit = prefetch - (prefetch >> 2);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    var qs = s as IQueueSubscription<T>;
                    if (qs != null)
                    {
                        int m = qs.RequestFusion(FuseableHelper.ANY);

                        if (m == FuseableHelper.SYNC)
                        {
                            sourceMode = m;
                            queue = qs;
                            Volatile.Write(ref done, true);
                            SetupSubscribers();
                            Drain();
                            return;
                        }
                        if (m == FuseableHelper.ASYNC)
                        {
                            sourceMode = m;
                            queue = qs;
                            SetupSubscribers();
                            s.Request(prefetch < 0 ? long.MaxValue : prefetch);
                            return;
                        }
                    }

                    queue = QueueDrainHelper.CreateQueue<T>(prefetch);

                    SetupSubscribers();

                    s.Request(prefetch < 0 ? long.MaxValue : prefetch);
                }
            }

            void SetupSubscribers()
            {
                var array = subscribers;
                int n = array.Length;

                for (int i = 0; i < n; i++)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }
                    Volatile.Write(ref subscriberCount, i + 1);

                    array[i].OnSubscribe(new RailSubscription(this, i));
                }
            }

            internal void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                s.Cancel();
                if (QueueDrainHelper.Enter(ref wip))
                {
                    queue.Clear();
                }
            }

            internal void Request(int index, long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    var rs = requests;
                    BackpressureHelper.GetAndAddCap(ref rs[index], n);
                    if (Volatile.Read(ref subscriberCount) == rs.Length)
                    {
                        Drain();
                    }
                }
            }


            public void OnNext(T t)
            {
                if (sourceMode == FuseableHelper.NONE)
                {
                    if (!queue.Offer(t))
                    {
                        s.Cancel();
                        OnError(BackpressureHelper.MissingBackpressureException());
                        return;
                    }
                }
                Drain();
            }

            public void OnError(Exception e)
            {
                error = e;
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                Drain();
            }

            void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                if (sourceMode == FuseableHelper.SYNC)
                {
                    DrainSync();
                }
                else
                {
                    DrainAsync();
                }
            }

            void DrainSync()
            {
                int missed = 1;
                var q = queue;
                var a = subscribers;
                int n = a.Length;
                var r = requests;
                var e = emissions;
                int i = index;

                for (;;)
                {

                    int notReady = 0;

                    for (;;)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            queue.Clear();
                            return;
                        }

                        bool empty = q.IsEmpty();

                        if (empty)
                        {
                            foreach (var s in a)
                            {
                                s.OnComplete();
                            }
                            return;
                        }

                        long ei = e[i];
                        if (Volatile.Read(ref r[i]) != ei)
                        {
                            T v;
                            try
                            {
                                empty = !q.Poll(out v);
                            }
                            catch (Exception ex)
                            {
                                ExceptionHelper.ThrowIfFatal(ex);
                                s.Cancel();
                                foreach (var s in a)
                                {
                                    s.OnError(ex);
                                }
                                return;
                            }

                            if (empty)
                            {
                                foreach (var s in a)
                                {
                                    s.OnComplete();
                                }
                                return;
                            }

                            a[i].OnNext(v);

                            e[i] = ei + 1;

                            notReady = 0;
                        } else
                        {
                            notReady++;
                        }

                        if (++i == n)
                        {
                            i = 0;
                        }

                        if (notReady == n)
                        {
                            break;
                        }
                    }

                    index = i;
                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            void DrainAsync()
            {
                int missed = 1;
                var q = queue;
                var a = subscribers;
                int n = a.Length;
                var r = requests;
                var e = emissions;
                int i = index;
                int c = produced;

                for (;;)
                {
                    int notReady = 0;

                    for (;;)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            queue.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);
                        if (d)
                        {
                            var ex = error;
                            if (ex != null)
                            {
                                q.Clear();
                                foreach (var s in a)
                                {
                                    s.OnError(ex);
                                }
                                return;
                            }
                        }

                        bool empty = q.IsEmpty();

                        if (d && empty)
                        {
                            foreach (var s in a)
                            {
                                s.OnComplete();
                            }
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        long ei = e[i];
                        if (Volatile.Read(ref r[i]) != ei)
                        {
                            T v;
                            try
                            {
                                empty = !q.Poll(out v);
                            }
                            catch (Exception ex)
                            {
                                ExceptionHelper.ThrowIfFatal(ex);
                                s.Cancel();
                                foreach (var s in a)
                                {
                                    s.OnError(ex);
                                }
                                return;
                            }

                            if (empty)
                            {
                                break;
                            }

                            a[i].OnNext(v);

                            e[i] = ei + 1;

                            int ci = ++c;
                            if (ci == limit)
                            {
                                c = 0;
                                s.Request(ci);
                            }

                            notReady = 0;
                        }
                        else
                        {
                            notReady++;
                        }

                        if (++i == n)
                        {
                            i = 0;
                        }

                        if (notReady == n)
                        {
                            break;
                        }
                    }

                    index = i;
                    produced = c;
                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            sealed class RailSubscription : ISubscription
            {
                readonly UnorderedDispatcher parent;

                readonly int index;

                internal RailSubscription(UnorderedDispatcher parent, int index)
                {
                    this.parent = parent;
                    this.index = index;
                }

                public void Request(long n)
                {
                    parent.Request(index, n);
                }

                public void Cancel()
                {
                    parent.Cancel();
                }
            }
        }
    }
}
