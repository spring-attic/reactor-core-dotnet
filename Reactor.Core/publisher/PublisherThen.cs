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
    sealed class PublisherThen<T, U> : IFlux<U>, IMono<U>
    {
        readonly IPublisher<T> source;

        readonly IPublisher<U> after;

        internal PublisherThen(IPublisher<T> source, IPublisher<U> after)
        {
            this.source = source;
            this.after = after;
        }

        public void Subscribe(ISubscriber<U> s)
        {
            if (s is IConditionalSubscriber<U>)
            {
                source.Subscribe(new ThenConditionalSubscriber((IConditionalSubscriber<U>)s, after));
            }
            else
            {
                source.Subscribe(new ThenSubscriber(s, after));
            }
        }

        sealed class ThenSubscriber : ISubscriber<T>, ISubscription
        {
            readonly ISubscriber<U> actual;

            readonly IPublisher<U> after;

            ISubscription s;

            ISubscription z;

            long requested;

            internal ThenSubscriber(ISubscriber<U> actual, IPublisher<U> after)
            {
                this.actual = actual;
                this.after = after;
            }

            public void Cancel()
            {
                s.Cancel();
                SubscriptionHelper.Cancel(ref z);
            }

            public void OnComplete()
            {
                after.Subscribe(new OtherSubscriber(this, actual));
            }

            public void OnError(Exception e)
            {
                actual.OnError(e);
            }

            public void OnNext(T t)
            {
                // ignored
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    actual.OnSubscribe(this);

                    s.Request(long.MaxValue);
                }
            }

            public void Request(long n)
            {
                BackpressureHelper.DeferredRequest(ref z, ref requested, n);
            }

            internal void AfterSubscribe(ISubscription s)
            {
                BackpressureHelper.DeferredSetOnce(ref z, ref requested, s);
            }

            sealed class OtherSubscriber : ISubscriber<U>
            {
                readonly ThenSubscriber parent;

                readonly ISubscriber<U> actual;

                internal OtherSubscriber(ThenSubscriber parent, ISubscriber<U> actual)
                {
                    this.parent = parent;
                    this.actual = actual;
                }

                public void OnSubscribe(ISubscription s)
                {
                    parent.AfterSubscribe(s);
                }

                public void OnNext(U t)
                {
                    actual.OnNext(t);
                }

                public void OnError(Exception e)
                {
                    actual.OnError(e);
                }

                public void OnComplete()
                {
                    actual.OnComplete();
                }
            }
        }

        sealed class ThenConditionalSubscriber : ISubscriber<T>, ISubscription
        {
            readonly IConditionalSubscriber<U> actual;

            readonly IPublisher<U> after;

            ISubscription s;

            ISubscription z;

            long requested;

            internal ThenConditionalSubscriber(IConditionalSubscriber<U> actual, IPublisher<U> after)
            {
                this.actual = actual;
                this.after = after;
            }

            public void Cancel()
            {
                s.Cancel();
                SubscriptionHelper.Cancel(ref z);
            }

            public void OnComplete()
            {
                after.Subscribe(new OtherSubscriber(this, actual));
            }

            public void OnError(Exception e)
            {
                actual.OnError(e);
            }

            public void OnNext(T t)
            {
                // ignored
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    actual.OnSubscribe(this);

                    s.Request(long.MaxValue);
                }
            }

            public void Request(long n)
            {
                BackpressureHelper.DeferredRequest(ref z, ref requested, n);
            }

            internal void AfterSubscribe(ISubscription s)
            {
                BackpressureHelper.DeferredSetOnce(ref z, ref requested, s);
            }

            sealed class OtherSubscriber : IConditionalSubscriber<U>
            {
                readonly ThenConditionalSubscriber parent;

                readonly IConditionalSubscriber<U> actual;

                internal OtherSubscriber(ThenConditionalSubscriber parent, IConditionalSubscriber<U> actual)
                {
                    this.parent = parent;
                    this.actual = actual;
                }

                public void OnSubscribe(ISubscription s)
                {
                    parent.AfterSubscribe(s);
                }

                public void OnNext(U t)
                {
                    actual.OnNext(t);
                }

                public bool TryOnNext(U t)
                {
                    return actual.TryOnNext(t);
                }

                public void OnError(Exception e)
                {
                    actual.OnError(e);
                }

                public void OnComplete()
                {
                    actual.OnComplete();
                }
            }
        }
    }
}
