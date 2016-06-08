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
    sealed class PublisherCollect<T, C> : IMono<C>, IFuseable
    {
        readonly IPublisher<T> source;

        readonly Func<C> collectionSupplier;

        readonly Action<C, T> collector;

        internal PublisherCollect(IPublisher<T> source, Func<C> collectionSupplier, Action<C, T> collector)
        {
            this.source = source;
            this.collectionSupplier = collectionSupplier;
            this.collector = collector;
        }

        public void Subscribe(ISubscriber<C> s)
        {
            C c;

            try
            {
                c = collectionSupplier();
            }
            catch (Exception ex)
            {
                ExceptionHelper.ThrowIfFatal(ex);
                EmptySubscription<C>.Error(s, ex);
                return;
            }

            CollectSubscriber parent = new CollectSubscriber(s, c, collector);

            source.Subscribe(parent);
        }

        sealed class CollectSubscriber : DeferredScalarSubscriber<T, C>
        {
            readonly Action<C, T> collector;

            internal CollectSubscriber(ISubscriber<C> actual, C collection, Action<C, T> collector) : base(actual)
            {
                this.value = collection;
                this.collector = collector;
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
                try
                {
                    collector(value, t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return;
                }
            }

            protected override void OnStart()
            {
                s.Request(long.MaxValue);
            }
        }
    }
}
