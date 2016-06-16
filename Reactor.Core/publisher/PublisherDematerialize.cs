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
    sealed class PublisherDematerialize<T> : IFlux<T>
    {
        readonly IPublisher<ISignal<T>> source;

        internal PublisherDematerialize(IPublisher<ISignal<T>> source)
        {
            this.source = source;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            throw new NotImplementedException();
        }

        sealed class DematerializeSubscriber : BasicSubscriber<ISignal<T>, T>, ISubscription
        {
            ISignal<T> value;

            public DematerializeSubscriber(ISubscriber<T> actual) : base(actual)
            {
            }

            protected override void AfterSubscribe()
            {
                s.Request(1);
            }

            public override void OnComplete()
            {
                Complete();
            }

            public override void OnError(Exception e)
            {
                Error(e);
            }

            public override void OnNext(ISignal<T> t)
            {
                if (done)
                {
                    return;
                }

                var s = value;

                if (s != null)
                {
                    if (s.IsNext)
                    {
                        actual.OnNext(t.Next);
                    }
                    else
                    if (s.IsError)
                    {
                        value = null;
                        this.s.Cancel();
                        Error(s.Error);
                        return;
                    }
                    else
                    {
                        this.s.Cancel();
                        Complete();
                        return;
                    }
                }

                value = s;
            }
        }
    }
}
