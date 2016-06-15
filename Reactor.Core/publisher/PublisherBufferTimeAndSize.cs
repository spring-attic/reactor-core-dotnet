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
    sealed class PublisherBufferTimeAndSize<T> : IFlux<IList<T>>
    {
        readonly IPublisher<T> source;

        readonly int maxSize;

        readonly TimeSpan timespan;

        readonly TimedScheduler scheduler;

        readonly int capacityHint;

        internal PublisherBufferTimeAndSize(IPublisher<T> source, int maxSize, 
            TimeSpan timespan, TimedScheduler scheduler, int capacityHint)
        {
            this.source = source;
            this.maxSize = maxSize;
            this.timespan = timespan;
            this.scheduler = scheduler;
            this.capacityHint = capacityHint;
        }

        public void Subscribe(ISubscriber<IList<T>> s)
        {
            var worker = scheduler.CreateTimedWorker();

            source.Subscribe(new BufferTimeAndSizeSubscriber(s, maxSize, timespan, worker, capacityHint));
        }

        enum BufferWorkType
        {
            VALUE,
            COMPLETE,
            BOUNDARY
        }

        struct BufferWork
        {
            internal T value;
            internal BufferWorkType type;
            internal long index;
        }

        sealed class BufferTimeAndSizeSubscriber : ISubscriber<T>, ISubscription
        {
            readonly ISubscriber<IList<T>> actual;

            readonly int maxSize;

            readonly TimeSpan timespan;

            readonly TimedWorker worker;

            readonly IQueue<BufferWork> queue;

            IndexedMultipeDisposableStruct d;

            ISubscription s;

            Exception error;

            bool cancelled;

            long index;

            long requested;

            long produced;

            int wip;

            IList<T> buffer;

            internal BufferTimeAndSizeSubscriber(ISubscriber<IList<T>> actual, int maxSize,
                TimeSpan timespan, TimedWorker worker, int capacityHint)
            {
                this.actual = actual;
                this.maxSize = maxSize;
                this.timespan = timespan;
                this.worker = worker;
                this.queue = new SpscLinkedArrayQueue<BufferWork>(capacityHint);
                this.buffer = new List<T>();
            }

            public void Cancel()
            {
                s.Cancel();
                d.Dispose();
                worker.Dispose();

                if (QueueDrainHelper.Enter(ref wip))
                {
                    queue.Clear();
                    buffer = null;
                }
            }

            public void OnComplete()
            {
                d.Dispose();
                BufferWork bw = new BufferWork();
                bw.type = BufferWorkType.COMPLETE;
                lock (this)
                {
                    queue.Offer(bw);
                }
                Drain();
            }

            public void OnError(Exception e)
            {
                d.Dispose();
                Volatile.Write(ref error, e);
                Drain();
            }

            public void OnNext(T t)
            {
                BufferWork bw = new BufferWork();
                bw.value = t;
                lock (this)
                {
                    queue.Offer(bw);
                }
                Drain();
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    Schedule(0);

                    actual.OnSubscribe(this);

                    s.Request(long.MaxValue);
                }
            }

            void Schedule(long idx)
            {
                d.Replace(worker.Schedule(() => Run(idx), timespan), idx);
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                }
            }

            void Run(long index)
            {
                if (index == Volatile.Read(ref this.index))
                {
                    BufferWork bw = new BufferWork();
                    bw.type = BufferWorkType.BOUNDARY;
                    bw.index = index;
                    lock (this)
                    {
                        queue.Offer(bw);
                    }
                    Drain();
                }
            }

            void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                int missed = 1;
                var q = queue;
                var a = actual;
                var buf = buffer;

                for (;;)
                {

                    for (;;)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        Exception ex = Volatile.Read(ref error);
                        if (ex != null)
                        {
                            q.Clear();
                            a.OnError(ex);
                            return;
                        }

                        BufferWork bw;

                        if (q.Poll(out bw))
                        {
                            switch (bw.type)
                            {
                                case BufferWorkType.COMPLETE:
                                    {
                                        Interlocked.Exchange(ref index, long.MaxValue);

                                        q.Clear();
                                        buffer = null;

                                        if (buf.Count == 0L)
                                        {
                                            a.OnComplete();
                                        }
                                        else
                                        {
                                            long r = Volatile.Read(ref requested);
                                            long p = produced;
                                            if (r != p)
                                            {
                                                a.OnNext(buf);
                                                a.OnComplete();
                                            }
                                            else
                                            {
                                                a.OnError(BackpressureHelper.MissingBackpressureException());
                                            }
                                        }

                                        worker.Dispose();
                                        return;
                                    }
                                case BufferWorkType.BOUNDARY:
                                    {
                                        long idx = Interlocked.Increment(ref index);
                                        if (buf.Count != 0)
                                        {
                                            long r = Volatile.Read(ref requested);
                                            long p = produced;
                                            if (r != p)
                                            {
                                                buffer = new List<T>();
                                                a.OnNext(buf);
                                                buf = buffer;

                                                if (r != long.MaxValue)
                                                {
                                                    produced = p + 1;
                                                }
                                                Schedule(idx);
                                            }
                                            else
                                            {
                                                s.Cancel();
                                                d.Dispose();
                                                worker.Dispose();

                                                q.Clear();

                                                a.OnError(BackpressureHelper.MissingBackpressureException());

                                                return;
                                            }
                                        } else
                                        {
                                            Schedule(idx);
                                        }
                                    }
                                    break;
                                default:
                                    {
                                        buf.Add(bw.value);
                                        if (buf.Count == maxSize)
                                        {
                                            long idx = Interlocked.Increment(ref index);
                                            long r = Volatile.Read(ref requested);
                                            long p = produced;
                                            if (r != p)
                                            {
                                                buffer = new List<T>();
                                                a.OnNext(buf);
                                                buf = buffer;

                                                if (r != long.MaxValue)
                                                {
                                                    produced = p + 1;
                                                }
                                                Schedule(idx);
                                            }
                                            else
                                            {
                                                s.Cancel();
                                                d.Dispose();
                                                worker.Dispose();

                                                q.Clear();

                                                a.OnError(BackpressureHelper.MissingBackpressureException());

                                                return;
                                            }
                                        }
                                    }
                                    break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

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
