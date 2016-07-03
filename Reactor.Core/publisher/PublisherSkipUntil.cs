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
    sealed class PublisherSkipUntil<T, U> : IFlux<T>
    {
        readonly IPublisher<T> source;

        readonly IPublisher<U> other;

        internal PublisherSkipUntil(IPublisher<T> source, IPublisher<U> other)
        {
            this.source = source;
            this.other = other;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            SkipUntilHelper parent;
            if (s is IConditionalSubscriber<T>)
            {
                parent = new SkipUntilConditionalSubscriber((IConditionalSubscriber<T>)s);
            }
            else
            {
                parent = new SkipUntilSubscriber(s);
            }

            var until = new UntilSubscriber(parent);

            s.OnSubscribe(parent);

            other.Subscribe(until);

            source.Subscribe(parent);
        }

        interface SkipUntilHelper : IConditionalSubscriber<T>, ISubscription
        {
            void OtherSubscribe(ISubscription s);

            void OtherNext();

            void OtherError(Exception ex);
        }

        sealed class SkipUntilConditionalSubscriber : SkipUntilHelper
        {
            readonly IConditionalSubscriber<T> actual;

            ISubscription s;

            long requested;

            ISubscription other;

            bool gate;

            HalfSerializerStruct serializer;

            internal SkipUntilConditionalSubscriber(IConditionalSubscriber<T> actual)
            {
                this.actual = actual;
            }

            public void OnSubscribe(ISubscription s)
            {
                BackpressureHelper.DeferredSetOnce(ref this.s, ref requested, s);
            }

            public void OnNext(T t)
            {
                if (!TryOnNext(t))
                {
                    s.Request(1);
                }
            }

            public bool TryOnNext(T t)
            {
                if (gate)
                {
                    return serializer.TryOnNext(actual, t);
                }
                return false;
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

            public void OtherSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref other, s))
                {
                    s.Request(long.MaxValue);
                }
            }

            public void OtherNext()
            {
                gate = true;
                SubscriptionHelper.Cancel(ref other);
            }

            public void OtherError(Exception ex)
            {
                serializer.OnError(actual, ex);
            }
        }

        sealed class SkipUntilSubscriber : SkipUntilHelper
        {
            readonly ISubscriber<T> actual;

            ISubscription s;

            long requested;

            ISubscription other;

            bool gate;

            HalfSerializerStruct serializer;

            internal SkipUntilSubscriber(ISubscriber<T> actual)
            {
                this.actual = actual;
            }

            public void OnSubscribe(ISubscription s)
            {
                BackpressureHelper.DeferredSetOnce(ref this.s, ref requested, s);
            }

            public void OnNext(T t)
            {
                if (!TryOnNext(t))
                {
                    s.Request(1);
                }
            }

            public bool TryOnNext(T t)
            {
                if (gate)
                {
                    serializer.OnNext(actual, t);
                    return true;
                }
                return false;
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

            public void OtherSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref other, s))
                {
                    s.Request(long.MaxValue);
                }
            }

            public void OtherNext()
            {
                gate = true;
                SubscriptionHelper.Cancel(ref other);
            }

            public void OtherError(Exception ex)
            {
                serializer.OnError(actual, ex);
            }
        }

        sealed class UntilSubscriber : ISubscriber<U>
        {
            readonly SkipUntilHelper parent;

            internal UntilSubscriber(SkipUntilHelper parent)
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
