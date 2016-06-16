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
    sealed class PublisherDefaultIfEmpty<T> : IFlux<T>
    {
        readonly IPublisher<T> source;

        readonly T defaultValue;

        internal PublisherDefaultIfEmpty(IPublisher<T> source, T defaultValue)
        {
            this.source = source;
            this.defaultValue = defaultValue;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                source.Subscribe(new DefaultIfEmptyConditionalSubscriber((s as IConditionalSubscriber<T>), defaultValue));
            } else
            {
                source.Subscribe(new DefaultIfEmptySubscriber(s, defaultValue));
            }
        }

        sealed class DefaultIfEmptySubscriber : ISubscriber<T>, ISubscription, IQueue<T>
        {
            readonly ISubscriber<T> actual;

            readonly T defaultValue;

            bool taken;

            bool hasValue;

            ISubscription s;

            bool cancelled;

            long requested;

            internal DefaultIfEmptySubscriber(ISubscriber<T> actual, T defaultValue)
            {
                this.actual = actual;
                this.defaultValue = defaultValue;
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    actual.OnSubscribe(this);
                }
            }

            public void OnNext(T t)
            {
                if (!hasValue)
                {
                    hasValue = true;
                }
                actual.OnNext(t);
            }

            public void OnError(Exception e)
            {
                actual.OnError(e);
            }

            public void OnComplete()
            {
                if (!hasValue)
                {
                    BackpressureHelper.PostComplete(ref requested, actual, this, ref cancelled);
                }
                else
                {
                    actual.OnComplete();
                }
            }

            public bool Offer(T value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out T value)
            {
                if (!taken)
                {
                    taken = true;
                    value = this.defaultValue;
                    return true;
                }
                value = default(T);
                return false;
            }

            public bool IsEmpty()
            {
                return taken;
            }

            public void Clear()
            {
                taken = true;
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    if (!BackpressureHelper.PostCompleteRequest(ref requested, n, actual, this, ref cancelled))
                    {
                        s.Request(n);
                    }
                }
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                s.Cancel();
            }
        }

        sealed class DefaultIfEmptyConditionalSubscriber : IConditionalSubscriber<T>, ISubscription, IQueue<T>
        {
            readonly IConditionalSubscriber<T> actual;

            readonly T defaultValue;

            bool taken;

            bool hasValue;

            ISubscription s;

            bool cancelled;

            long requested;

            internal DefaultIfEmptyConditionalSubscriber(IConditionalSubscriber<T> actual, T defaultValue)
            {
                this.actual = actual;
                this.defaultValue = defaultValue;
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    actual.OnSubscribe(this);
                }
            }

            public void OnNext(T t)
            {
                if (!hasValue)
                {
                    hasValue = true;
                }
                actual.OnNext(t);
            }

            public bool TryOnNext(T t)
            {
                if (!hasValue)
                {
                    hasValue = true;
                }
                return actual.TryOnNext(t);
            }

            public void OnError(Exception e)
            {
                actual.OnError(e);
            }

            public void OnComplete()
            {
                if (!hasValue)
                {
                    BackpressureHelper.PostComplete(ref requested, actual, this, ref cancelled);
                }
                else
                {
                    actual.OnComplete();
                }
            }

            public bool Offer(T value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out T value)
            {
                if (!taken)
                {
                    taken = true;
                    value = this.defaultValue;
                    return true;
                }
                value = default(T);
                return false;
            }

            public bool IsEmpty()
            {
                return taken;
            }

            public void Clear()
            {
                taken = true;
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    if (!BackpressureHelper.PostCompleteRequest(ref requested, n, actual, this, ref cancelled))
                    {
                        s.Request(n);
                    }
                }
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                s.Cancel();
            }
        }
    }
}
