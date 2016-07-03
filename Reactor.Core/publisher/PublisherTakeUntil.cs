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
    sealed class PublisherTakeUntil<T, U> : IFlux<T>
    {
        readonly IPublisher<T> source;

        readonly IPublisher<U> other;

        internal PublisherTakeUntil(IPublisher<T> source, IPublisher<U> other)
        {
            this.source = source;
            this.other = other;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            TakeUntilHelper parent;
            if (s is IConditionalSubscriber<T>)
            {
                parent = new TakeUntilConditionalSubscriber((IConditionalSubscriber<T>)s);
            }
            else
            {
                parent = new TakeUntilSubscriber(s);
            }

            var until = new UntilSubscriber(parent);

            s.OnSubscribe(parent);

            other.Subscribe(until);

            source.Subscribe(parent);
        }

        interface TakeUntilHelper : ISubscriber<T>, ISubscription
        {
            void OtherNext();

            void OtherError(Exception e);

            void OtherSubscribe(ISubscription s);
        }

        sealed class TakeUntilSubscriber : TakeUntilHelper
        {
            readonly ISubscriber<T> actual;

            ISubscription s;

            long requested;

            ISubscription other;

            HalfSerializerStruct serializer;

            internal TakeUntilSubscriber(ISubscriber<T> actual)
            {
                this.actual = actual;
            }

            public void OtherNext()
            {
                Cancel();
                serializer.OnComplete(actual);
            }

            public void OtherError(Exception e)
            {
                SubscriptionHelper.Cancel(ref s);
                serializer.OnError(actual, e);
            }

            public void OtherSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref other, s))
                {
                    s.Request(long.MaxValue);
                }
            }

            public void OnSubscribe(ISubscription s)
            {
                BackpressureHelper.DeferredSetOnce(ref this.s, ref requested, s);
            }

            public void OnNext(T t)
            {
                serializer.OnNext(actual, t);
            }

            public void OnError(Exception e)
            {
                SubscriptionHelper.Cancel(ref other);
                serializer.OnError(actual, e);
            }

            public void OnComplete()
            {
                SubscriptionHelper.Cancel(ref other);
                serializer.OnComplete(actual);
            }

            public void Request(long n)
            {
                BackpressureHelper.DeferredRequest(ref s, ref requested, n);
            }

            public void Cancel()
            {
                SubscriptionHelper.Cancel(ref s);
                SubscriptionHelper.Cancel(ref other);
            }
        }

        sealed class TakeUntilConditionalSubscriber : TakeUntilHelper, IConditionalSubscriber<T>
        {
            readonly IConditionalSubscriber<T> actual;

            ISubscription s;

            long requested;

            ISubscription other;

            HalfSerializerStruct serializer;

            internal TakeUntilConditionalSubscriber(IConditionalSubscriber<T> actual)
            {
                this.actual = actual;
            }

            public void OtherNext()
            {
                Cancel();
                serializer.OnComplete(actual);
            }

            public void OtherError(Exception e)
            {
                SubscriptionHelper.Cancel(ref s);
                serializer.OnError(actual, e);
            }

            public void OtherSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref other, s))
                {
                    s.Request(long.MaxValue);
                }
            }

            public void OnSubscribe(ISubscription s)
            {
                BackpressureHelper.DeferredSetOnce(ref this.s, ref requested, s);
            }

            public void OnNext(T t)
            {
                serializer.OnNext(actual, t);
            }

            public bool TryOnNext(T t)
            {
                return serializer.TryOnNext(actual, t);
            }

            public void OnError(Exception e)
            {
                SubscriptionHelper.Cancel(ref other);
                serializer.OnError(actual, e);
            }

            public void OnComplete()
            {
                SubscriptionHelper.Cancel(ref other);
                serializer.OnComplete(actual);
            }

            public void Request(long n)
            {
                BackpressureHelper.DeferredRequest(ref s, ref requested, n);
            }

            public void Cancel()
            {
                SubscriptionHelper.Cancel(ref s);
                SubscriptionHelper.Cancel(ref other);
            }
        }

        sealed class UntilSubscriber : ISubscriber<U>
        {
            readonly TakeUntilHelper parent;

            internal UntilSubscriber(TakeUntilHelper parent)
            {
                this.parent = parent;
            }

            public void OnComplete()
            {
                parent.OtherNext();
            }

            public void OnError(Exception e)
            {
                parent.OtherError(e);
            }

            public void OnNext(U t)
            {
                parent.OtherNext();
            }

            public void OnSubscribe(ISubscription s)
            {
                parent.OtherSubscribe(s);
            }
        }
    }
}
