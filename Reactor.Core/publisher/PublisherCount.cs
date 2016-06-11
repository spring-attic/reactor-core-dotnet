using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core;
using System.Threading;
using Reactor.Core.flow;
using Reactor.Core.subscription;
using Reactor.Core.util;
using Reactor.Core.subscriber;

namespace Reactor.Core.publisher
{
    sealed class PublisherCount<T> : IFlux<long>, IMono<long>
    {
        readonly IPublisher<T> source;

        public PublisherCount(IPublisher<T> source)
        {
            this.source = source;
        }

        public void Subscribe(ISubscriber<long> s)
        {
            source.Subscribe(new CountSubscriber(s));
        }

        sealed class CountSubscriber : DeferredScalarSubscriber<T, long>
        {
            public CountSubscriber(ISubscriber<long> actual) : base(actual)
            {
            }

            public override void OnComplete()
            {
                Complete(value);
            }

            public override void OnError(Exception e)
            {
                Error(e);
            }

            public override void OnNext(T t)
            {
                value++;
            }
        }
    }
}
