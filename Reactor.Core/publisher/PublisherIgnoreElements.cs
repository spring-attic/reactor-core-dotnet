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

        sealed class IgnoreElementsSubscriber : BasicFuseableSubscriber<T, R>
        {
            public IgnoreElementsSubscriber(ISubscriber<R> actual) : base(actual)
            {
            }

            public override void OnComplete()
            {
                Complete();
            }

            public override void OnError(Exception e)
            {
                Error(e);
            }

            public override void OnNext(T t)
            {
                // ignored
            }

            public override bool Poll(out R value)
            {
                for (;;)
                {
                    T local;

                    if (!qs.Poll(out local))
                    {
                        break;
                    }
                }
                value = default(R);
                return false;
            }

            public override int RequestFusion(int mode)
            {
                return TransitiveAnyFusion(mode);
            }

            public override void Request(long n)
            {
                // ignored
            }

            protected override void AfterSubscribe()
            {
                if (fusionMode != FuseableHelper.SYNC)
                {
                    s.Request(long.MaxValue);
                }
            }
        }
    }
}
