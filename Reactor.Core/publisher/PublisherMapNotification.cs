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
    sealed class PublisherMapNotification<T, R> : IFlux<IPublisher<R>>
    {
        readonly IPublisher<T> source;

        readonly Func<T, IPublisher<R>> onNextMapper;

        readonly Func<Exception, IPublisher<R>> onErrorMapper;

        readonly Func<IPublisher<R>> onCompleteMapper;

        public PublisherMapNotification(IPublisher<T> source,
            Func<T, IPublisher<R>> onNextMapper,
            Func<Exception, IPublisher<R>> onErrorMapper,
            Func<IPublisher<R>> onCompleteMapper)
        {
            this.source = source;
            this.onNextMapper = onNextMapper;
            this.onErrorMapper = onErrorMapper;
            this.onCompleteMapper = onCompleteMapper;
        }

        public void Subscribe(ISubscriber<IPublisher<R>> s)
        {
            source.Subscribe(new MapNotificationSubscriber(s, onNextMapper, onErrorMapper, onCompleteMapper));
        }

        sealed class MapNotificationSubscriber : BasicSinglePostCompleteSubscriber<T, IPublisher<R>>
        {
            readonly Func<T, IPublisher<R>> onNextMapper;

            readonly Func<Exception, IPublisher<R>> onErrorMapper;

            readonly Func<IPublisher<R>> onCompleteMapper;

            internal MapNotificationSubscriber(
                ISubscriber<IPublisher<R>> actual,
                Func<T, IPublisher<R>> onNextMapper,
                Func<Exception, IPublisher<R>> onErrorMapper,
                Func<IPublisher<R>> onCompleteMapper) : base(actual)
            {
                this.onNextMapper = onNextMapper;
                this.onErrorMapper = onErrorMapper;
                this.onCompleteMapper = onCompleteMapper;
            }

            public override void OnNext(T t)
            {
                produced++;

                IPublisher<R> p;

                try
                {
                    p = ObjectHelper.RequireNonNull(onNextMapper(t), "The onNextMapper returned a null IPublisher");
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return;
                }

                actual.OnNext(p);
            }

            public override void OnError(Exception e)
            {
                IPublisher<R> last;
                try
                {
                    last = ObjectHelper.RequireNonNull(onErrorMapper(e), "The onErrorMapper returned a null IPublisher");
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return;
                }

                Complete(last);
            }

            public override void OnComplete()
            {
                IPublisher<R> last;
                try
                {
                    last = ObjectHelper.RequireNonNull(onCompleteMapper(), "The onCompleteMapper returned a null IPublisher");
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return;
                }

                Complete(last);
            }
        }
    }
}
