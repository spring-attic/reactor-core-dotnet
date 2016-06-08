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
    sealed class PublisherTake<T> : IFlux<T>, IFuseable
    {
        readonly IPublisher<T> source;

        readonly long n;

        public PublisherTake(IPublisher<T> source, long n)
        {
            this.source = source;
            this.n = n;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                source.Subscribe(new TakeConditionalSubscriber((IConditionalSubscriber<T>)s, n));
            }
            else
            {
                source.Subscribe(new TakeSubscriber(s, n));
            }
        }

        sealed class TakeSubscriber : BasicFuseableSubscriber<T, T>
        {
            readonly long n;

            long remaining;

            int once;

            public TakeSubscriber(ISubscriber<T> actual, long n) : base(actual)
            {
                this.n = n;
                this.remaining = n;
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
                if (done)
                {
                    return;
                }
                if (fusionMode == FuseableHelper.ASYNC)
                {
                    actual.OnNext(t);
                    return;
                }
                long r = remaining;
                if (r == 0L)
                {
                    s.Cancel();
                    Complete();
                    return;
                }
                actual.OnNext(t);
                if (--r == 0)
                {
                    s.Cancel();
                    Complete();
                    return;
                }
                remaining = r;
            }

            public override int RequestFusion(int mode)
            {
                if (qs != null)
                {
                    return TransitiveAnyFusion(mode);
                }
                return FuseableHelper.NONE;
            }

            public override void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    if (Volatile.Read(ref once) == 0 && Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        if (n >= this.n)
                        {
                            s.Request(long.MaxValue);
                            return;
                        }
                    }
                    s.Request(n);
                }
            }

            public override bool Poll(out T value)
            {
                long r = remaining;
                if (r == 0)
                {
                    if (fusionMode == FuseableHelper.ASYNC && !done)
                    {
                        done = true;
                        qs.Cancel();
                        actual.OnComplete();
                    }
                    value = default(T);
                    return false;
                }
                remaining = r - 1;
                return qs.Poll(out value);
            }
        }

        sealed class TakeConditionalSubscriber : BasicFuseableConditionalSubscriber<T, T>
        {
            readonly long n;

            long remaining;

            int once;

            public TakeConditionalSubscriber(IConditionalSubscriber<T> actual, long n) : base(actual)
            {
                this.n = n;
                this.remaining = n;
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
                if (done)
                {
                    return;
                }
                if (fusionMode == FuseableHelper.ASYNC)
                {
                    actual.OnNext(t);
                    return;
                }
                long r = remaining;
                if (r == 0L)
                {
                    s.Cancel();
                    Complete();
                    return;
                }
                actual.OnNext(t);
                if (--r == 0)
                {
                    s.Cancel();
                    Complete();
                    return;
                }
                remaining = r;
            }

            public override bool TryOnNext(T t)
            {
                if (done)
                {
                    return true;
                }
                if (fusionMode == FuseableHelper.ASYNC)
                {
                    return actual.TryOnNext(t);
                }
                long r = remaining;
                if (r == 0L)
                {
                    s.Cancel();
                    Complete();
                    return true;
                }
                bool b = actual.TryOnNext(t);
                if (--r == 0)
                {
                    s.Cancel();
                    Complete();
                    return true;
                }
                return b;
            }

            public override int RequestFusion(int mode)
            {
                if (qs != null)
                {
                    return TransitiveAnyFusion(mode);
                }
                return FuseableHelper.NONE;
            }

            public override void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    if (Volatile.Read(ref once) == 0 && Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        if (n >= this.n)
                        {
                            s.Request(long.MaxValue);
                            return;
                        }
                    }
                    s.Request(n);
                }
            }

            public override bool Poll(out T value)
            {
                long r = remaining;
                if (r == 0)
                {
                    if (fusionMode == FuseableHelper.ASYNC && !done)
                    {
                        done = true;
                        qs.Cancel();
                        actual.OnComplete();
                    }
                    value = default(T);
                    return false;
                }
                remaining = r - 1;
                return qs.Poll(out value);
            }

            public override bool IsEmpty()
            {
                return remaining == 0 || qs.IsEmpty();
            }
        }
    }
}
