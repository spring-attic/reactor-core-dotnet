using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core.flow;
using Reactor.Core.subscriber;
using Reactor.Core.subscription;
using Reactor.Core.util;
using System.Threading;
using System.Runtime.InteropServices;

namespace Reactor.Core.publisher
{
    sealed class PublisherOnBackpressureDrop<T> : IFlux<T>
    {
        readonly IPublisher<T> source;

        readonly Action<T> onDrop;

        internal PublisherOnBackpressureDrop(IPublisher<T> source, Action<T> onDrop)
        {
            this.source = source;
            this.onDrop = onDrop;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                source.Subscribe(new OnBackpressureDropConditionalSubscriber((IConditionalSubscriber<T>)s, onDrop));
            }
            else
            {
                source.Subscribe(new OnBackpressureDropSubscriber(s, onDrop));
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class OnBackpressureDropSubscriber : ISubscriber<T>, ISubscription
        {
            readonly ISubscriber<T> actual;

            readonly Action<T> onDrop;

            ISubscription s;

            bool done;

            Pad128 p0;

            long requested;

            Pad120 p1;

            long produced;

            Pad120 p2;

            internal OnBackpressureDropSubscriber(ISubscriber<T> actual, Action<T> onDrop)
            {
                this.actual = actual;
                this.onDrop = onDrop;
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    actual.OnSubscribe(this);

                    s.Request(long.MaxValue);
                }
            }

            public void OnNext(T t)
            {
                if (done)
                {
                    return;
                }

                long r = Volatile.Read(ref requested);
                long p = produced;

                if (p != r)
                {
                    produced = p + 1;
                    actual.OnNext(t);
                }
                else
                {
                    try
                    {
                        onDrop?.Invoke(t);
                    }
                    catch (Exception ex)
                    {
                        ExceptionHelper.ThrowIfFatal(ex);
                        s.Cancel();
                        OnError(ex);
                    }
                }
            }

            public void OnError(Exception e)
            {
                if (done)
                {
                    ExceptionHelper.OnErrorDropped(e);
                    return;
                }
                done = true;
                actual.OnError(e);
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;
                actual.OnComplete();
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                }
            }

            public void Cancel()
            {
                s.Cancel();
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class OnBackpressureDropConditionalSubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IConditionalSubscriber<T> actual;

            readonly Action<T> onDrop;

            ISubscription s;

            bool done;

            Pad128 p0;

            long requested;

            Pad120 p1;

            long produced;

            Pad120 p2;

            internal OnBackpressureDropConditionalSubscriber(IConditionalSubscriber<T> actual, Action<T> onDrop)
            {
                this.actual = actual;
                this.onDrop = onDrop;
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    actual.OnSubscribe(this);

                    s.Request(long.MaxValue);
                }
            }

            public void OnNext(T t)
            {
                if (done)
                {
                    return;
                }

                long r = Volatile.Read(ref requested);
                long p = produced;

                if (p != r)
                {
                    if (actual.TryOnNext(t))
                    {
                        produced = p + 1;
                    }
                }
                else
                {
                    try
                    {
                        onDrop?.Invoke(t);
                    }
                    catch (Exception ex)
                    {
                        ExceptionHelper.ThrowIfFatal(ex);
                        s.Cancel();
                        OnError(ex);
                    }
                }
            }

            public bool TryOnNext(T t)
            {
                if (done)
                {
                    return true;
                }

                long r = Volatile.Read(ref requested);
                long p = produced;

                if (p != r)
                {
                    if (actual.TryOnNext(t))
                    {
                        produced = p + 1;
                        return true;
                    }
                }
                else
                {
                    try
                    {
                        onDrop?.Invoke(t);
                    }
                    catch (Exception ex)
                    {
                        ExceptionHelper.ThrowIfFatal(ex);
                        s.Cancel();
                        OnError(ex);
                    }
                }
                return false;
            }

            public void OnError(Exception e)
            {
                if (done)
                {
                    ExceptionHelper.OnErrorDropped(e);
                    return;
                }
                done = true;
                actual.OnError(e);
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;
                actual.OnComplete();
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                }
            }

            public void Cancel()
            {
                s.Cancel();
            }
        }

    }
}
