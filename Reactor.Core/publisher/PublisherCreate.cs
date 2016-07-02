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
    sealed class PublisherCreate<T> : IFlux<T>
    {
        readonly Action<IFluxEmitter<T>> emitter;

        readonly BackpressureHandling backpressure;

        internal PublisherCreate(Action<IFluxEmitter<T>> emitter, BackpressureHandling backpressure)
        {
            this.emitter = emitter;
            this.backpressure = backpressure;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            switch (backpressure)
            {
                case BackpressureHandling.Error:
                    {
                        ErrorObserver o = new ErrorObserver(s);
                        s.OnSubscribe(o);
                        emitter(o);
                    }
                    break;
                case BackpressureHandling.Drop:
                    {
                        ErrorObserver o = new ErrorObserver(s);
                        s.OnSubscribe(o);
                        emitter(o);
                    }
                    break;
                case BackpressureHandling.Latest:
                    {
                        if (s is IConditionalSubscriber<T>)
                        {
                            LatestConditionalObserver o = new LatestConditionalObserver((IConditionalSubscriber<T>)s);
                            s.OnSubscribe(o);
                            emitter(o);
                        }
                        else
                        {
                            LatestObserver o = new LatestObserver(s);
                            s.OnSubscribe(o);
                            emitter(o);
                        }
                    }
                    break;
                case BackpressureHandling.Buffer:
                    {
                        if (s is IConditionalSubscriber<T>)
                        {
                            BufferConditionalObserver o = new BufferConditionalObserver((IConditionalSubscriber<T>)s, Flux.BufferSize);
                            s.OnSubscribe(o);
                            emitter(o);
                        }
                        else
                        {
                            BufferObserver o = new BufferObserver(s, Flux.BufferSize);
                            s.OnSubscribe(o);
                            emitter(o);
                        }
                    }
                    break;
                default:
                    {
                        NoneObserver o = new NoneObserver(s);
                        s.OnSubscribe(o);
                        emitter(o);
                    }
                    break;
            }
        }

        internal sealed class NoneObserver : BasicRejectingSubscription<T>, IFluxEmitter<T>
        {
            readonly ISubscriber<T> actual;

            IDisposable d;

            long requested;

            public NoneObserver(ISubscriber<T> actual)
            {
                this.actual = actual;
            }

            public long Requested
            {
                get
                {
                    return Volatile.Read(ref requested);
                }
            }

            public override void Cancel()
            {
                DisposableHelper.Dispose(ref d);
            }

            public void Complete()
            {
                actual.OnComplete();
            }

            public void Error(Exception error)
            {
                actual.OnError(error);
            }

            public void Next(T value)
            {
                actual.OnNext(value);
            }

            public override void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                }
            }

            public void SetDisposable(IDisposable d)
            {
                DisposableHelper.Replace(ref this.d, d);
            }

        }

        sealed class ErrorObserver : BasicRejectingSubscription<T>, IFluxEmitter<T>
        {
            readonly ISubscriber<T> actual;

            IDisposable d;

            long requested;

            long produced;

            bool done;

            public long Requested
            {
                get
                {
                    return Volatile.Read(ref requested);
                }
            }

            public ErrorObserver(ISubscriber<T> actual)
            {
                this.actual = actual;
            }

            public override void Cancel()
            {
                DisposableHelper.Dispose(ref d);
            }

            public void Complete()
            {
                if (done)
                {
                    return;
                }
                done = true;
                actual.OnComplete();
            }

            public void Error(Exception error)
            {
                if (done)
                {
                    ExceptionHelper.OnErrorDropped(error);
                    return;
                }
                done = true;
                actual.OnError(error);
            }

            public void Next(T value)
            {
                if (done)
                {
                    return;
                }

                long r = Volatile.Read(ref requested);
                long p = produced;

                if (r != p)
                {
                    produced = p + 1;
                    actual.OnNext(value);
                }
                else
                {
                    done = true;
                    Cancel();
                    actual.OnError(BackpressureHelper.MissingBackpressureException());
                }
            }

            public override void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                }
            }

            public void SetDisposable(IDisposable d)
            {
                DisposableHelper.Replace(ref this.d, d);
            }
        }

        sealed class DropObserver : BasicRejectingSubscription<T>, IFluxEmitter<T>
        {
            readonly ISubscriber<T> actual;

            IDisposable d;

            long requested;

            long produced;

            public long Requested
            {
                get
                {
                    return Volatile.Read(ref requested);
                }
            }

            public DropObserver(ISubscriber<T> actual)
            {
                this.actual = actual;
            }

            public override void Cancel()
            {
                DisposableHelper.Dispose(ref d);
            }

            public void Complete()
            {
                actual.OnComplete();
            }

            public void Error(Exception error)
            {
                actual.OnError(error);
            }

            public void Next(T value)
            {
                long r = Volatile.Read(ref requested);
                long p = produced;

                if (r != p)
                {
                    produced = p + 1;
                    actual.OnNext(value);
                }
            }

            public override void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                }
            }

            public void SetDisposable(IDisposable d)
            {
                DisposableHelper.Replace(ref this.d, d);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class BufferObserver : IFluxEmitter<T>, IQueueSubscription<T>
        {
            readonly ISubscriber<T> actual;

            readonly IQueue<T> queue;

            bool outputFused;

            IDisposable d;

            bool done;

            Exception error;

            bool cancelled;

            Pad128 p0;

            long requested;

            Pad120 p1;

            int wip;

            Pad120 p2;

            public long Requested
            {
                get
                {
                    return Volatile.Read(ref requested);
                }
            }

            public BufferObserver(ISubscriber<T> actual, int bufferSize)
            {
                this.actual = actual;
                this.queue = new SpscLinkedArrayQueue<T>(bufferSize);
            }

            public void Cancel()
            {
                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                Volatile.Write(ref cancelled, true);
                DisposableHelper.Dispose(ref d);

                if (QueueDrainHelper.Enter(ref wip))
                {
                    queue.Clear();
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

            public void SetDisposable(IDisposable d)
            {
                DisposableHelper.Replace(ref this.d, d);
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
                {
                    DrainRegular();
                }
            }

            void DrainOutput()
            {
                var q = queue;
                var a = actual;

                int missed = 1;

                for (;;)
                {

                    if (Volatile.Read(ref cancelled))
                    {
                        q.Clear();
                        return;
                    }

                    bool d = Volatile.Read(ref done);

                    T v;

                    bool empty = !queue.Poll(out v);

                    if (!empty)
                    {
                        a.OnNext(default(T));
                    }

                    if (d && empty)
                    {
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
                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            void DrainRegular()
            {
                var q = queue;
                var a = actual;

                int missed = 1;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    long e = 0;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        T v;

                        bool empty = !queue.Poll(out v);

                        if (d && empty)
                        {
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

                        a.OnNext(v);

                        e++;
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        if (Volatile.Read(ref done) && q.IsEmpty())
                        {
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

            public void Complete()
            {
                Volatile.Write(ref done, true);
                Drain();
            }

            public void Error(Exception error)
            {
                this.error = error;
                Volatile.Write(ref done, true);
                Drain();
            }

            public void Next(T value)
            {
                queue.Offer(value);
                Drain();
            }

            public int RequestFusion(int mode)
            {
                int m = mode & FuseableHelper.ASYNC;
                outputFused = m != 0;
                return m;
            }

            public bool Offer(T value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out T value)
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
        sealed class BufferConditionalObserver : IFluxEmitter<T>, IQueueSubscription<T>
        {
            readonly IConditionalSubscriber<T> actual;

            readonly IQueue<T> queue;

            bool outputFused;

            IDisposable d;

            bool done;

            Exception error;

            bool cancelled;

            Pad128 p0;

            long requested;

            Pad120 p1;

            int wip;

            Pad120 p2;

            public long Requested
            {
                get
                {
                    return Volatile.Read(ref requested);
                }
            }

            public BufferConditionalObserver(IConditionalSubscriber<T> actual, int bufferSize)
            {
                this.actual = actual;
                this.queue = new SpscLinkedArrayQueue<T>(bufferSize);
            }

            public void Cancel()
            {
                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                Volatile.Write(ref cancelled, true);
                DisposableHelper.Dispose(ref d);

                if (QueueDrainHelper.Enter(ref wip))
                {
                    queue.Clear();
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

            public void SetDisposable(IDisposable d)
            {
                DisposableHelper.Replace(ref this.d, d);
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
                {
                    DrainRegular();
                }
            }

            void DrainOutput()
            {
                var q = queue;
                var a = actual;

                int missed = 1;

                for (;;)
                {

                    if (Volatile.Read(ref cancelled))
                    {
                        q.Clear();
                        return;
                    }

                    bool d = Volatile.Read(ref done);

                    T v;

                    bool empty = !queue.Poll(out v);

                    if (!empty)
                    {
                        a.TryOnNext(default(T));
                    }

                    if (d && empty)
                    {
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
                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            void DrainRegular()
            {
                var q = queue;
                var a = actual;

                int missed = 1;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    long e = 0;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        T v;

                        bool empty = !queue.Poll(out v);

                        if (d && empty)
                        {
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

                        if (a.TryOnNext(v))
                        {
                            e++;
                        }
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        if (Volatile.Read(ref done) && q.IsEmpty())
                        {
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

            public void Complete()
            {
                Volatile.Write(ref done, true);
                Drain();
            }

            public void Error(Exception error)
            {
                this.error = error;
                Volatile.Write(ref done, true);
                Drain();
            }

            public void Next(T value)
            {
                queue.Offer(value);
                Drain();
            }

            public int RequestFusion(int mode)
            {
                int m = mode & FuseableHelper.ASYNC;
                outputFused = m != 0;
                return m;
            }

            public bool Offer(T value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out T value)
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
        sealed class LatestObserver : IFluxEmitter<T>, IQueueSubscription<T>
        {
            readonly ISubscriber<T> actual;

            IDisposable d;

            bool outputFused;

            bool done;

            Exception error;

            bool cancelled;

            Pad128 p0;

            Entry entry;

            Pad120 p1;

            long requested;

            Pad120 p2;

            int wip;

            Pad120 p3;

            public long Requested
            {
                get
                {
                    return Volatile.Read(ref requested);
                }
            }

            internal LatestObserver(ISubscriber<T> actual)
            {
                this.actual = actual;
            }

            public void SetDisposable(IDisposable d)
            {
                DisposableHelper.Replace(ref this.d, d);
            }

            public bool Offer(T value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public void Complete()
            {
                Volatile.Write(ref done, true);
                Drain();
            }

            public void Error(Exception error)
            {
                this.error = error;
                Volatile.Write(ref done, true);
                Drain();
            }

            public void Next(T value)
            {
                Volatile.Write(ref entry, new Entry(value));
                Drain();
            }

            public int RequestFusion(int mode)
            {
                int m = mode & FuseableHelper.ASYNC;
                outputFused = m != 0;
                return m;
            }

            public bool Poll(out T value)
            {
                var e = Volatile.Read(ref entry);

                if (e != null)
                {
                    e = Interlocked.Exchange(ref entry, null);
                    value = e.value;
                    return true;
                }
                value = default(T);
                return false;
            }

            public bool IsEmpty()
            {
                return Volatile.Read(ref entry) == null;
            }

            public void Clear()
            {
                entry = null;
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
                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                Volatile.Write(ref cancelled, true);
                DisposableHelper.Dispose(ref d);

                if (QueueDrainHelper.Enter(ref wip))
                {
                    entry = null;
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
                {
                    DrainRegular();
                }
            }

            void DrainOutput()
            {
                var a = actual;

                int missed = 1;

                for (;;)
                {

                    if (Volatile.Read(ref cancelled))
                    {
                        Clear();
                        return;
                    }

                    bool d = Volatile.Read(ref done);

                    bool empty = IsEmpty();

                    if (!empty)
                    {
                        a.OnNext(default(T));
                    }

                    if (d && empty)
                    {
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
                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            void DrainRegular()
            {
                var a = actual;

                int missed = 1;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    long e = 0;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        T v;

                        bool empty = Poll(out v);

                        if (d && empty)
                        {
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

                        a.OnNext(v);

                        e++;
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Clear();
                            return;
                        }

                        if (Volatile.Read(ref done) && IsEmpty())
                        {
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
        sealed class LatestConditionalObserver : IFluxEmitter<T>, IQueueSubscription<T>
        {
            readonly IConditionalSubscriber<T> actual;

            IDisposable d;

            bool outputFused;

            bool done;

            Exception error;

            bool cancelled;

            Pad128 p0;

            Entry entry;

            Pad120 p1;

            long requested;

            Pad120 p2;

            int wip;

            Pad120 p3;

            public long Requested
            {
                get
                {
                    return Volatile.Read(ref requested);
                }
            }

            internal LatestConditionalObserver(IConditionalSubscriber<T> actual)
            {
                this.actual = actual;
            }

            public void SetDisposable(IDisposable d)
            {
                DisposableHelper.Replace(ref this.d, d);
            }

            public bool Offer(T value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public void Complete()
            {
                Volatile.Write(ref done, true);
                Drain();
            }

            public void Error(Exception error)
            {
                this.error = error;
                Volatile.Write(ref done, true);
                Drain();
            }

            public void Next(T value)
            {
                Volatile.Write(ref entry, new Entry(value));
                Drain();
            }

            public int RequestFusion(int mode)
            {
                int m = mode & FuseableHelper.ASYNC;
                outputFused = m != 0;
                return m;
            }

            public bool Poll(out T value)
            {
                var e = Volatile.Read(ref entry);

                if (e != null)
                {
                    e = Interlocked.Exchange(ref entry, null);
                    value = e.value;
                    return true;
                }
                value = default(T);
                return false;
            }

            public bool IsEmpty()
            {
                return Volatile.Read(ref entry) == null;
            }

            public void Clear()
            {
                entry = null;
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
                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                Volatile.Write(ref cancelled, true);
                DisposableHelper.Dispose(ref d);

                if (QueueDrainHelper.Enter(ref wip))
                {
                    entry = null;
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
                {
                    DrainRegular();
                }
            }

            void DrainOutput()
            {
                var a = actual;

                int missed = 1;

                for (;;)
                {

                    if (Volatile.Read(ref cancelled))
                    {
                        Clear();
                        return;
                    }

                    bool d = Volatile.Read(ref done);

                    bool empty = IsEmpty();

                    if (!empty)
                    {
                        a.TryOnNext(default(T));
                    }

                    if (d && empty)
                    {
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
                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            void DrainRegular()
            {
                var a = actual;

                int missed = 1;

                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    long e = 0;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        T v;

                        bool empty = Poll(out v);

                        if (d && empty)
                        {
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

                        if (a.TryOnNext(v))
                        {
                            e++;
                        }
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Clear();
                            return;
                        }

                        if (Volatile.Read(ref done) && IsEmpty())
                        {
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

        sealed class Entry
        {
            internal readonly T value;

            internal Entry(T value)
            {
                this.value = value;
            }
        }
    }
}
