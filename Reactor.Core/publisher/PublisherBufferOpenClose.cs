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
    sealed class PublisherBufferOpenClose<T, U, V> : IFlux<IList<T>>
    {
        readonly IPublisher<T> source;

        readonly IPublisher<U> open;

        readonly Func<U, IPublisher<V>> close;

        readonly int capacityHint;

        internal PublisherBufferOpenClose(IPublisher<T> source, IPublisher<U> open,
            Func<U, IPublisher<V>> close, int capacityHint)
        {
            this.source = source;
            this.open = open;
            this.close = close;
            this.capacityHint = capacityHint;
        }

        public void Subscribe(ISubscriber<IList<T>> s)
        {
            var parent = new BufferOpenCloseSubscriber(s, close, capacityHint);

            open.Subscribe(parent.open);

            source.Subscribe(parent);
        }

        enum BufferWorkType
        {
            /// <summary>
            /// Add a value to the active buffers.
            /// </summary>
            VALUE,
            /// <summary>
            /// Complete all buffers.
            /// </summary>
            COMPLETE,
            /// <summary>
            /// Open a new buffer.
            /// </summary>
            OPEN,
            /// <summary>
            /// Close a specific buffer.
            /// </summary>
            CLOSE
        }

        struct BufferWork
        {
            internal T value;
            internal BufferWorkType type;
            internal BufferCloseSubscriber buffer;
        }

        sealed class BufferOpenCloseSubscriber : ISubscriber<T>, ISubscription
        {
            readonly ISubscriber<IList<T>> actual;

            internal readonly BufferOpenSubscriber open;

            readonly Func<U, IPublisher<V>> close;

            readonly IQueue<BufferWork> queue;

            readonly LinkedList<BufferCloseSubscriber> buffers;

            int wip;

            bool cancelled;

            Exception error;

            ISubscription s;

            internal BufferOpenCloseSubscriber(ISubscriber<IList<T>> actual,
                Func<U, IPublisher<V>> close, int capacityHint)
            {
                this.actual = actual;
                this.open = new BufferOpenSubscriber(this);
                this.close = close;
                this.queue = new SpscLinkedArrayQueue<BufferWork>(capacityHint);
                this.buffers = new LinkedList<BufferCloseSubscriber>();
            }

            public void Cancel()
            {
                if (Volatile.Read(ref cancelled))
                {
                    return;
                }

                Volatile.Write(ref cancelled, true);
                s.Cancel();
                open.Cancel();

                if (QueueDrainHelper.Enter(ref wip))
                {
                    foreach (var close in buffers)
                    {
                        close.Cancel();
                    }
                    queue.Clear();
                }
            }

            public void OnComplete()
            {
                open.Cancel();
                var b = new BufferWork();
                b.type = BufferWorkType.COMPLETE;

                lock (this)
                {
                    queue.Offer(b);
                }
                Drain();
            }

            public void OnError(Exception e)
            {
                open.Cancel();
                if (ExceptionHelper.AddError(ref error, e))
                {
                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
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
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    actual.OnSubscribe(this);

                    s.Request(long.MaxValue);
                }
            }

            public void Request(long n)
            {
                open.Request(n);
            }

            internal void OpenNext(U u)
            {
                var b = new BufferWork();
                b.type = BufferWorkType.OPEN;
                b.buffer = new BufferCloseSubscriber(this, u);

                lock (this)
                {
                    queue.Offer(b);
                }
                Drain();
            }

            internal void OpenError(Exception e)
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

            internal void OpenComplete()
            {
                var b = new BufferWork();
                b.type = BufferWorkType.COMPLETE;

                lock (this)
                {
                    queue.Offer(b);
                }
                Drain();
            }

            internal void CloseNext(BufferCloseSubscriber v)
            {
                var b = new BufferWork();
                b.type = BufferWorkType.CLOSE;
                b.buffer = v;

                lock (this)
                {
                    queue.Offer(b);
                }
                Drain();
            }

            internal void CloseError(Exception e)
            {
                open.Cancel();
                if (ExceptionHelper.AddError(ref error, e))
                {
                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
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
                var bs = buffers;

                for (;;)
                {
                    for (;;)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            foreach (var close in buffers)
                            {
                                close.Cancel();
                            }
                            queue.Clear();
                            return;
                        }

                        if (HandleException(a, q))
                        {
                            return;
                        }

                        BufferWork bw;

                        if (q.Poll(out bw))
                        {
                            switch (bw.type)
                            {
                                case BufferWorkType.COMPLETE:
                                    {
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

                                        foreach (var buffer in bs)
                                        {
                                            var b = buffer.buffer;
                                            if (b.Count != 0)
                                            {
                                                a.OnNext(b);
                                            }
                                        }

                                        a.OnComplete();
                                        return;
                                    }
                                case BufferWorkType.OPEN:
                                    {
                                        var b = bw.buffer;

                                        bs.AddLast(b);

                                        IPublisher<V> p;

                                        try
                                        {
                                            p = close(b.u);
                                            if (p == null)
                                            {
                                                throw new NullReferenceException("The close function returned a null IPublisher");
                                            }
                                        }
                                        catch (Exception exc)
                                        {
                                            ExceptionHelper.ThrowIfFatal(exc);
                                            ExceptionHelper.AddError(ref error, exc);
                                            HandleException(a, q);
                                            return;
                                        }

                                        p.Subscribe(b);
                                    }
                                    break;
                                case BufferWorkType.CLOSE:
                                    {
                                        if (bs.Remove(bw.buffer))
                                        {
                                            var b = bw.buffer.buffer;
                                            if (b.Count != 0)
                                            {
                                                a.OnNext(b);
                                            }
                                        }
                                    }
                                    break;
                                default:
                                    {
                                        foreach (var buffer in bs)
                                        {
                                            buffer.buffer.Add(bw.value);
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

            bool HandleException(ISubscriber<IList<T>> a, IQueue<BufferWork> q)
            {
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
                    return true;
                }
                return false;
            }
        }

        sealed class BufferOpenSubscriber : ISubscriber<U>, ISubscription
        {
            readonly BufferOpenCloseSubscriber parent;

            ISubscription s;

            long requested;

            internal BufferOpenSubscriber(BufferOpenCloseSubscriber parent)
            {
                this.parent = parent;
            }

            public void Cancel()
            {
                SubscriptionHelper.Cancel(ref s);
            }

            public void OnComplete()
            {
                parent.OpenComplete();
            }

            public void OnError(Exception e)
            {
                parent.OpenError(e);
            }

            public void OnNext(U t)
            {
                parent.OpenNext(t);
            }

            public void OnSubscribe(ISubscription s)
            {
                BackpressureHelper.DeferredSetOnce(ref this.s, ref requested, s);
            }

            public void Request(long n)
            {
                BackpressureHelper.DeferredRequest(ref s, ref requested, n);
            }
        }

        sealed class BufferCloseSubscriber : ISubscriber<V>
        {
            readonly BufferOpenCloseSubscriber parent;

            internal readonly IList<T> buffer;

            internal readonly U u;

            ISubscription s;

            bool done;

            internal BufferCloseSubscriber(BufferOpenCloseSubscriber parent, U u)
            {
                this.parent = parent;
                this.u = u;
                this.buffer = new List<T>();
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;
                parent.CloseNext(this);
            }

            public void OnError(Exception e)
            {
                if (done)
                {
                    return;
                }
                done = true;
                parent.CloseError(e);
            }

            public void OnNext(V t)
            {
                if (done)
                {
                    return;
                }
                done = true;
                s.Cancel();
                parent.CloseNext(this);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    s.Request(long.MaxValue);
                }
            }

            internal void Cancel()
            {
                SubscriptionHelper.Cancel(ref s);
            }
        }
    }
}
