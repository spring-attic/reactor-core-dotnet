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
    sealed class PublisherFirstOrEmpty<T> : IMono<T>
    {
        readonly IPublisher<T> source;

        internal PublisherFirstOrEmpty(IPublisher<T> source)
        {
            this.source = source;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            throw new NotImplementedException();
        }

        sealed class FirstOrEmptySubscriber : DeferredScalarSubscriber<T, T>
        {

            bool done;

            public FirstOrEmptySubscriber(ISubscriber<T> actual) : base(actual)
            {
            }

            public override void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;
                Complete();
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
                Complete(t);
            }

            protected override void OnStart()
            {
                s.Request(long.MaxValue);
            }
        }
    }
}
