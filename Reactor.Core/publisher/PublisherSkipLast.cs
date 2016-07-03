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
    sealed class PublisherSkipLast<T> : IFlux<T>
    {
        readonly IPublisher<T> source;

        readonly long n;

        internal PublisherSkipLast(IPublisher<T> source, long n)
        {
            this.source = source;
            this.n = n;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                source.Subscribe(new SkipLastConditionalSubscriber((IConditionalSubscriber<T>)s, n));
            }
            else
            {
                source.Subscribe(new SkipLastSubscriber(s, n));
            }
        }

        sealed class SkipLastSubscriber : ISubscriber<T>, ISubscription
        {
            readonly ISubscriber<T> actual;

            readonly long n;

            readonly IQueue<T> queue;

            ISubscription s;

            long size;

            internal SkipLastSubscriber(ISubscriber<T> actual, long n)
            {
                this.actual = actual;
                this.n = n;
                this.queue = new ArrayQueue<T>();
            }

            public void Cancel()
            {
                s.Cancel();
            }

            public void OnComplete()
            {
                actual.OnComplete();
            }

            public void OnError(Exception e)
            {
                queue.Clear();
                actual.OnError(e);
            }

            public void OnNext(T t)
            {
                long z = size;
                if (z != n)
                {
                    queue.Offer(t);
                    size = z + 1;
                }
                else
                {
                    T u;
                    queue.Poll(out u);
                    queue.Offer(t);
                    actual.OnNext(u);
                }
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    actual.OnSubscribe(this);

                    s.Request(n);
                }
            }

            public void Request(long n)
            {
                s.Request(n);
            }
        }

        sealed class SkipLastConditionalSubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IConditionalSubscriber<T> actual;

            readonly long n;

            readonly IQueue<T> queue;

            ISubscription s;

            long size;

            internal SkipLastConditionalSubscriber(IConditionalSubscriber<T> actual, long n)
            {
                this.actual = actual;
                this.n = n;
                this.queue = new ArrayQueue<T>();
            }

            public void Cancel()
            {
                s.Cancel();
            }

            public void OnComplete()
            {
                actual.OnComplete();
            }

            public void OnError(Exception e)
            {
                queue.Clear();
                actual.OnError(e);
            }

            public void OnNext(T t)
            {
                long z = size;
                if (z != n)
                {
                    queue.Offer(t);
                    size = z + 1;
                }
                else
                {
                    T u;
                    queue.Poll(out u);
                    queue.Offer(t);
                    actual.OnNext(u);
                }
            }

            public bool TryOnNext(T t)
            {
                long z = size;
                if (z != n)
                {
                    queue.Offer(t);
                    size = z + 1;
                    return true;
                }
                T u;
                queue.Poll(out u);
                queue.Offer(t);
                return actual.TryOnNext(u);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    actual.OnSubscribe(this);

                    s.Request(n);
                }
            }

            public void Request(long n)
            {
                s.Request(n);
            }
        }
    }
}
