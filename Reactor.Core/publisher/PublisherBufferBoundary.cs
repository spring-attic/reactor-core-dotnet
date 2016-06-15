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
    sealed class PublisherBufferBoundary<T, U> : IFlux<IList<T>>
    {
        readonly IPublisher<T> source;

        readonly IPublisher<U> other;

        readonly int capacityHint;

        public PublisherBufferBoundary(IPublisher<T> source, IPublisher<U> other, int capacityHint)
        {
            this.source = source;
            this.other = other;
            this.capacityHint = capacityHint;
        }

        public void Subscribe(ISubscriber<IList<T>> s)
        {
            var parent = new BufferBoundarySubscriber(s, capacityHint);

            other.Subscribe(parent.other);

            source.Subscribe(parent);
        }

        enum BufferWorkType
        {
            /// <summary>
            /// Add a value to the current buffer.
            /// </summary>
            VALUE,
            /// <summary>
            /// Complete the entire buffering operation.
            /// </summary>
            COMPLETE,
            /// <summary>
            /// Start a new buffer.
            /// </summary>
            BOUNDARY
        }

        struct BufferWork
        {
            internal T value;
            internal BufferWorkType type;
        }

        sealed class BufferBoundarySubscriber : ISubscriber<T>, ISubscription
        {
            internal readonly BufferBoundaryOtherSubscriber other;

            readonly ISubscriber<IList<T>> actual;

            IList<T> buffer;

            ISubscription s;

            Exception error;

            int wip;

            bool cancelled;

            IQueue<BufferWork> queue;

            public BufferBoundarySubscriber(ISubscriber<IList<T>> actual, int capacityHint)
            {
                this.actual = actual;
                this.other = new BufferBoundaryOtherSubscriber(this);
                this.buffer = new List<T>();
                this.queue = new SpscLinkedArrayQueue<BufferWork>(capacityHint);
            }

            internal void otherNext()
            {
                var b = new BufferWork();
                b.type = BufferWorkType.BOUNDARY;

                lock (this)
                {
                    queue.Offer(b);
                }
                Drain();
            }

            internal void otherError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            internal void otherComplete()
            {
                var b = new BufferWork();
                b.type = BufferWorkType.COMPLETE;

                lock (this)
                {
                    queue.Offer(b);
                }
                Drain();
            }

            public void OnNext(T t)
            {
                var b = new BufferWork();
                b.value = t;

                lock (this)
                {
                    queue.Offer(b);
                }
                Drain();
            }

            public void OnError(Exception e)
            {
                other.Cancel();
                if (ExceptionHelper.AddError(ref error, e))
                {
                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public void OnComplete()
            {
                other.Cancel();
                var b = new BufferWork();
                b.type = BufferWorkType.COMPLETE;

                lock (this)
                {
                    queue.Offer(b);
                }
                Drain();
            }

            void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                int missed = 1;
                var a = actual;
                var q = queue;

                var current = buffer;

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
                            ex = ExceptionHelper.Terminate(ref error);

                            var c = Interlocked.Exchange(ref this.s, SubscriptionHelper.Cancelled);

                            if (c == null)
                            {
                                q.Clear();

                                a.OnSubscribe(EmptySubscription<IList<T>>.Instance);
                            }
                            else
                            {
                                c.Cancel();

                                q.Clear();
                            }

                            a.OnError(ex);
                            return;
                        }

                        BufferWork b;

                        if (q.Poll(out b))
                        {
                            switch (b.type)
                            {
                                case BufferWorkType.BOUNDARY:
                                    {
                                        buffer = new List<T>();
                                        if (current.Count != 0)
                                        {
                                            a.OnNext(current);
                                        }
                                        else
                                        {
                                            other.Request(1);
                                        }
                                        current = buffer;
                                    }
                                    break;
                                case BufferWorkType.COMPLETE:
                                    {
                                        buffer = null;

                                        var c = Interlocked.Exchange(ref this.s, SubscriptionHelper.Cancelled);

                                        if (c == null)
                                        {
                                            q.Clear();

                                            a.OnSubscribe(EmptySubscription<IList<T>>.Instance);
                                        }
                                        else
                                        {
                                            c.Cancel();

                                            q.Clear();
                                        }

                                        if (current.Count != 0)
                                        {
                                            a.OnNext(current);
                                        }
                                        a.OnComplete();
                                        return;
                                    }
                                default:
                                    {
                                        current.Add(b.value);
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

            public void Request(long n)
            {
                other.Request(n);
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                other.Cancel();
                SubscriptionHelper.Cancel(ref s);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    actual.OnSubscribe(this);

                    s.Request(long.MaxValue);
                }
            }
        }

        sealed class BufferBoundaryOtherSubscriber : ISubscriber<U>, ISubscription
        {
            readonly BufferBoundarySubscriber parent;

            ISubscription s;

            long requested;

            internal BufferBoundaryOtherSubscriber(BufferBoundarySubscriber parent)
            {
                this.parent = parent;
            }

            public void OnComplete()
            {
                parent.otherComplete();
            }

            public void OnError(Exception e)
            {
                parent.otherError(e);
            }

            public void OnNext(U t)
            {
                parent.otherNext();
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
            }
        }
    }
}
