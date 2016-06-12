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
    sealed class PublisherZip<T, R> : IFlux<R>
    {
        readonly IPublisher<T>[] sources;

        readonly IEnumerable<IPublisher<T>> sourcesEnumerable;

        readonly Func<T[], R> zipper;

        readonly bool delayErrors;

        readonly int prefetch;

        internal PublisherZip(IPublisher<T>[] sources, IEnumerable<IPublisher<T>> sourcesEnumerable, Func<T[], R> zipper,
            bool delayErrors, int prefetch)
        {
            this.sources = sources;
            this.sourcesEnumerable = sourcesEnumerable;
            this.zipper = zipper;
            this.delayErrors = delayErrors;
            this.prefetch = prefetch;
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
                new PublisherMap<T, R>(a[0], v => zipper(new T[] { v })).Subscribe(s);
                return;
            }

            if (s is IConditionalSubscriber<R>)
            {
                var parent = new ZipConditionalSubscription((IConditionalSubscriber<R>)s, n, zipper, prefetch, delayErrors);

                s.OnSubscribe(parent);

                parent.Subscribe(a, n);
            }
            else
            {
                var parent = new ZipSubscription(s, n, zipper, prefetch, delayErrors);

                s.OnSubscribe(parent);

                parent.Subscribe(a, n);
            }
        }

        interface ZipParent
        {
            void Drain();

            void InnerError(ZipInnerSubscriber inner, Exception ex);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class ZipSubscription : ISubscription, ZipParent
        {
            readonly ISubscriber<R> actual;

            readonly ZipInnerSubscriber[] subscribers;

            readonly Func<T[], R> zipper;

            readonly T[] current;

            readonly bool delayErrors;

            bool cancelled;

            Exception error;



            Pad128 p0;

            int wip;

            Pad120 p1;

            long requested;

            Pad120 p2;

            internal ZipSubscription(ISubscriber<R> actual, int n, Func<T[], R> zipper, int prefetch, bool delayErrors)
            {
                this.actual = actual;
                this.zipper = zipper;

                this.current = new T[n];
                var a = new ZipInnerSubscriber[n];

                for (int i = 0; i < n; i++)
                {
                    a[i] = new ZipInnerSubscriber(this, prefetch);
                }

                this.subscribers = a;
                this.delayErrors = delayErrors;
            }

            internal void Subscribe(IPublisher<T>[] sources, int n)
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
                    ClearAll();
                    ClearCurrent();
                }
            }

            void ClearAll()
            {
                foreach (var inner in subscribers)
                {
                    inner.Clear();
                }
            }

            void CancelAll()
            {
                foreach (var inner in subscribers)
                {
                    inner.Cancel();
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                    Drain();
                }
            }

            public void InnerError(ZipInnerSubscriber inner, Exception ex)
            {
                if (ExceptionHelper.AddError(ref error, ex))
                {
                    inner.SetDone();

                    if (!delayErrors)
                    {
                        CancelAll();
                    }

                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(ex);
                }
            }

            void ClearCurrent()
            {
                var a = current;
                int n = a.Length;
                for (int i = 0; i < n; i++)
                {
                    a[i] = default(T);
                }
            }

            public void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                var a = actual;
                var s = subscribers;
                var c = current;
                int n = s.Length;
                int missed = 1;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    long e = 0L;

                    for (;;)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            ClearAll();
                            ClearCurrent();
                            return;
                        }
                        if (!delayErrors)
                        {
                            Exception ex = Volatile.Read(ref error);
                            if (ex != null)
                            {
                                ex = ExceptionHelper.Terminate(ref error);
                                ClearAll();
                                ClearCurrent();

                                a.OnError(ex);
                                return;
                            }
                        }


                        int done = 0;
                        int hasValue = 0;

                        for (int i = 0; i < n; i++)
                        {

                            var inner = s[i];

                            if (inner.IsDone())
                            {
                                done++;

                                if (!inner.NonEmpty())
                                {
                                    CancelAll();
                                    ClearAll();
                                    ClearCurrent();

                                    a.OnComplete();
                                    return;
                                }
                            }

                            if (inner.HasValue())
                            {
                                hasValue++;
                            }
                            else
                            {
                                if (inner.Poll(out c[i]))
                                {
                                    inner.HasValue(true);
                                    hasValue++;
                                }
                            }

                        }

                        if (done == n && hasValue != n)
                        {
                            Exception ex = Volatile.Read(ref error);
                            if (ex != null)
                            {
                                ex = ExceptionHelper.Terminate(ref error);
                                ClearCurrent();

                                a.OnError(ex);
                            }
                            else
                            {
                                a.OnComplete();
                            }

                            return;
                        }

                        if (hasValue != n)
                        {
                            break;
                        }

                        if (e == r)
                        {
                            break;
                        }

                        var row = new T[n];

                        for (int i = 0; i < n; i++)
                        {
                            row[i] = current[i];

                            var inner = s[i];
                            inner.HasValue(false);
                            inner.RequestOne();
                        }

                        R t;

                        try
                        {
                            t = zipper(row);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);
                            ExceptionHelper.AddError(ref error, ex);

                            ex = ExceptionHelper.Terminate(ref error);

                            CancelAll();
                            ClearAll();
                            ClearCurrent();

                            a.OnError(ex);
                            return;
                        }

                        a.OnNext(t);

                        e++;
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
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class ZipConditionalSubscription : ISubscription, ZipParent
        {
            readonly IConditionalSubscriber<R> actual;

            readonly ZipInnerSubscriber[] subscribers;

            readonly Func<T[], R> zipper;

            readonly T[] current;

            readonly bool delayErrors;

            bool cancelled;

            Exception error;



            Pad128 p0;

            int wip;

            Pad120 p1;

            long requested;

            Pad120 p2;

            internal ZipConditionalSubscription(IConditionalSubscriber<R> actual, int n, Func<T[], R> zipper, int prefetch, bool delayErrors)
            {
                this.actual = actual;
                this.zipper = zipper;

                this.current = new T[n];
                var a = new ZipInnerSubscriber[n];

                for (int i = 0; i < n; i++)
                {
                    a[i] = new ZipInnerSubscriber(this, prefetch);
                }

                this.subscribers = a;
                this.delayErrors = delayErrors;
            }

            internal void Subscribe(IPublisher<T>[] sources, int n)
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
                    ClearAll();
                }
            }

            void ClearAll()
            {
                foreach (var inner in subscribers)
                {
                    inner.Clear();
                }
            }

            void CancelAll()
            {
                foreach (var inner in subscribers)
                {
                    inner.Cancel();
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                    Drain();
                }
            }

            public void InnerError(ZipInnerSubscriber inner, Exception ex)
            {
                if (ExceptionHelper.AddError(ref error, ex))
                {
                    inner.SetDone();

                    if (!delayErrors)
                    {
                        CancelAll();
                    }

                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(ex);
                }
            }

            void ClearCurrent()
            {
                var a = current;
                int n = a.Length;
                for (int i = 0; i < n; i++)
                {
                    a[i] = default(T);
                }
            }

            public void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                var a = actual;
                var s = subscribers;
                var c = current;
                int n = s.Length;
                int missed = 1;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    long e = 0L;

                    for (;;)
                    {
                        if (!delayErrors)
                        {
                            Exception ex = Volatile.Read(ref error);
                            if (ex != null)
                            {
                                ex = ExceptionHelper.Terminate(ref error);
                                ClearAll();
                                ClearCurrent();

                                a.OnError(ex);
                                return;
                            }
                        }


                        int done = 0;
                        int hasValue = 0;

                        for (int i = 0; i < n; i++)
                        {

                            var inner = s[i];

                            if (inner.IsDone())
                            {
                                done++;

                                if (!inner.NonEmpty())
                                {
                                    CancelAll();
                                    ClearAll();
                                    ClearCurrent();

                                    a.OnComplete();
                                    return;
                                }
                            }

                            if (inner.HasValue())
                            {
                                hasValue++;
                            }
                            else
                            {
                                if (inner.Poll(out c[i]))
                                {
                                    inner.HasValue(true);
                                    hasValue++;
                                }
                            }

                        }

                        if (done == n && hasValue != n)
                        {
                            Exception ex = Volatile.Read(ref error);
                            if (ex != null)
                            {
                                ex = ExceptionHelper.Terminate(ref error);
                                ClearCurrent();

                                a.OnError(ex);
                            }
                            else
                            {
                                a.OnComplete();
                            }

                            return;
                        }

                        if (hasValue != n)
                        {
                            break;
                        }

                        if (e == r)
                        {
                            break;
                        }

                        var row = new T[n];

                        for (int i = 0; i < n; i++)
                        {
                            row[i] = current[i];

                            var inner = s[i];
                            inner.HasValue(false);
                            inner.RequestOne();
                        }

                        R t;

                        try
                        {
                            t = zipper(row);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);
                            ExceptionHelper.AddError(ref error, ex);

                            ex = ExceptionHelper.Terminate(ref error);

                            CancelAll();
                            ClearAll();
                            ClearCurrent();

                            a.OnError(ex);
                            return;
                        }

                        if (a.TryOnNext(t))
                        {
                            e++;
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
        }

        sealed class ZipInnerSubscriber : ISubscriber<T>
        {
            readonly ZipParent parent;

            readonly int prefetch;

            readonly int limit;

            ISubscription s;

            int consumed;

            IQueue<T> queue;

            int fusionMode;

            bool done;

            bool nonEmpty;

            bool hasValue;

            internal ZipInnerSubscriber(ZipParent parent, int prefetch)
            {
                this.parent = parent;
                this.prefetch = prefetch;
                this.limit = prefetch - (prefetch >> 2);
            }

            internal void RequestOne()
            {
                if (fusionMode != FuseableHelper.SYNC)
                {
                    int p = consumed + 1;
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
                            nonEmpty = !qs.IsEmpty();
                            Volatile.Write(ref queue, qs);
                            Volatile.Write(ref done, true);

                            parent.Drain();
                            return;
                        }
                        else
                        if (m == FuseableHelper.ASYNC)
                        {
                            fusionMode = m;
                            Volatile.Write(ref queue, qs);

                            s.Request(prefetch < 0 ? long.MaxValue : prefetch);

                            return;
                        }
                    }

                    Volatile.Write(ref queue, QueueDrainHelper.CreateQueue<T>(prefetch));

                    s.Request(prefetch < 0 ? long.MaxValue : prefetch);
                }
            }

            public void OnNext(T t)
            {
                if (!nonEmpty)
                {
                    nonEmpty = true;
                }
                if (fusionMode != FuseableHelper.ASYNC)
                {
                    queue.Offer(t);
                }
                parent.Drain();
            }

            public void OnError(Exception e)
            {
                parent.InnerError(this, e);
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                parent.Drain();
            }

            internal bool IsDone()
            {
                return Volatile.Read(ref done);
            }

            internal void SetDone()
            {
                Volatile.Write(ref done, true);
            }

            internal void Clear()
            {
                queue = null;
            }

            internal bool Poll(out T value)
            {
                var q = Volatile.Read(ref queue);
                if (q != null)
                {
                    return q.Poll(out value);
                }

                value = default(T);
                return false;
            }

            internal bool NonEmpty()
            {
                return nonEmpty;
            }

            internal bool HasValue()
            {
                return hasValue;
            }

            internal void HasValue(bool state)
            {
                hasValue = state;
            }
        }
    }
}
