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
    sealed class PublisherIgnoreElements<T, R> : IFlux<R>, IMono<R>
    {
        readonly IPublisher<T> source;

        internal PublisherIgnoreElements(IPublisher<T> source)
        {
            this.source = source;
        }

        public void Subscribe(ISubscriber<R> s)
        {
            source.Subscribe(new IgnoreElementsSubscriber(s));
        }

        sealed class IgnoreElementsSubscriber : ISubscriber<T>, IQueueSubscription<R>
        {
            readonly ISubscriber<R> actual;

            ISubscription s;

            public IgnoreElementsSubscriber(ISubscriber<R> actual)
            {
                this.actual = actual;
            }

            public void Cancel()
            {
                s.Cancel();
            }

            public void Clear()
            {
                // always empty
            }

            public bool IsEmpty()
            {
                return true;
            }

            public bool Offer(R value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public void OnComplete()
            {
                actual.OnComplete();
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
                    s.Request(long.MaxValue);
                }
            }

            public bool Poll(out R value)
            {
                value = default(R);
                return false;
            }

            public void Request(long n)
            {
                // ignored, always empty
            }

            public int RequestFusion(int mode)
            {
                return mode & FuseableHelper.ASYNC;
            }
        }
    }
}
