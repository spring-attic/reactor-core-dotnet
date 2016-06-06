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
    sealed class PublisherCombineLatest<T, R> : IFlux<R>, IFuseable
    {
        readonly IPublisher<T>[] sources;

        readonly IEnumerable<IPublisher<T>> sourcesEnumerable;

        readonly int prefetch;

        readonly bool delayError;

        readonly Func<T[], R> combiner;

        internal PublisherCombineLatest(IPublisher<T>[] sources, 
            IEnumerable<IPublisher<T>> sourceEnumerable,
            int prefetch, bool delayError, Func<T[], R> combiner)
        {
            this.sources = sources;
            this.sourcesEnumerable = sourceEnumerable;
            this.prefetch = prefetch;
            this.delayError = delayError;
            this.combiner = combiner;
        }

        public void Subscribe(ISubscriber<R> s)
        {
            int n;
            IPublisher<T>[] a;

            if (!MultiSourceHelper.ToArray(sources, sourcesEnumerable, s, out n, out a))
            {
                return;
            }

            if (n == 0)
            {
                EmptySubscription<R>.Complete(s);
                return;
            }
            else
            if (n == 1)
            {
                new PublisherMap<T, R>(a[0], v => combiner(new T[] { v })).Subscribe(s);
                return;
            }

            if (s is IConditionalSubscriber<R>)
            {
                CombineLatestConditionalSubscription parent = new CombineLatestConditionalSubscription((IConditionalSubscriber<R>)s, prefetch, delayError, combiner, n);

                s.OnSubscribe(parent);

                parent.Subscribe(a, n);
            }
            else
            {
                CombineLatestSubscription parent = new CombineLatestSubscription(s, prefetch, delayError, combiner, n);

                s.OnSubscribe(parent);

                parent.Subscribe(a, n);
            }

        }

        interface IComineLatestParent
        {
            void InnerNext(CombineLatestSubscriber sender, int index, T value);

            void InnerError(CombineLatestSubscriber sender, Exception e);

            void InnerComplete(CombineLatestSubscriber sender);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class CombineLatestSubscription : IQueueSubscription<R>, IComineLatestParent
        {
            // COLD fields

            readonly ISubscriber<R> actual;

            readonly int prefetch;

            readonly bool delayError;

            readonly Func<T[], R> combiner;

            readonly IQueue<Entry> queue;

            readonly CombineLatestSubscriber[] subscribers;

            readonly T[] latest;

            bool outputFused;

            Pad128 p00;

            // Terminal fields

            bool cancelled;

            bool hasEmptySource;

            Exception error;

            // HOT fields

            Pad104 p0;

            int wip;

            Pad120 p2;

            long requested;

            Pad120 p3;

            int active;

            Pad120 p4;

            int complete;

            Pad120 p5;

            internal CombineLatestSubscription(ISubscriber<R> actual, int prefetch, bool delayError, Func<T[], R> combiner, int n)
            {
                this.actual = actual;
                this.prefetch = prefetch;
                this.delayError = delayError;
                this.combiner = combiner;
                this.latest = new T[n];
                var a = new CombineLatestSubscriber[n];
                for (int i = 0; i < n; i++)
                {
                    a[i] = new CombineLatestSubscriber(this, i, prefetch);
                }
                this.subscribers = a;
                this.queue = new SpscLinkedArrayQueue<Entry>(prefetch);
            }

            public void Subscribe(IPublisher<T>[] sources, int n)
            {
                var a = subscribers;
                for (int i = 0; i < n; i++)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }

                    sources[i].Subscribe(a[i]);
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.ValidateAndAddCap(ref requested, n);
                    Drain();
                }
            }

            public void Cancel()
            {
                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                Volatile.Write(ref cancelled, true);
                CancelAll();

                if (QueueDrainHelper.Enter(ref wip))
                {
                    Cleanup();
                }
            }

            void CancelAll()
            {
                foreach (var inner in subscribers)
                {
                    inner.Cancel();
                }
            }

            void Cleanup()
            {
                var a = latest;
                int n = a.Length;
                Array.Clear(a, 0, n);
                queue.Clear();
            }

            public void InnerNext(CombineLatestSubscriber sender, int index, T value)
            {
                var a = latest;
                int n = a.Length;

                int activeCount = Volatile.Read(ref active);

                if (!sender.hasValue)
                {
                    sender.hasValue = true;
                    activeCount = Interlocked.Increment(ref active);
                }

                bool drain = activeCount == n;

                lock (this) {
                    a[index] = value;

                    if (drain)
                    {
                        T[] b = new T[n];
                        Array.Copy(a, 0, b, 0, n);
                        queue.Offer(new Entry(b, sender));
                    }
                }

                if (drain)
                {
                    Drain();
                }
                else
                {
                    sender.RequestOne();
                }
            }

            public void InnerError(CombineLatestSubscriber sender, Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    InnerComplete(sender);
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public void InnerComplete(CombineLatestSubscriber sender)
            {
                if (!sender.hasValue)
                {
                    CancelAll();
                    Volatile.Write(ref hasEmptySource, true);
                }
                Interlocked.Increment(ref complete);
                Drain();
            }

            void DrainNoDelay()
            {
                var a = actual;
                var c = latest;
                int n = c.Length;
                var q = queue;

                int missed = 1;

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        Cleanup();
                        return;
                    }

                    if (Volatile.Read(ref hasEmptySource))
                    {
                        Exception ex = ExceptionHelper.Terminate(ref error);
                        Cleanup();
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

                    long r = Volatile.Read(ref requested);
                    long e = 0L;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Cleanup();
                            return;
                        }

                        Exception ex = Volatile.Read(ref error);

                        if (ex != null)
                        {
                            ex = ExceptionHelper.Terminate(ref error);
                            Cleanup();
                            a.OnError(ex);
                            return;
                        }

                        bool d = Volatile.Read(ref complete) == n;

                        Entry v;
                        bool empty = q.Poll(out v);

                        if (d && empty)
                        {
                            Cleanup();
                            a.OnComplete();
                            return;
                        }

                        R result;

                        try
                        {
                            result = combiner(v.row);
                        }
                        catch (Exception exc)
                        {
                            ExceptionHelper.ThrowIfFatal(exc);

                            CancelAll();
                            Cleanup();

                            ExceptionHelper.AddError(ref error, exc);
                            exc = ExceptionHelper.Terminate(ref error);

                            a.OnError(exc);
                            return;
                        }

                        a.OnNext(result);

                        e++;
                        v.sender.RequestOne();
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Cleanup();
                            return;
                        }

                        Exception ex = Volatile.Read(ref error);

                        if (ex != null)
                        {
                            ex = ExceptionHelper.Terminate(ref error);
                            Cleanup();
                            a.OnError(ex);
                            return;
                        }

                        if (Volatile.Read(ref complete) == n && q.IsEmpty())
                        {
                            Cleanup();

                            a.OnComplete();
                            return;
                        }
                    }

                    if (e != 0 && r != long.MaxValue)
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

            void DrainDelay()
            {
                var a = actual;
                var c = latest;
                int n = c.Length;
                var q = queue;

                int missed = 1;

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        Cleanup();
                        return;
                    }

                    if (Volatile.Read(ref hasEmptySource))
                    {
                        Exception ex = ExceptionHelper.Terminate(ref error);
                        Cleanup();
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

                    long r = Volatile.Read(ref requested);
                    long e = 0L;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Cleanup();
                            return;
                        }

                        bool d = Volatile.Read(ref complete) == n;

                        Entry v;
                        bool empty = q.Poll(out v);

                        if (d && empty)
                        {
                            Cleanup();

                            Exception ex = ExceptionHelper.Terminate(ref error);

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

                        R result;

                        try
                        {
                            result = combiner(v.row);
                        }
                        catch (Exception exc)
                        {
                            ExceptionHelper.ThrowIfFatal(exc);

                            CancelAll();
                            Cleanup();

                            ExceptionHelper.AddError(ref error, exc);
                            exc = ExceptionHelper.Terminate(ref error);

                            a.OnError(exc);
                            return;
                        }

                        a.OnNext(result);

                        e++;
                        v.sender.RequestOne();
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Cleanup();
                            return;
                        }

                        if (Volatile.Read(ref complete) == n && q.IsEmpty())
                        {
                            Cleanup();

                            Exception ex = ExceptionHelper.Terminate(ref error);

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

                    if (e != 0 && r != long.MaxValue)
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

            void DrainOutput()
            {
                int missed = 1;

                var a = actual;
                var q = queue;
                int n = latest.Length;

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        Cleanup();
                        return;
                    }

                    if (Volatile.Read(ref hasEmptySource))
                    {
                        Cleanup();

                        Exception ex = ExceptionHelper.Terminate(ref error);
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

                    if (!delayError)
                    {
                        Exception ex = Volatile.Read(ref error);
                        if (ex != null)
                        {
                            Cleanup();
                            ex = ExceptionHelper.Terminate(ref error);

                            a.OnError(ex);
                            return;
                        }
                    }

                    bool d = Volatile.Read(ref complete) == n;

                    bool empty = q.IsEmpty();
                    if (!empty)
                    {
                        a.OnNext(default(R));
                    }

                    if (d && empty)
                    {
                        Cleanup();
                        Exception ex = ExceptionHelper.Terminate(ref error);
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

            void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                if (outputFused)
                {
                    DrainOutput();
                }
                else
                if (delayError)
                {
                    DrainDelay();
                }
                else
                {
                    DrainNoDelay();
                }
            }

            public int RequestFusion(int mode)
            {
                if ((mode & FuseableHelper.BOUNDARY) != 0)
                {
                    return FuseableHelper.NONE;
                }
                int m = mode & FuseableHelper.ASYNC;
                outputFused = m != 0;
                return m;
            }

            public bool Offer(R value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out R value)
            {
                Entry e;

                if (queue.Poll(out e))
                {
                    value = combiner(e.row);

                    e.sender.RequestOne();
                    return true;
                }
                value = default(R);
                return false;
            }

            public bool IsEmpty()
            {
                return queue.IsEmpty();
            }

            public void Clear()
            {
                Cleanup();
            }
        }

        struct Entry
        {
            internal T[] row;

            internal CombineLatestSubscriber sender;

            internal Entry(T[] sourceRow, CombineLatestSubscriber sender)
            {
                this.row = sourceRow;
                this.sender = sender;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class CombineLatestConditionalSubscription : IQueueSubscription<R>, IComineLatestParent
        {
            // COLD fields

            readonly IConditionalSubscriber<R> actual;

            readonly int prefetch;

            readonly bool delayError;

            readonly Func<T[], R> combiner;

            readonly IQueue<Entry> queue;

            readonly CombineLatestSubscriber[] subscribers;

            readonly T[] latest;

            bool outputFused;

            Pad128 p00;

            // Terminal fields

            bool cancelled;

            bool hasEmptySource;

            Exception error;

            // HOT fields

            Pad104 p0;

            int wip;

            Pad120 p2;

            long requested;

            Pad120 p3;

            int active;

            Pad120 p4;

            int complete;

            Pad120 p5;

            internal CombineLatestConditionalSubscription(IConditionalSubscriber<R> actual, int prefetch, bool delayError, Func<T[], R> combiner, int n)
            {
                this.actual = actual;
                this.prefetch = prefetch;
                this.delayError = delayError;
                this.combiner = combiner;
                this.latest = new T[n];
                var a = new CombineLatestSubscriber[n];
                for (int i = 0; i < n; i++)
                {
                    a[i] = new CombineLatestSubscriber(this, i, prefetch);
                }
                this.subscribers = a;
                this.queue = new SpscLinkedArrayQueue<Entry>(prefetch);
            }

            public void Subscribe(IPublisher<T>[] sources, int n)
            {
                var a = subscribers;
                for (int i = 0; i < n; i++)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }

                    sources[i].Subscribe(a[i]);
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.ValidateAndAddCap(ref requested, n);
                    Drain();
                }
            }

            public void Cancel()
            {
                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                Volatile.Write(ref cancelled, true);
                CancelAll();

                if (QueueDrainHelper.Enter(ref wip))
                {
                    Cleanup();
                }
            }

            void CancelAll()
            {
                foreach (var inner in subscribers)
                {
                    inner.Cancel();
                }
            }

            void Cleanup()
            {
                var a = latest;
                int n = a.Length;
                Array.Clear(a, 0, n);
                queue.Clear();
            }

            public void InnerNext(CombineLatestSubscriber sender, int index, T value)
            {
                var a = latest;
                int n = a.Length;

                int activeCount = Volatile.Read(ref active);

                if (!sender.hasValue)
                {
                    sender.hasValue = true;
                    activeCount = Interlocked.Increment(ref active);
                }

                bool drain = activeCount == n;

                lock (this)
                {
                    a[index] = value;

                    if (drain)
                    {
                        T[] b = new T[n];
                        Array.Copy(a, 0, b, 0, n);
                        queue.Offer(new Entry(b, sender));
                    }
                }

                if (drain)
                {
                    Drain();
                }
                else
                {
                    sender.RequestOne();
                }
            }

            public void InnerError(CombineLatestSubscriber sender, Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    InnerComplete(sender);
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public void InnerComplete(CombineLatestSubscriber sender)
            {
                if (!sender.hasValue)
                {
                    CancelAll();
                    Volatile.Write(ref hasEmptySource, true);
                }
                Interlocked.Increment(ref complete);
                Drain();
            }

            void DrainNoDelay()
            {
                var a = actual;
                var c = latest;
                int n = c.Length;
                var q = queue;

                int missed = 1;

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        Cleanup();
                        return;
                    }

                    if (Volatile.Read(ref hasEmptySource))
                    {
                        Exception ex = ExceptionHelper.Terminate(ref error);
                        Cleanup();
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

                    long r = Volatile.Read(ref requested);
                    long e = 0L;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Cleanup();
                            return;
                        }

                        Exception ex = Volatile.Read(ref error);

                        if (ex != null)
                        {
                            ex = ExceptionHelper.Terminate(ref error);
                            Cleanup();
                            a.OnError(ex);
                            return;
                        }

                        bool d = Volatile.Read(ref complete) == n;

                        Entry v;
                        bool empty = q.Poll(out v);

                        if (d && empty)
                        {
                            Cleanup();
                            a.OnComplete();
                            return;
                        }

                        R result;

                        try
                        {
                            result = combiner(v.row);
                        }
                        catch (Exception exc)
                        {
                            ExceptionHelper.ThrowIfFatal(exc);

                            CancelAll();
                            Cleanup();

                            ExceptionHelper.AddError(ref error, exc);
                            exc = ExceptionHelper.Terminate(ref error);

                            a.OnError(exc);
                            return;
                        }

                        if (a.TryOnNext(result))
                        {
                            e++;
                        }
                        v.sender.RequestOne();
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Cleanup();
                            return;
                        }

                        Exception ex = Volatile.Read(ref error);

                        if (ex != null)
                        {
                            ex = ExceptionHelper.Terminate(ref error);
                            Cleanup();
                            a.OnError(ex);
                            return;
                        }

                        if (Volatile.Read(ref complete) == n && q.IsEmpty())
                        {
                            Cleanup();

                            a.OnComplete();
                            return;
                        }
                    }

                    if (e != 0 && r != long.MaxValue)
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

            void DrainDelay()
            {
                var a = actual;
                var c = latest;
                int n = c.Length;
                var q = queue;

                int missed = 1;

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        Cleanup();
                        return;
                    }

                    if (Volatile.Read(ref hasEmptySource))
                    {
                        Exception ex = ExceptionHelper.Terminate(ref error);
                        Cleanup();
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

                    long r = Volatile.Read(ref requested);
                    long e = 0L;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Cleanup();
                            return;
                        }

                        bool d = Volatile.Read(ref complete) == n;

                        Entry v;
                        bool empty = q.Poll(out v);

                        if (d && empty)
                        {
                            Cleanup();

                            Exception ex = ExceptionHelper.Terminate(ref error);

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

                        R result;

                        try
                        {
                            result = combiner(v.row);
                        }
                        catch (Exception exc)
                        {
                            ExceptionHelper.ThrowIfFatal(exc);

                            CancelAll();
                            Cleanup();

                            ExceptionHelper.AddError(ref error, exc);
                            exc = ExceptionHelper.Terminate(ref error);

                            a.OnError(exc);
                            return;
                        }

                        if (a.TryOnNext(result))
                        {
                            e++;
                        }

                        v.sender.RequestOne();
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Cleanup();
                            return;
                        }

                        if (Volatile.Read(ref complete) == n && q.IsEmpty())
                        {
                            Cleanup();

                            Exception ex = ExceptionHelper.Terminate(ref error);

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

                    if (e != 0 && r != long.MaxValue)
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

            void DrainOutput()
            {
                int missed = 1;

                var a = actual;
                var q = queue;
                int n = latest.Length;

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        Cleanup();
                        return;
                    }

                    if (Volatile.Read(ref hasEmptySource))
                    {
                        Cleanup();

                        Exception ex = ExceptionHelper.Terminate(ref error);
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

                    if (!delayError)
                    {
                        Exception ex = Volatile.Read(ref error);
                        if (ex != null)
                        {
                            Cleanup();
                            ex = ExceptionHelper.Terminate(ref error);

                            a.OnError(ex);
                            return;
                        }
                    }

                    bool d = Volatile.Read(ref complete) == n;

                    bool empty = q.IsEmpty();
                    if (!empty)
                    {
                        a.TryOnNext(default(R));
                    }

                    if (d && empty)
                    {
                        Cleanup();
                        Exception ex = ExceptionHelper.Terminate(ref error);
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

            void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                if (outputFused)
                {
                    DrainOutput();
                }
                else
                if (delayError)
                {
                    DrainDelay();
                }
                else
                {
                    DrainNoDelay();
                }
            }

            public int RequestFusion(int mode)
            {
                if ((mode & FuseableHelper.BOUNDARY) != 0)
                {
                    return FuseableHelper.NONE;
                }
                int m = mode & FuseableHelper.ASYNC;
                outputFused = m != 0;
                return m;
            }

            public bool Offer(R value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out R value)
            {
                Entry e;

                if (queue.Poll(out e))
                {
                    value = combiner(e.row);

                    e.sender.RequestOne();
                    return true;
                }
                value = default(R);
                return false;
            }

            public bool IsEmpty()
            {
                return queue.IsEmpty();
            }

            public void Clear()
            {
                Cleanup();
            }
        }

        sealed class CombineLatestSubscriber : ISubscriber<T>
        {
            readonly IComineLatestParent parent;

            readonly int index;

            readonly int prefetch;

            readonly int limit;

            ISubscription s;

            int produced;

            internal bool hasValue;

            internal CombineLatestSubscriber(IComineLatestParent parent, int index, int prefetch)
            {
                this.parent = parent;
                this.index = index;
                this.prefetch = prefetch;
                this.limit = prefetch - (prefetch >> 2);
            }

            internal void RequestOne()
            {
                int p = produced + 1;
                if (p == limit)
                {
                    produced = 0;
                    s.Request(p);
                }
            }

            internal void Cancel()
            {
                SubscriptionHelper.Cancel(ref s);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    s.Request(prefetch);
                }
            }

            public void OnNext(T t)
            {
                parent.InnerNext(this, index, t);
            }

            public void OnError(Exception e)
            {
                parent.InnerError(this, e);
            }

            public void OnComplete()
            {
                parent.InnerComplete(this);
            }
        }
    }
}
