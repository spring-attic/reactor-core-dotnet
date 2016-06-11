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
    sealed class PublisherSkip<T> : IFlux<T>
    {
        readonly IPublisher<T> source;

        readonly long n;

        public PublisherSkip(IPublisher<T> source, long n)
        {
            this.source = source;
            this.n = n;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                source.Subscribe(new SkipConditionalSubscriber((IConditionalSubscriber<T>)s, n));
            }
            else
            {
                source.Subscribe(new SkipSubscriber(s, n));
            }
        }

        sealed class SkipSubscriber : BasicFuseableSubscriber<T, T>
        {
            readonly long n;

            long remaining;

            public SkipSubscriber(ISubscriber<T> actual, long n) : base(actual)
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
                long r = remaining;
                if (r == 0L)
                {
                    actual.OnNext(t);
                    return;
                }
                remaining = r - 1;
            }

            public override bool Poll(out T value)
            {
                var qs = this.qs;
                long r = remaining;

                if (r == 0L)
                {
                    return qs.Poll(out value);
                }

                T local;
                for (;;)
                {
                    if (!qs.Poll(out local))
                    {
                        remaining = r;
                        value = default(T);
                        return false;
                    }
                    if (--r == 0)
                    {
                        remaining = 0;
                        return qs.Poll(out value);
                    }
                }
            }

            public override int RequestFusion(int mode)
            {
                return TransitiveAnyFusion(mode);
            }

            protected override void OnStart()
            {
                s.Request(n);
            }
        }

        sealed class SkipConditionalSubscriber : BasicFuseableConditionalSubscriber<T, T>
        {
            readonly long n;

            long remaining;

            public SkipConditionalSubscriber(IConditionalSubscriber<T> actual, long n) : base(actual)
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
                long r = remaining;
                if (r == 0L)
                {
                    actual.OnNext(t);
                    return;
                }
                remaining = r - 1;
            }

            public override bool TryOnNext(T t)
            {
                long r = remaining;
                if (r == 0L)
                {
                    return actual.TryOnNext(t);
                }
                remaining = r - 1;
                return true;
            }

            public override bool Poll(out T value)
            {
                var qs = this.qs;
                long r = remaining;

                if (r == 0L)
                {
                    return qs.Poll(out value);
                }

                T local;
                for (;;)
                {
                    if (!qs.Poll(out local))
                    {
                        remaining = r;
                        value = default(T);
                        return false;
                    }
                    if (--r == 0)
                    {
                        remaining = 0;
                        return qs.Poll(out value);
                    }
                }
            }

            public override int RequestFusion(int mode)
            {
                return TransitiveAnyFusion(mode);
            }

            protected override void OnStart()
            {
                s.Request(n);
            }
        }

    }
}
