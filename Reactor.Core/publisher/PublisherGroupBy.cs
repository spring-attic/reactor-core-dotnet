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

namespace Reactor.Core.publisher
{
    sealed class PublisherGroupBy<T, K, V> : IFlux<IGroupedFlux<K, V>>
    {
        readonly IPublisher<T> source;

        readonly Func<T, K> keySelector;

        readonly Func<T, V> valueSelector;

        readonly int prefetch;

        internal PublisherGroupBy(IPublisher<T> source, 
            Func<T, K> keySelector,
            Func<T, V> valueSelector, 
            int prefetch)
        {
            this.source = source;
            this.keySelector = keySelector;
            this.valueSelector = valueSelector;
            this.prefetch = prefetch;
        }

        public void Subscribe(ISubscriber<IGroupedFlux<K, V>> s)
        {
            source.Subscribe(new GroupBySubscriber(s, keySelector, valueSelector, prefetch));
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class GroupBySubscriber : ISubscriber<T>, IQueueSubscription<IGroupedFlux<K, V>>
        {
            readonly ISubscriber<IGroupedFlux<K, V>> actual;

            readonly Func<T, K> keySelector;

            readonly Func<T, V> valueSelector;

            readonly int prefetch;

            readonly IQueue<IGroupedFlux<K, V>> queue;

            ISubscription s;

            Dictionary<K, GroupUnicast> groups;

            bool outputFused;

            int groupCount;

            bool done;

            Exception error;

            int cancelled;

            long requested;

            Pad128 p0;

            int wip;

            Pad120 p1;

            internal GroupBySubscriber(ISubscriber<IGroupedFlux<K, V>> actual,
                Func<T, K> keySelector,
                Func<T, V> valueSelector,
                int prefetch)
            {
                this.actual = actual;
                this.keySelector = keySelector;
                this.valueSelector = valueSelector;
                this.prefetch = prefetch;
                this.groupCount = 1;
                this.groups = new Dictionary<K, GroupUnicast>();
                this.queue = new SpscLinkedArrayQueue<IGroupedFlux<K, V>>(prefetch);
            }

            internal void InnerConsumed(long n)
            {
                s.Request(n);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    actual.OnSubscribe(this);

                    s.Request(prefetch);
                }
            }

            public void OnNext(T t)
            {
                if (done)
                {
                    return;
                }
                K key;

                V value;

                try
                {
                    key = keySelector(t);

                    value = valueSelector(t);
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowIfFatal(ex);
                    s.Cancel();
                    OnError(ex);
                    return;
                }

                GroupUnicast g;
                bool newGroup = false;

                lock (this)
                {
                    if (!groups.TryGetValue(key, out g))
                    {
                        if (Volatile.Read(ref cancelled) != 0)
                        {
                            return;
                        }

                        Interlocked.Increment(ref groupCount);
                        g = new GroupUnicast(this, key, prefetch);
                        groups.Add(key, g);
                        newGroup = true;
                    }
                }


                g.OnNext(value);

                if (newGroup)
                {
                    queue.Offer(g);
                    Drain();
                }
            }

            public void OnError(Exception e)
            {
                Dictionary<K, GroupUnicast> g;
                lock (this)
                {
                    g = groups;
                    if (g == null)
                    {
                        return;
                    }
                    groups = null;
                }
                foreach (var u in g.Values)
                {
                    u.OnError(e);
                }

                error = e;
                Volatile.Write(ref done, true);
                Drain();
            }

            public void OnComplete()
            {
                Dictionary<K, GroupUnicast> g;
                lock (this)
                {
                    g = groups;
                    if (g == null)
                    {
                        return;
                    }
                    groups = null;
                }
                foreach (var u in g.Values)
                {
                    u.OnComplete();
                }

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

            public void Cancel()
            {
                if (Interlocked.CompareExchange(ref cancelled, 1, 0) == 0)
                {
                    DoCancel();
                }
            }

            internal void InnerCancelled(K key)
            {
                lock(this)
                {
                    var gs = groups;
                    if (gs != null && gs.ContainsKey(key))
                    {
                        gs.Remove(key);
                    }
                }
                DoCancel();
            }

            void DoCancel()
            {
                if (Interlocked.Decrement(ref groupCount) == 0)
                {
                    s.Cancel();

                    if (QueueDrainHelper.Enter(ref wip))
                    {
                        queue.Clear();
                    }
                }
            }

            void Drain()
            {
                if (QueueDrainHelper.Enter(ref wip))
                {
                    if (outputFused)
                    {
                        DrainFused();
                    }
                    else
                    {
                        DrainNormal();
                    }
                }
            }

            void DrainNormal()
            {
                int missed = 1;
                var a = actual;
                var q = queue;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    long e = 0L;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled) != 0)
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        IGroupedFlux<K, V> t;

                        bool empty = !q.Poll(out t);

                        if (d && empty)
                        {
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
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled) != 0)
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        bool empty = q.IsEmpty();

                        if (d && empty)
                        {
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

                    if (e != 0L)
                    {
                        if (r != long.MaxValue)
                        {
                            Interlocked.Add(ref requested, -e);
                        }
                        s.Request(e);
                    }

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            void DrainFused()
            {
                int missed = 1;
                var a = actual;
                var q = queue;

                for (;;)
                {
                    if (Volatile.Read(ref cancelled) != 0)
                    {
                        q.Clear();
                        return;
                    }

                    bool d = Volatile.Read(ref done);

                    a.OnNext(null);

                    if (d)
                    {
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

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            public int RequestFusion(int mode)
            {
                if ((mode & FuseableHelper.ASYNC) != 0)
                {
                    outputFused = true;
                    return FuseableHelper.ASYNC;
                }
                return FuseableHelper.NONE;
            }

            public bool Offer(IGroupedFlux<K, V> value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out IGroupedFlux<K, V> value)
            {
                return queue.Poll(out value);
            }

            public bool IsEmpty()
            {
                return queue.IsEmpty();
            }

            public void Clear()
            {
                queue.Clear();
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class GroupUnicast : IGroupedFlux<K, V>, IQueueSubscription<V>
        {
            readonly GroupBySubscriber parent;

            readonly K key;

            readonly IQueue<V> queue;

            ISubscriber<V> regular;

            IConditionalSubscriber<V> conditional;

            int once;

            bool outputFused;

            bool done;

            Exception error;

            int cancelled;

            long requested;

            int produced;

            Pad128 p0;

            int wip;

            Pad120 p1;

            public K Key
            {
                get
                {
                    return key;
                }
            }

            internal GroupUnicast(GroupBySubscriber parent, K key, int prefetch)
            {
                this.parent = parent;
                this.key = key;
                this.queue = new SpscLinkedArrayQueue<V>(prefetch);
            }

            public void Subscribe(ISubscriber<V> s)
            {
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                {
                    s.OnSubscribe(this);
                    if (s is IConditionalSubscriber<V>)
                    {
                        Volatile.Write(ref conditional, (IConditionalSubscriber<V>)s);
                        if (Volatile.Read(ref cancelled) != 0)
                        {
                            conditional = null;
                            return;
                        }
                    }
                    else
                    {
                        Volatile.Write(ref regular, s);
                        if (Volatile.Read(ref cancelled) != 0)
                        {
                            regular = null;
                            return;
                        }
                    }
                    Drain();
                }
                else
                {
                    EmptySubscription<V>.Error(s, new InvalidOperationException("The IGroupedFlux allows only a single ISubscriber!"));
                }
            }

            internal void OnNext(V v)
            {
                queue.Offer(v);
                Drain();
            }

            internal void OnError(Exception e)
            {
                error = e;
                Volatile.Write(ref done, true);
                Drain();
            }

            internal void OnComplete()
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

            public void Cancel()
            {
                if (Interlocked.CompareExchange(ref cancelled, 1, 0) == 0)
                {
                    parent.InnerCancelled(key);
                }
            }

            void Drain()
            {
                if (outputFused)
                {
                    DrainFused();
                }
                else
                {
                    DrainNormal();
                }
            }

            void DrainNormal()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                int missed = 1;
                var a = Volatile.Read(ref regular);
                var b = Volatile.Read(ref conditional);
                var q = queue;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    long e = 0L;

                    if (a != null)
                    {

                        while (e != r)
                        {
                            if (Volatile.Read(ref cancelled) != 0)
                            {
                                regular = null;
                                q.Clear();
                                return;
                            }

                            bool d = Volatile.Read(ref done);

                            V v;

                            bool empty = !q.Poll(out v);

                            if (d && empty)
                            {
                                regular = null;
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

                            a.OnNext(v);

                            e++;
                        }

                        if (e == r)
                        {
                            if (Volatile.Read(ref cancelled) != 0)
                            {
                                regular = null;
                                q.Clear();
                                return;
                            }

                            bool d = Volatile.Read(ref done);

                            bool empty = q.IsEmpty();

                            if (d && empty)
                            {
                                regular = null;
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

                        if (e != 0L)
                        {
                            if (r != long.MaxValue)
                            {
                                Interlocked.Add(ref requested, -e);
                            }
                            parent.InnerConsumed(e);
                        }
                    }

                    if (b != null)
                    {
                        while (e != r)
                        {
                            if (Volatile.Read(ref cancelled) != 0)
                            {
                                conditional = null;
                                q.Clear();
                                return;
                            }

                            bool d = Volatile.Read(ref done);

                            V v;

                            bool empty = !q.Poll(out v);

                            if (d && empty)
                            {
                                conditional = null;
                                var ex = error;
                                if (ex != null)
                                {
                                    b.OnError(ex);
                                }
                                else
                                {
                                    b.OnComplete();
                                }
                                return;
                            }

                            if (empty)
                            {
                                break;
                            }

                            if (b.TryOnNext(v))
                            {
                                e++;
                            }
                        }

                        if (e == r)
                        {
                            if (Volatile.Read(ref cancelled) != 0)
                            {
                                conditional = null;
                                q.Clear();
                                return;
                            }

                            bool d = Volatile.Read(ref done);

                            bool empty = q.IsEmpty();

                            if (d && empty)
                            {
                                conditional = null;
                                var ex = error;
                                if (ex != null)
                                {
                                    b.OnError(ex);
                                }
                                else
                                {
                                    b.OnComplete();
                                }
                                return;
                            }
                        }
                    }

                    if (e != 0L)
                    {
                        if (r != long.MaxValue)
                        {
                            Interlocked.Add(ref requested, -e);
                        }
                        parent.InnerConsumed(e);
                    }

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                    if (a == null && b == null)
                    {
                        a = Volatile.Read(ref regular);
                        b = Volatile.Read(ref conditional);
                    }
                }
            }

            void DrainFused()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                int missed = 1;
                var a = Volatile.Read(ref regular);
                var b = Volatile.Read(ref conditional);
                var q = queue;

                for (;;)
                {
                    if (a != null)
                    {
                        if (Volatile.Read(ref cancelled) != 0)
                        {
                            regular = null;
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        a.OnNext(default(V));

                        if (d)
                        {
                            regular = null;
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

                    if (b != null)
                    {
                        if (Volatile.Read(ref cancelled) != 0)
                        {
                            conditional = null;
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        b.TryOnNext(default(V));

                        if (d)
                        {
                            conditional = null;
                            var ex = error;
                            if (ex != null)
                            {
                                b.OnError(ex);
                            }
                            else
                            {
                                b.OnComplete();
                            }
                            return;
                        }
                    }

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                    if (a == null && b == null)
                    {
                        a = Volatile.Read(ref regular);
                        b = Volatile.Read(ref conditional);
                    }
                }
            }

            public int RequestFusion(int mode)
            {
                if ((mode & FuseableHelper.ASYNC) != 0)
                {
                    outputFused = true;
                    return FuseableHelper.ASYNC;
                }
                return FuseableHelper.NONE;
            }

            public bool Offer(V value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out V value)
            {
                bool b = queue.Poll(out value);
                if (b)
                {
                    produced++;
                }
                else
                {
                    int p = produced;
                    if (p != 0)
                    {
                        produced = 0;
                        parent.InnerConsumed(p);
                    }
                }
                return b;
            }

            public bool IsEmpty()
            {
                return queue.IsEmpty();
            }

            public void Clear()
            {
                queue.Clear();
            }
        }
    }
}
