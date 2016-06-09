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
using System.Collections;

namespace Reactor.Core.publisher
{
    sealed class PublisherAsEnumerable<T> : IEnumerable<T>
    {
        readonly IPublisher<T> source;

        readonly int prefetch;

        public PublisherAsEnumerable(IPublisher<T> source, int prefetch)
        {
            this.source = source;
            this.prefetch = prefetch;
        }

        public IEnumerator<T> GetEnumerator()
        {
            var a = new AsEnumerator(prefetch);

            source.Subscribe(a);

            return a;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            var a = new AsEnumerator(prefetch);

            source.Subscribe(a);

            return a;
        }

        sealed class AsEnumerator : ISubscriber<T>, IEnumerator<T>, IEnumerator
        {
            readonly int prefetch;

            readonly int limit;

            ISubscription s;

            IQueue<T> queue;

            int sourceMode;

            Exception error;

            bool done;

            T current;

            int consumed;

            public T Current
            {
                get
                {
                    return current;
                }
            }

            object IEnumerator.Current
            {
                get
                {
                    return current;
                }
            }

            public AsEnumerator(int prefetch)
            {
                this.prefetch = prefetch;
                this.limit = prefetch - (prefetch >> 2);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    var qs = s as IQueueSubscription<T>;
                    if (qs != null)
                    {
                        int m = qs.RequestFusion(FuseableHelper.ANY | FuseableHelper.BOUNDARY);

                        if (m == FuseableHelper.SYNC)
                        {
                            this.sourceMode = m;
                            this.queue = qs;
                            Volatile.Write(ref done, true);
                            return;
                        }
                        else
                        if (m == FuseableHelper.ASYNC)
                        {
                            this.sourceMode = m;
                            this.queue = qs;

                            s.Request(prefetch < 0 ? long.MaxValue : prefetch);

                            return;

                        }
                    }

                    queue = QueueDrainHelper.CreateQueue<T>(prefetch);

                    s.Request(prefetch < 0 ? long.MaxValue : prefetch);
                }
            }

            public void OnNext(T t)
            {
                if (done)
                {
                    return;
                }

                if (sourceMode != FuseableHelper.ASYNC)
                {
                    if (!queue.Offer(t))
                    {
                        s.Cancel();
                        OnError(BackpressureHelper.MissingBackpressureException());
                        return;
                    }
                }

                Signal();
            }

            public void OnError(Exception e)
            {
                if (done)
                {
                    ExceptionHelper.OnErrorDropped(e);
                    return;
                }
                Volatile.Write(ref done, true);
                Signal();
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }
                Volatile.Write(ref done, true);
                Signal();
            }

            void Signal()
            {
                Monitor.Enter(this);
                try
                {
                    Monitor.Pulse(this);
                }
                finally
                {
                    Monitor.Exit(this);
                }
            }

            public bool MoveNext()
            {
                for (;;)
                {
                    bool d = Volatile.Read(ref done);

                    bool empty = !queue.Poll(out current);

                    if (d && empty)
                    {
                        Exception ex = error;
                        if (ex != null)
                        {
                            throw ex;
                        }
                        return false;
                    }

                    if (empty)
                    {
                        Monitor.Enter(this);
                        try
                        {
                            Monitor.Wait(this);
                        }
                        finally
                        {
                            Monitor.Exit(this);
                        }
                    }
                    else
                    {
                        if (sourceMode != FuseableHelper.SYNC)
                        {
                            int c = consumed + 1;
                            if (c == limit)
                            {
                                consumed = 0;
                                s.Request(c);
                            }
                            else
                            {
                                consumed = c;
                            }
                        }
                        return true;
                    }
                }
            }

            public void Reset()
            {
                throw new InvalidOperationException("Reset is not supported");
            }

            public void Dispose()
            {
                SubscriptionHelper.Cancel(ref s);
            }
        }
    }
}
