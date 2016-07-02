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
    sealed class PublisherOnTerminateDetach<T> : IFlux<T>, IMono<T>
    {
        readonly IPublisher<T> source;

        internal PublisherOnTerminateDetach(IPublisher<T> source)
        {
            this.source = source;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                source.Subscribe(new OnTerminateDetachConditionalSubscriber((IConditionalSubscriber<T>)s));
            }
            else
            {
                source.Subscribe(new OnTerminateDetachSubscriber(s));
            }
        }

        abstract class BaseOnTerminateDetachSubscriber : IQueueSubscription<T>
        {
            ISubscription s;

            IQueueSubscription<T> qs;

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    qs = s as IQueueSubscription<T>;

                    subscribeActual();
                }
            }

            protected abstract void subscribeActual();

            public int RequestFusion(int mode)
            {
                var qs = this.qs;
                if (qs != null)
                {
                    return qs.RequestFusion(mode);
                }
                return FuseableHelper.NONE;
            }

            public bool Offer(T value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out T value)
            {
                var qs = this.qs;
                if (qs != null)
                {
                    return qs.Poll(out value);
                }
                value = default(T);
                return false;
            }

            public bool IsEmpty()
            {
                var qs = this.qs;
                if (qs != null)
                {
                    return qs.IsEmpty();
                }
                return true;
            }

            public void Clear()
            {
                var qs = this.qs;
                Cleanup();
                qs?.Clear();
            }

            public void Request(long n)
            {
                s?.Request(n);
            }

            public void Cancel()
            {
                var s = this.s;
                Cleanup();
                s.Cancel();
            }

            protected void Cleanup()
            {
                nullActual();
                this.s = SubscriptionHelper.Cancelled;
                qs = null;
            }

            protected abstract void nullActual();
        }

        sealed class OnTerminateDetachSubscriber : BaseOnTerminateDetachSubscriber, ISubscriber<T>
        {
            ISubscriber<T> actual;

            internal OnTerminateDetachSubscriber(ISubscriber<T> actual)
            {
                this.actual = actual;
            }

            public void OnNext(T t)
            {
                actual?.OnNext(t);
            }

            public void OnError(Exception e)
            {
                var a = actual;
                Cleanup();
                a?.OnError(e);
            }

            public void OnComplete()
            {
                var a = actual;
                Cleanup();
                a?.OnComplete();
            }

            protected override void subscribeActual()
            {
                actual.OnSubscribe(this);
            }

            protected override void nullActual()
            {
                actual = null;
            }
        }

        sealed class OnTerminateDetachConditionalSubscriber : BaseOnTerminateDetachSubscriber, IConditionalSubscriber<T>
        {
            IConditionalSubscriber<T> actual;

            internal OnTerminateDetachConditionalSubscriber(IConditionalSubscriber<T> actual)
            {
                this.actual = actual;
            }

            public void OnNext(T t)
            {
                actual?.OnNext(t);
            }

            public bool TryOnNext(T t)
            {
                var a = actual;
                if (a != null)
                {
                    return a.TryOnNext(t);
                }
                return false;
            }

            public void OnError(Exception e)
            {
                var a = actual;
                Cleanup();
                a?.OnError(e);
            }

            public void OnComplete()
            {
                var a = actual;
                Cleanup();
                a?.OnComplete();
            }

            protected override void subscribeActual()
            {
                actual.OnSubscribe(this);
            }

            protected override void nullActual()
            {
                actual = null;
            }
        }
    }
}
