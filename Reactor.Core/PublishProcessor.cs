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

namespace Reactor.Core
{
    /// <summary>
    /// A processor that dispatches signals to its Subscriber and coordinates
    /// requests in a lockstep fashion.
    /// This type of IProcessor mandates the call to OnSubscribe().
    /// </summary>
    /// <typeparam name="T">The value type dispatched</typeparam>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public sealed class PublishProcessor<T> : IFluxProcessor<T>, IDisposable
    {
        TrackingArray<PublishSubscription> subscribers;

        readonly int prefetch;

        ISubscription s;

        IQueue<T> queue;

        int sourceMode;

        bool done;
        Exception error;

        Pad128 p0;

        int wip;

        Pad120 p1;

        /// <inheritDoc/>
        public bool HasSubscribers
        {
            get
            {
                return subscribers.Array().Length != 0;
            }
        }

        /// <inheritDoc/>
        public bool IsComplete
        {
            get
            {
                return Volatile.Read(ref done) && error == null;
            }
        }

        /// <inheritDoc/>
        public bool HasError
        {
            get
            {
                return Volatile.Read(ref error) != null;
            }
        }

        /// <inheritDoc/>
        public Exception Error
        {
            get
            {
                return Volatile.Read(ref error);
            }
        }

        /// <summary>
        /// Constructs a PublishProcessor with the default
        /// prefetch amount of <see cref="Flux.BufferSize"/>.
        /// </summary>
        public PublishProcessor() : this(Flux.BufferSize)
        {
        }

        /// <summary>
        /// Constructs a PublishProcessor with the given
        /// prefetch amount (rounded to the next power-of-two).
        /// </summary>
        /// <param name="prefetch">The prefetch amount</param>
        public PublishProcessor(int prefetch)
        {
            subscribers.Init();
            this.prefetch = prefetch;
        }

        /// <inheritDoc/>
        public void Dispose()
        {
            if (SubscriptionHelper.Cancel(ref s))
            {
                OnError(new OperationCanceledException());
            }
            
        }

        /// <inheritDoc/>
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
                        sourceMode = m;
                        queue = qs;
                        Volatile.Write(ref done, true);
                        Drain();
                        return;
                    }
                    if (m == FuseableHelper.ASYNC)
                    {
                        sourceMode = m;
                        queue = qs;

                        s.Request(prefetch < 0 ? long.MaxValue : prefetch);

                        return;
                    }
                }

                queue = QueueDrainHelper.CreateQueue<T>(prefetch);

                s.Request(prefetch < 0 ? long.MaxValue : prefetch);
            }
        }


        /// <inheritDoc/>
        public void OnComplete()
        {
            if (done)
            {
                return;
            }
            Volatile.Write(ref done, true);
            Drain();
        }

        /// <inheritDoc/>
        public void OnError(Exception e)
        {
            if (done || Interlocked.CompareExchange(ref error, e, null) != null)
            {
                ExceptionHelper.OnErrorDropped(e);
                return;
            }
            Volatile.Write(ref done, true);
            Drain();
        }

        /// <inheritDoc/>
        public void OnNext(T t)
        {
            if (done)
            {
                return;
            }

            if (sourceMode == FuseableHelper.NONE)
            {
                if (!queue.Offer(t))
                {
                    SubscriptionHelper.Cancel(ref s);
                    OnError(BackpressureHelper.MissingBackpressureException());
                    return;
                }
            }
            Drain();
        }

        /// <inheritDoc/>
        public void Subscribe(ISubscriber<T> s)
        {
            PublishSubscription ps = new PublishSubscription(s, this);
            s.OnSubscribe(ps);

            if (subscribers.Add(ps))
            {
                if (ps.IsCancelled())
                {
                    subscribers.Remove(ps);
                }
                else
                {
                    Drain();
                }
            }
            else
            {
                var ex = error;
                if (ex != null)
                {
                    EmptySubscription<T>.Error(s, ex);
                }
                else
                {
                    EmptySubscription<T>.Complete(s);
                }
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

            for (;;)
            {
                var array = subscribers.Array();
                int n = array.Length;

                if (n != 0 && q != null)
                {
                    long r = long.MaxValue;
                    long e = long.MaxValue;

                    foreach (var s in array)
                    {
                        r = Math.Min(r, s.Requested());
                        e = Math.Min(e, s.Produced());
                    }

                    while (e != r)
                    {
                        bool d = Volatile.Read(ref done);

                        T v;

                        bool empty = !q.Poll(out v);

                        if (d && empty)
                        {
                            var ex = Volatile.Read(ref error);
                            if (ex != null)
                            {
                                foreach(var a in subscribers.Terminate())
                                {
                                    a.actual.OnError(ex);
                                }
                            }
                            else
                            {
                                foreach (var a in subscribers.Terminate())
                                {
                                    a.actual.OnComplete();
                                }
                            }
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        foreach (var a in array)
                        {
                            a.actual.OnNext(v);
                        }

                        e++;
                    }

                    if (e == r)
                    {
                        bool d = Volatile.Read(ref done);

                        bool empty = q.IsEmpty();

                        if (d && empty)
                        {
                            var ex = Volatile.Read(ref error);
                            if (ex != null)
                            {
                                foreach (var a in subscribers.Terminate())
                                {
                                    a.actual.OnError(ex);
                                }
                            }
                            else
                            {
                                foreach (var a in subscribers.Terminate())
                                {
                                    a.actual.OnComplete();
                                }
                            }
                            return;
                        }
                    }

                    if (e != 0L)
                    {
                        foreach (var a in array)
                        {
                            a.Produced(e);
                        }

                        if (sourceMode != FuseableHelper.SYNC)
                        {
                            s.Request(e);
                        }
                    }
                }

                missed = QueueDrainHelper.Leave(ref wip, missed);
                if (missed == 0)
                {
                    break;
                }
            }
        }

        sealed class PublishSubscription : ISubscription
        {
            internal readonly ISubscriber<T> actual;

            readonly PublishProcessor<T> parent;

            int cancelled;

            long requested;

            long produced;

            internal PublishSubscription(ISubscriber<T> actual, PublishProcessor<T> parent)
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
                    parent.Drain();
                }
            }

            public bool IsCancelled()
            {
                return Volatile.Read(ref cancelled) != 0;
            }

            public long Requested()
            {
                return Volatile.Read(ref requested);
            }

            public void Produced(long n)
            {
                produced += n;
            }

            public long Produced()
            {
                return produced;
            }

        }
    }
}
