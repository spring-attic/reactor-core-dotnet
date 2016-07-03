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
    sealed class PublisherSample<T, U> : IFlux<T>
    {
        readonly IPublisher<T> source;

        readonly IPublisher<U> sampler;

        internal PublisherSample(IPublisher<T> source, IPublisher<U> sampler)
        {
            this.source = source;
            this.sampler = sampler;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            var parent = new SampleSubscriber(s);
            var other = new OtherSubscriber(parent);

            s.OnSubscribe(parent);

            sampler.Subscribe(other);

            source.Subscribe(parent);
        }

        sealed class SampleSubscriber : ISubscriber<T>, ISubscription
        {
            readonly ISubscriber<T> actual;

            ISubscription s;

            ISubscription z;

            long requested;

            Node latest;

            HalfSerializerStruct serializer;

            internal SampleSubscriber(ISubscriber<T> actual)
            {
                this.actual = actual;
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    s.Request(long.MaxValue);
                }
            }

            public void OnNext(T t)
            {
                Interlocked.Exchange(ref latest, new Node(t));
            }

            public void OnError(Exception e)
            {
                SubscriptionHelper.Cancel(ref z);
                Interlocked.Exchange(ref latest, null);
                serializer.OnError(actual, e);
            }

            public void OnComplete()
            {
                SubscriptionHelper.Cancel(ref z);
                OtherNext();
                serializer.OnComplete(actual);
            }

            public void Request(long n)
            {
                BackpressureHelper.DeferredRequest(ref z, ref requested, n);
            }

            internal void OtherSubscribe(ISubscription s)
            {
                BackpressureHelper.DeferredSetOnce(ref z, ref requested, s);
            }

            internal void OtherNext()
            {
                Node n = Interlocked.Exchange(ref latest, null);
                
                if (n != null)
                {
                    serializer.OnNext(actual, n.value);
                }
            }

            internal void OtherError(Exception ex)
            {
                SubscriptionHelper.Cancel(ref s);
                Interlocked.Exchange(ref latest, null);
                serializer.OnError(actual, ex);
            }

            internal void OtherComplete()
            {
                SubscriptionHelper.Cancel(ref s);
                OtherNext();
                serializer.OnComplete(actual);
            }

            public void Cancel()
            {
                SubscriptionHelper.Cancel(ref s);
                SubscriptionHelper.Cancel(ref z);
            }
        }

        sealed class OtherSubscriber : ISubscriber<U>
        {
            readonly SampleSubscriber parent;

            internal OtherSubscriber(SampleSubscriber parent)
            {
                this.parent = parent;
            }

            public void OnComplete()
            {
                parent.OtherComplete();
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

        sealed class Node
        {
            internal readonly T value;

            internal Node(T value)
            {
                this.value = value;
            }
        }
    }
}
