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
    sealed class PublisherMaterialize<T> : IFlux<ISignal<T>>
    {
        readonly IPublisher<T> source;

        internal PublisherMaterialize(IPublisher<T> source)
        {
            this.source = source;
        }

        public void Subscribe(ISubscriber<ISignal<T>> s)
        {
            source.Subscribe(new MaterializeSubscriber(s));
        }

        sealed class MaterializeSubscriber : BasicSinglePostCompleteSubscriber<T, ISignal<T>>
        {
            public MaterializeSubscriber(ISubscriber<ISignal<T>> actual) : base(actual)
            {
            }

            public override void OnComplete()
            {
                Complete(SignalHelper.Complete<T>());
            }

            public override void OnError(Exception e)
            {
                Complete(SignalHelper.Error<T>(e));
            }

            public override void OnNext(T t)
            {
                produced++;
                actual.OnNext(SignalHelper.Next(t));
            }
        }
    }
}
