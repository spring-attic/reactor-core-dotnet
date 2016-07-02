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

        sealed class MapNotificationSubscriber : BasicSubscriber<T, IPublisher<R>>, IQueue<IPublisher<R>>
        {
            readonly Func<T, IPublisher<R>> onNextMapper;

            readonly Func<Exception, IPublisher<R>> onErrorMapper;

            readonly Func<IPublisher<R>> onCompleteMapper;

            IPublisher<R> last;

            long requested;

            bool cancelled;

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
                try
                {
                    last = ObjectHelper.RequireNonNull(onErrorMapper(e), "The onErrorMapper returned a null IPublisher");
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return;
                }

                BackpressureHelper.PostComplete(ref requested, actual, this, ref cancelled);
            }

            public override void OnComplete()
            {
                try
                {
                    last = ObjectHelper.RequireNonNull(onCompleteMapper(), "The onCompleteMapper returned a null IPublisher");
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return;
                }

                BackpressureHelper.PostComplete(ref requested, actual, this, ref cancelled);
            }

            public override void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    if (!BackpressureHelper.PostCompleteRequest(ref requested, n, actual, this, ref cancelled))
                    {
                        s.Request(n);
                    }
                }
            }

            public bool Offer(IPublisher<R> value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out IPublisher<R> value)
            {
                var o = last;
                if (o != null)
                {
                    last = null;
                    value = o;
                    return true;
                }
                value = null;
                return false;
            }

            public bool IsEmpty()
            {
                return last == null;
            }

            public void Clear()
            {
                last = null;
            }

            public override void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                base.Cancel();
            }
        }
    }
}
