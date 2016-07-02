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
    sealed class PublisherCache<T> : IFlux<T>, ISubscriber<T>, IDisposable
    {
        readonly IPublisher<T> source;

        readonly ICacheBuffer buffer;

        TrackingArray<CacheSubscription> subscribers;

        int once;

        ISubscription s;

        internal PublisherCache(IPublisher<T> source, int history)
        {
            this.source = source;
            if (history < 0)
            {
                this.buffer = new CacheUnboundedBuffer(-history);
            }
            else
            {
                this.buffer = new CacheBoundedBuffer(history);
            }
            subscribers.Init();
        }

        public void Subscribe(ISubscriber<T> s)
        {
            var cs = new CacheSubscription(s, this);
            s.OnSubscribe(cs);

            if (subscribers.Add(cs))
            {
                if (cs.IsCancelled())
                {
                    subscribers.Remove(cs);
                    return;
                }
            }

            if (Volatile.Read(ref once) == 0 && Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                source.Subscribe(this);
            }
            buffer.Drain(cs);
        }

        public void OnSubscribe(ISubscription s)
        {
            if (SubscriptionHelper.SetOnce(ref this.s, s))
            {
                s.Request(long.MaxValue);
            }
        }

        public void OnNext(T t)
        {
            var buf = buffer;
            buf.Next(t);

            foreach (var cs in subscribers.Array())
            {
                buf.Drain(cs);
            }
        }

        public void OnError(Exception e)
        {
            var buf = buffer;
            buf.Error(e);

            foreach (var cs in subscribers.Terminate())
            {
                buf.Drain(cs);
            }
        }

        public void OnComplete()
        {
            var buf = buffer;
            buf.Complete();

            foreach (var cs in subscribers.Terminate())
            {
                buf.Drain(cs);
            }
        }

        public void Dispose()
        {
            SubscriptionHelper.Cancel(ref s);
        }

        interface ICacheBuffer
        {
            void Next(T t);

            void Error(Exception e);

            void Complete();

            void Drain(CacheSubscription cs);
        }

        sealed class CacheUnboundedBuffer : ICacheBuffer
        {
            readonly int capacity;

            readonly Node head;

            Node tail;

            int offset;

            bool done;

            Exception error;

            long available;

            internal CacheUnboundedBuffer(int capacity)
            {
                this.capacity = capacity;
                this.head = tail = new Node(capacity);
            }

            public void Complete()
            {
                Volatile.Write(ref done, true);
            }

            public void Drain(CacheSubscription cs)
            {
                if (!cs.Enter())
                {
                    return;
                }

                int missed = 1;
                var a = cs.actual;
                int c = capacity;

                for (;;)
                {
                    long r = cs.Requested();
                    long e = cs.index;
                    int o = cs.offset;
                    long j = Volatile.Read(ref available);
                    Node n = cs.node as Node;
                    if (n == null)
                    {
                        n = head;
                    }

                    while (e != j && e != r)
                    {
                        if (cs.IsCancelled())
                        {
                            cs.node = null;
                            return;
                        }

                        if (o == c)
                        {
                            n = n.lvNext();
                            o = 0;
                        }

                        a.OnNext(n.array[o]);

                        o++;
                        e++;
                    }

                    if (e == r || e == j)
                    {
                        if (cs.IsCancelled())
                        {
                            cs.node = null;
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        bool empty = Volatile.Read(ref available) == e;

                        if (d && empty)
                        {
                            cs.node = null;
                            Exception ex = error;
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

                    cs.node = n;
                    cs.index = e;
                    cs.offset = o;
                    missed = cs.Leave(missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            public void Error(Exception e)
            {
                error = e;
                Volatile.Write(ref done, true);
            }

            public void Next(T v)
            {
                var t = tail;
                var to = offset;
                if (to != capacity)
                {
                    t.array[to] = v;
                    offset = to + 1;
                }
                else
                {
                    var u = new Node(capacity);
                    u.array[0] = v;
                    offset = 1;
                    t.soNext(u);
                    t = u;
                }

                Volatile.Write(ref available, available + 1);
            }

            sealed class Node
            {
                internal readonly T[] array;

                Node next;

                internal Node(int capacity)
                {
                    this.array = new T[capacity];
                }

                internal Node lvNext()
                {
                    return Volatile.Read(ref next);
                }

                internal void soNext(Node n)
                {
                    Volatile.Write(ref next, n);
                }
            }
        }

        sealed class CacheBoundedBuffer : ICacheBuffer
        {
            readonly int capacity;

            Node head;

            Node tail;

            int size;

            bool done;

            Exception error;

            internal CacheBoundedBuffer(int capacity)
            {
                this.capacity = capacity;
                var e = new Node(default(T));
                tail = e;
                Volatile.Write(ref head, e);
            }

            public void Complete()
            {
                Volatile.Write(ref done, true);
            }

            public void Drain(CacheSubscription cs)
            {
                if (!cs.Enter())
                {
                    return;
                }

                int missed = 1;
                var a = cs.actual;

                for (;;)
                {
                    long r = cs.Requested();
                    long e = cs.index;
                    Node n = cs.node as Node;
                    if (n == null)
                    {
                        n = Volatile.Read(ref head);
                    }

                    while (e != r)
                    {
                        if (cs.IsCancelled())
                        {
                            cs.node = null;
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        var m = n.lvNext();

                        bool empty = m == null;

                        if (d && empty)
                        {
                            cs.node = null;
                            Exception ex = error;
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

                        a.OnNext(m.value);

                        e++;
                        n = m;
                    }

                    if (e == r)
                    {
                        if (cs.IsCancelled())
                        {
                            cs.node = null;
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        var m = n.lvNext();

                        bool empty = m == null;

                        if (d && empty)
                        {
                            cs.node = null;
                            Exception ex = error;
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

                    cs.index = e;
                    cs.node = n;
                    missed = cs.Leave(missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            public void Error(Exception e)
            {
                error = e;
                Volatile.Write(ref done, true);
            }

            public void Next(T v)
            {
                var n = new Node(v);
                tail.soNext(n);
                tail = n;
                int s = size;
                if (s == capacity)
                {
                    Volatile.Write(ref head, head.lpNext());
                }
                else
                {
                    size = s + 1;
                }
            }

            sealed class Node
            {
                internal readonly T value;

                internal Node next;

                internal Node(T value)
                {
                    this.value = value;
                }

                internal Node lvNext()
                {
                    return Volatile.Read(ref next);
                }

                internal Node lpNext()
                {
                    return next;
                }

                internal void soNext(Node n)
                {
                    Volatile.Write(ref next, n);
                }
            }
        }

        sealed class CacheSubscription : ISubscription
        {
            internal readonly ISubscriber<T> actual;

            internal readonly PublisherCache<T> parent;

            int cancelled;

            long requested;

            internal object node;

            internal int offset;

            internal long index;

            int wip;

            internal CacheSubscription(ISubscriber<T> actual, PublisherCache<T> parent)
            {
                this.actual = actual;
                this.parent = parent;
            }

            public void Cancel()
            {
                if (Interlocked.CompareExchange(ref cancelled, 1, 0) == 0)
                {
                    parent.subscribers.Remove(this);
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                    parent.buffer.Drain(this);
                }
            }

            internal bool IsCancelled()
            {
                return Volatile.Read(ref cancelled) != 0;
            }

            internal long Requested()
            {
                return Volatile.Read(ref requested);
            }

            internal bool Enter()
            {
                return QueueDrainHelper.Enter(ref wip);
            }

            internal int Leave(int missed)
            {
                return QueueDrainHelper.Leave(ref wip, missed);
            }
        }
    }
}
