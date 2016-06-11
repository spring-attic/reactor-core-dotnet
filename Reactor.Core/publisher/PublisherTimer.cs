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
    sealed class PublisherTimer : IFlux<long>, IMono<long>
    {
        readonly TimeSpan delay;

        readonly TimedScheduler scheduler;

        internal PublisherTimer(TimeSpan delay, TimedScheduler scheduler)
        {
            this.delay = delay;
            this.scheduler = scheduler;
        }

        public void Subscribe(ISubscriber<long> s)
        {
            TimerSubscription parent = new TimerSubscription(s);
            s.OnSubscribe(parent);

            parent.SetFuture(scheduler.Schedule(parent.Run, delay));
        }

        sealed class TimerSubscription : IQueueSubscription<long>
        {
            readonly ISubscriber<long> actual;

            IDisposable d;

            bool requested;

            bool available;

            internal TimerSubscription(ISubscriber<long> actual)
            {
                this.actual = actual;
            }

            internal void SetFuture(IDisposable d)
            {
                DisposableHelper.Replace(ref this.d, d);
            }

            internal void Run()
            {
                if (Volatile.Read(ref requested))
                {
                    available = true;
                    actual.OnNext(0);
                    if (!DisposableHelper.IsDisposed(ref d))
                    {
                        actual.OnComplete();
                    }
                }
                else
                {
                    actual.OnError(BackpressureHelper.MissingBackpressureException());
                }
            }

            public void Cancel()
            {
                DisposableHelper.Dispose(ref d);
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            public bool IsEmpty()
            {
                return !available;
            }

            public bool Offer(long value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out long value)
            {
                if (available)
                {
                    available = false;
                    value = 0;
                    return true;
                }
                value = 0;
                return false;
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    Volatile.Write(ref requested, true);
                }
            }

            public int RequestFusion(int mode)
            {
                return mode & FuseableHelper.ASYNC;
            }
        }
    }
}
