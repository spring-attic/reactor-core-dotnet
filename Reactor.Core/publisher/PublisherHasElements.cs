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
    sealed class PublisherHasElements<T> : IMono<bool>
    {
        readonly IPublisher<T> source;

        internal PublisherHasElements(IPublisher<T> source)
        {
            this.source = source;
        }

        public void Subscribe(ISubscriber<bool> s)
        {
            source.Subscribe(new HasElementsSubscriber(s));
        }

        sealed class HasElementsSubscriber : DeferredScalarSubscriber<T, bool>
        {
            bool done;

            public HasElementsSubscriber(ISubscriber<bool> actual) : base(actual)
            {

            }

            protected override void OnStart()
            {
                s.Request(long.MaxValue);
            }

            public override void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;
                Complete(false);
            }

            public override void OnError(Exception e)
            {
                if (done)
                {
                    ExceptionHelper.OnErrorDropped(e);
                    return;
                }
                done = true;
                Error(e);
            }

            public override void OnNext(T t)
            {
                if (done)
                {
                    return;
                }
                done = true;
                s.Cancel();
                Complete(true);
            }
        }
    }
}
