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
    sealed class PublisherHide<T> : IFlux<T>, IMono<T>
    {
        readonly IPublisher<T> source;

        internal PublisherHide(IPublisher<T> source)
        {
            this.source = source;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            source.Subscribe(new HideSubscriber(s));
        }

        sealed class HideSubscriber : ISubscriber<T>, ISubscription
        {
            readonly ISubscriber<T> actual;

            ISubscription s;

            internal HideSubscriber(ISubscriber<T> actual)
            {
                this.actual = actual;
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
                actual.OnError(e);
            }

            public void OnNext(T t)
            {
                actual.OnNext(t);
            }

            public void OnSubscribe(ISubscription s)
            {
                this.s = s;
                actual.OnSubscribe(this);
            }

            public void Request(long n)
            {
                s.Request(n);
            }
        }
    }
}
