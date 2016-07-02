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
    sealed class PublisherOnBackpressureBuffer<T> : IFlux<T>
    {
        readonly IFlux<T> source;

        readonly int capacityHint;

        internal PublisherOnBackpressureBuffer(IFlux<T> source, int capacityHint)
        {
            this.source = source;
            this.capacityHint = capacityHint;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                source.Subscribe(new OnBackpressureBufferConditionalSubscriber((IConditionalSubscriber<T>)s, capacityHint));
            }
            else
            {
                source.Subscribe(new OnBackpressureBufferSubscriber(s, capacityHint));
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal abstract class BaseOnBackpressureBufferSubscriber : ISubscriber<T>, IQueueSubscription<T>
        {
            protected readonly IQueue<T> queue;

            ISubscription s;

            bool outputFused;

            protected long requested;

            protected bool cancelled;

            protected bool done;

            protected Exception error;

            Pad128 p0;

            protected int wip;

            Pad120 p1;

            internal BaseOnBackpressureBufferSubscriber(int capacityHint)
            {
                this.queue = new SpscLinkedArrayQueue<T>(capacityHint);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    SubscribeActual();

                    s.Request(long.MaxValue);
                }
            }

            protected abstract void SubscribeActual();

            public void OnNext(T t)
            {
                queue.Offer(t);
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

            public int RequestFusion(int mode)
            {
                if ((mode & FuseableHelper.ASYNC) != 0)
                {
                    outputFused = true;
                    return FuseableHelper.ASYNC;
                }
                return FuseableHelper.NONE;
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
                Volatile.Write(ref cancelled, true);
                if (QueueDrainHelper.Enter(ref wip))
                {
                    queue.Clear();
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

            protected abstract void DrainNormal();

            protected abstract void DrainFused();
        }

        sealed class OnBackpressureBufferSubscriber : BaseOnBackpressureBufferSubscriber
        {
            readonly ISubscriber<T> actual;

            public OnBackpressureBufferSubscriber(ISubscriber<T> actual, int capacityHint) : base(capacityHint)
            {
                this.actual = actual;
            }

            protected override void DrainFused()
            {
                int missed = 1;
                var a = actual;
                var q = queue;

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        q.Clear();
                        return;
                    }

                    bool d = Volatile.Read(ref done);

                    a.OnNext(default(T));

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
                        return;
                    }
                }
            }

            protected override void DrainNormal()
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
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        T t;

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
                        if (Volatile.Read(ref cancelled))
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

                    if (e != 0L && r != long.MaxValue)
                    {
                        Interlocked.Add(ref requested, -e);
                    }

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        return;
                    }
                }
            }

            protected override void SubscribeActual()
            {
                actual.OnSubscribe(this);
            }
        }

        sealed class OnBackpressureBufferConditionalSubscriber : BaseOnBackpressureBufferSubscriber
        {
            readonly IConditionalSubscriber<T> actual;

            public OnBackpressureBufferConditionalSubscriber(IConditionalSubscriber<T> actual, int capacityHint) : base(capacityHint)
            {
                this.actual = actual;
            }

            protected override void DrainFused()
            {
                int missed = 1;
                var a = actual;
                var q = queue;

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        q.Clear();
                        return;
                    }

                    bool d = Volatile.Read(ref done);

                    a.TryOnNext(default(T));

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
                        return;
                    }
                }
            }

            protected override void DrainNormal()
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
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        T t;

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

                        if (a.TryOnNext(t))
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

                    if (e != 0L && r != long.MaxValue)
                    {
                        Interlocked.Add(ref requested, -e);
                    }

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        return;
                    }
                }
            }

            protected override void SubscribeActual()
            {
                actual.OnSubscribe(this);
            }
        }
    }
}
