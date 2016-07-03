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
    sealed class PublisherTakeLastOne<T> : IFlux<T>
    {
        readonly IPublisher<T> source;

        internal PublisherTakeLastOne(IPublisher<T> source)
        {
            this.source = source;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            source.Subscribe(new TakeLastOne(s));
        }

        sealed class TakeLastOne : DeferredScalarSubscriber<T, T>
        {

            bool hasValue;

            public TakeLastOne(ISubscriber<T> actual) : base(actual)
            {
            }

            protected override void OnStart()
            {
                s.Request(long.MaxValue);
            }

            public override void OnComplete()
            {
                if (hasValue)
                {
                    Complete(value);
                }
                else
                {
                    Complete();
                }
            }

            public override void OnError(Exception e)
            {
                value = default(T);
                Error(e);
            }

            public override void OnNext(T t)
            {
                if (!hasValue)
                {
                    hasValue = true;
                }
                value = t;
            }
        }
    }
}
