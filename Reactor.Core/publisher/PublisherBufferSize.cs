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

namespace Reactor.Core.publisher
{
    sealed class PublisherBufferSize<T> : IFlux<IList<T>>
    {
        readonly IPublisher<T> source;

        readonly int size;

        readonly int skip;

        internal PublisherBufferSize(IPublisher<T> source, int size, int skip)
        {
            this.source = source;
            this.size = size;
            this.skip = skip;
        }

        public void Subscribe(ISubscriber<IList<T>> s)
        {
            if (size == skip)
            {
                source.Subscribe(new BufferExact(s, size));
            }
            else
            if (size < skip)
            {
                source.Subscribe(new BufferSkip(s, size, skip));
            }
            else
            {
                source.Subscribe(new BufferOverlap(s, size, skip));
            }
        }

        sealed class BufferExact : BasicSubscriber<T, IList<T>>
        {
            readonly int size;

            IList<T> list;

            internal BufferExact(ISubscriber<IList<T>> actual, int size) : base(actual)
            {
                this.size = size;
                this.list = new List<T>();
            }

            public override void OnComplete()
            {
                var ls = list;
                list = null;
                if (ls.Count != 0)
                {
                    actual.OnNext(ls);
                }
                Complete();
            }

            public override void OnError(Exception e)
            {
                list = null;
                Error(e);
            }

            public override void OnNext(T t)
            {
                var ls = list;
                ls.Add(t);
                if (ls.Count == size)
                {
                    actual.OnNext(ls);
                    list = new List<T>();
                }
            }

            public override void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    long u = BackpressureHelper.MultiplyCap(n, size);
                    s.Request(u);
                }
            }
        }

        sealed class BufferSkip : BasicSubscriber<T, IList<T>>
        {
            readonly int size;

            readonly int skip;

            IList<T> list;

            int once;

            int consumed;

            public BufferSkip(ISubscriber<IList<T>> actual, int size, int skip) : base(actual)
            {
                this.size = size;
                this.skip = skip;
                this.list = new List<T>();
            }

            public override void OnComplete()
            {
                var ls = list;
                list = null;

                if (ls != null && ls.Count != 0)
                {
                    actual.OnNext(ls);
                }
                Complete();
            }

            public override void OnError(Exception e)
            {
                list = null;
                Error(e);
            }

            public override void OnNext(T t)
            {
                var ls = list;
                int c = consumed;

                if (c == 0)
                {
                    ls = new List<T>();
                    list = ls;
                }

                if (ls != null)
                {
                    ls.Add(t);
                    if (ls.Count == size)
                    {
                        list = null;
                        actual.OnNext(ls);
                    }
                }

                if (++c == skip)
                {
                    consumed = 0;
                }                
                else
                {
                    consumed = c;
                }

            }

            public override void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    if (Volatile.Read(ref once) == 0 && Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        long u = BackpressureHelper.MultiplyCap(n - 1, skip);
                        long v = BackpressureHelper.AddCap(u, size);
                        s.Request(v);
                    }
                    else
                    {
                        long u = BackpressureHelper.MultiplyCap(n, skip);
                        s.Request(u);
                    }
                }
            }
        }

        sealed class BufferOverlap : BasicSubscriber<T, IList<T>>
        {
            readonly int size;

            readonly int skip;

            ArrayQueue<IList<T>> lists;

            int consumed;

            int once;

            long requested;

            long produced;

            bool cancelled;

            public BufferOverlap(ISubscriber<IList<T>> actual, int size, int skip) : base(actual)
            {
                this.size = size;
                this.skip = skip;
                this.lists = new ArrayQueue<IList<T>>();
            }

            public override void OnComplete()
            {
                long p = produced;
                if (p != 0L && Volatile.Read(ref requested) != long.MaxValue)
                {
                    Interlocked.Add(ref requested, p);
                }
                BackpressureHelper.PostComplete<IList<T>>(ref requested, actual, lists, ref cancelled);
            }

            public override void OnError(Exception e)
            {
                lists.Clear();
                Error(e);
            }

            public override void OnNext(T t)
            {
                var ls = lists;
                int c = consumed;
                if (c == 0)
                {
                    ls.Offer(new List<T>());
                }

                int m = ls.mask;
                var a = ls.array;
                long pi = ls.producerIndex;

                for (long i = ls.consumerIndex; i != pi; i++)
                {
                    int offset = (int)i & m;

                    var inner = a[offset];
                    inner.Add(t);

                    if (inner.Count == size)
                    {
                        ls.Drop();

                        produced--;

                        actual.OnNext(inner);
                    }
                }

                if (++c == skip)
                {
                    consumed = 0;
                }
                else
                {
                    consumed = c;
                }
            }

            public override void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    if (!BackpressureHelper.PostCompleteRequest<IList<T>>(ref requested, n, actual, lists, ref cancelled))
                    {
                        if (Volatile.Read(ref once) == 0 && Interlocked.CompareExchange(ref once, 1, 0) == 0)
                        {
                            long r = BackpressureHelper.MultiplyCap(n - 1, size - skip);
                            long u = BackpressureHelper.AddCap(r, size);
                            s.Request(u);
                        }
                        else
                        {
                            long r = BackpressureHelper.MultiplyCap(n, size - skip);
                            s.Request(r);
                        }
                    }
                }
            }

            public override void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                base.Cancel();
            }
        }
    }
}
