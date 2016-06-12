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
    sealed class PublisherLast<T> : IMono<T>
    {
        readonly IPublisher<T> source;
    
        internal PublisherLast(IPublisher<T> source)
        {
            this.source = source;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            source.Subscribe(new LastSubscriber(s));
        }

        sealed class LastSubscriber : DeferredScalarSubscriber<T, T>
        {

            bool hasValue;

            public LastSubscriber(ISubscriber<T> actual) : base(actual)
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
                    Error(new IndexOutOfRangeException("The source sequence is empty."));
                }
            }

            public override void OnError(Exception e)
            {
                Error(e);
            }

            public override void OnNext(T t)
            {
                hasValue = true;
                value = t;
            }
        }
    }

}
