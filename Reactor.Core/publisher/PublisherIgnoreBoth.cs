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
    sealed class PublisherIgnoreBoth<T, U, R> : IFlux<R>, IMono<R>
    {
        readonly IPublisher<T> first;

        readonly IPublisher<U> second;

        internal PublisherIgnoreBoth(IPublisher<T> first, IPublisher<U> second)
        {
            this.first = first;
            this.second = second;
        }

        public void Subscribe(ISubscriber<R> s)
        {
            first.Subscribe(new IgnoreBothFirstSubscriber(s, second));
        }

        sealed class IgnoreBothFirstSubscriber : ISubscriber<T>, IQueueSubscription<R>
        {
            readonly ISubscriber<R> actual;

            readonly IPublisher<U> other;

            ISubscription s;

            ISubscription z;

            internal IgnoreBothFirstSubscriber(ISubscriber<R> actual, IPublisher<U> other)
            {
                this.actual = actual;
                this.other = other;
            }

            public void Cancel()
            {
                s.Cancel();
                SubscriptionHelper.Cancel(ref z);
            }

            public void OnComplete()
            {
                other.Subscribe(new SecondSubscriber(this));
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
                // ignored
            }

            internal void OtherSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref z, s))
                {
                    s.Request(long.MaxValue);
                }
            }

            internal void OtherComplete()
            {
                actual.OnComplete();
            }

            internal void OtherError(Exception ex)
            {
                actual.OnError(ex);
            }

            public int RequestFusion(int mode)
            {
                return mode & FuseableHelper.ASYNC;
            }

            public bool Offer(R value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out R value)
            {
                value = default(R);
                return false;
            }

            public bool IsEmpty()
            {
                return true;
            }

            public void Clear()
            {
                // always empty
            }

            sealed class SecondSubscriber : ISubscriber<U>
            {
                readonly IgnoreBothFirstSubscriber parent;

                internal SecondSubscriber(IgnoreBothFirstSubscriber parent)
                {
                    this.parent = parent;
                }

                public void OnComplete()
                {
                    parent.OtherComplete();
                }

                public void OnError(Exception e)
                {
                    parent.OtherError(e);
                }

                public void OnNext(U t)
                {
                    // ignored
                }

                public void OnSubscribe(ISubscription s)
                {
                    parent.OtherSubscribe(s);
                }
            }
        }
    }
}
