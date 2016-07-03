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
    sealed class PublisherTimeout<T, U, V> : IFlux<T>, IMono<T>
    {
        readonly IPublisher<T> source;

        readonly IPublisher<U> firstTimeout;

        readonly Func<T, IPublisher<V>> itemTimeout;

        readonly IPublisher<T> fallback;

        internal PublisherTimeout(IPublisher<T> source, IPublisher<U> firstTimeout,
            Func<T, IPublisher<V>> itemTimeout, IPublisher<T> fallback)
        {
            this.source = source;
            this.firstTimeout = firstTimeout;
            this.itemTimeout = itemTimeout;
            this.fallback = fallback;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                var parent = new TimeoutConditionalSubscriber((IConditionalSubscriber<T>)s, firstTimeout, itemTimeout, fallback);
                s.OnSubscribe(parent);
                source.Subscribe(parent);
            }
            else
            {
                var parent = new TimeoutSubscriber(s, firstTimeout, itemTimeout, fallback);
                s.OnSubscribe(parent);
                source.Subscribe(parent);
            }
        }

        interface TimeoutHelper : ISubscriber<T>, ISubscription
        {
            void TimeoutError(long idx, Exception e);

            void Timeout(long idx);
        }

        sealed class TimeoutSubscriber : TimeoutHelper
        {
            readonly ISubscriber<T> actual;

            readonly IPublisher<U> firstTimeout;

            readonly Func<T, IPublisher<V>> itemTimeout;

            readonly IPublisher<T> fallback;

            SubscriptionArbiterStruct arbiter;

            long index;

            IDisposable d;

            long produced;

            internal TimeoutSubscriber(ISubscriber<T> actual, IPublisher<U> firstTimeout, Func<T, IPublisher<V>> itemTimeout, IPublisher<T> fallback)
            {
                this.actual = actual;
                this.firstTimeout = firstTimeout;
                this.itemTimeout = itemTimeout;
                this.fallback = fallback;
            }

            public void Cancel()
            {
                DisposableHelper.Dispose(ref d);
                arbiter.Cancel();
            }

            public void OnComplete()
            {
                if (Interlocked.Exchange(ref index, long.MaxValue) != long.MaxValue)
                {
                    DisposableHelper.Dispose(ref d);

                    actual.OnComplete();
                }
            }

            public void OnError(Exception e)
            {
                if (Interlocked.Exchange(ref index, long.MaxValue) != long.MaxValue)
                {
                    DisposableHelper.Dispose(ref d);

                    actual.OnError(e);
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public void TimeoutError(long idx, Exception e)
            {
                if (Interlocked.CompareExchange(ref index, long.MaxValue, idx) == idx)
                {
                    arbiter.Cancel();

                    actual.OnError(e);
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public void OnNext(T t)
            {
                long idx = Volatile.Read(ref index);
                if (idx != long.MaxValue)
                {
                    if (Interlocked.CompareExchange(ref index, idx + 1, idx) == idx)
                    {
                        d?.Dispose();

                        produced++;

                        actual.OnNext(t);

                        IPublisher<V> p;

                        try
                        {
                            p = ObjectHelper.RequireNonNull(itemTimeout(t), "The itemTimeout returned a null IPublisher");
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);
                            arbiter.Cancel();
                            Interlocked.Exchange(ref index, long.MaxValue);
                            actual.OnError(ex);
                            return;
                        }
                        var dt = new ItemTimeout(this, idx + 1);
                        if (DisposableHelper.Replace(ref this.d, dt))
                        {
                            p.Subscribe(dt);
                        }
                    }
                }
            }

            public void OnSubscribe(ISubscription s)
            {
                var d = new FirstTimeout(this);
                if (DisposableHelper.Replace(ref this.d, d))
                {
                    firstTimeout.Subscribe(d);

                    arbiter.Set(s); // FIXME this races with the timeout switching to the fallback
                }
            }

            public void Request(long n)
            {
                arbiter.ValidateAndRequest(n);
            }

            public void Timeout(long idx)
            {
                if (Interlocked.CompareExchange(ref index, long.MaxValue, idx) == idx)
                {
                    arbiter.Set(EmptySubscription<T>.Instance);
                    var fallback = this.fallback;

                    if (fallback != null)
                    {
                        long p = produced;
                        if (p != 0L)
                        {
                            arbiter.Produced(p);
                        }

                        fallback.Subscribe(new FallbackSubscriber(this, actual));
                    }
                    else
                    {
                        actual.OnError(new TimeoutException("The " + idx + "th element timed out"));
                    }
                }
            }

            sealed class FallbackSubscriber : ISubscriber<T>
            {
                readonly TimeoutSubscriber parent;

                readonly ISubscriber<T> actual;

                internal FallbackSubscriber(TimeoutSubscriber parent, ISubscriber<T> actual)
                {
                    this.actual = actual;
                    this.parent = parent;
                }

                public void OnComplete()
                {
                    actual.OnComplete();
                }

                public void OnError(Exception e)
                {
                    actual.OnError(e);
                }

                public void OnNext(T t)
                {
                    actual.OnNext(t);
                }

                public void OnSubscribe(ISubscription s)
                {
                    parent.arbiter.Set(s);
                }
            }
        }

        sealed class TimeoutConditionalSubscriber : TimeoutHelper, IConditionalSubscriber<T>
        {
            readonly IConditionalSubscriber<T> actual;

            readonly IPublisher<U> firstTimeout;

            readonly Func<T, IPublisher<V>> itemTimeout;

            readonly IPublisher<T> fallback;

            SubscriptionArbiterStruct arbiter;

            long index;

            IDisposable d;

            long produced;

            internal TimeoutConditionalSubscriber(IConditionalSubscriber<T> actual, IPublisher<U> firstTimeout, Func<T, IPublisher<V>> itemTimeout, IPublisher<T> fallback)
            {
                this.actual = actual;
                this.firstTimeout = firstTimeout;
                this.itemTimeout = itemTimeout;
                this.fallback = fallback;
            }

            public void Cancel()
            {
                DisposableHelper.Dispose(ref d);
                arbiter.Cancel();
            }

            public void OnComplete()
            {
                if (Interlocked.Exchange(ref index, long.MaxValue) != long.MaxValue)
                {
                    DisposableHelper.Dispose(ref d);

                    actual.OnComplete();
                }
            }

            public void OnError(Exception e)
            {
                if (Interlocked.Exchange(ref index, long.MaxValue) != long.MaxValue)
                {
                    DisposableHelper.Dispose(ref d);

                    actual.OnError(e);
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public void TimeoutError(long idx, Exception e)
            {
                if (Interlocked.CompareExchange(ref index, long.MaxValue, idx) == idx)
                {
                    arbiter.Cancel();

                    actual.OnError(e);
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public void OnNext(T t)
            {
                long idx = Volatile.Read(ref index);
                if (idx != long.MaxValue)
                {
                    if (Interlocked.CompareExchange(ref index, idx + 1, idx) == idx)
                    {
                        d?.Dispose();

                        produced++;

                        actual.OnNext(t);

                        IPublisher<V> p;

                        try
                        {
                            p = ObjectHelper.RequireNonNull(itemTimeout(t), "The itemTimeout returned a null IPublisher");
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);
                            arbiter.Cancel();
                            Interlocked.Exchange(ref index, long.MaxValue);
                            actual.OnError(ex);
                            return;
                        }
                        var dt = new ItemTimeout(this, idx + 1);
                        if (DisposableHelper.Replace(ref this.d, dt))
                        {
                            p.Subscribe(dt);
                        }
                    }
                }
            }

            public bool TryOnNext(T t)
            {
                long idx = Volatile.Read(ref index);
                if (idx != long.MaxValue)
                {
                    if (Interlocked.CompareExchange(ref index, idx + 1, idx) == idx)
                    {
                        d?.Dispose();

                        produced++;

                        bool b = actual.TryOnNext(t);

                        IPublisher<V> p;

                        try
                        {
                            p = ObjectHelper.RequireNonNull(itemTimeout(t), "The itemTimeout returned a null IPublisher");
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);
                            arbiter.Cancel();
                            Interlocked.Exchange(ref index, long.MaxValue);
                            actual.OnError(ex);
                            return false;
                        }
                        var dt = new ItemTimeout(this, idx + 1);
                        if (DisposableHelper.Replace(ref this.d, dt))
                        {
                            p.Subscribe(dt);
                        }

                        return b;
                    }
                }
                return false;
            }

            public void OnSubscribe(ISubscription s)
            {
                var d = new FirstTimeout(this);
                if (DisposableHelper.Replace(ref this.d, d))
                {
                    firstTimeout.Subscribe(d);

                    arbiter.Set(s);
                }
            }

            public void Request(long n)
            {
                arbiter.ValidateAndRequest(n);
            }

            public void Timeout(long idx)
            {
                if (Interlocked.CompareExchange(ref index, long.MaxValue, idx) == idx)
                {
                    arbiter.Set(EmptySubscription<T>.Instance);
                    var fallback = this.fallback;

                    if (fallback != null)
                    {
                        long p = produced;
                        if (p != 0L)
                        {
                            arbiter.Produced(p);
                        }

                        fallback.Subscribe(new FallbackConditionalSubscriber(this, actual));
                    }
                    else
                    {
                        actual.OnError(new TimeoutException("The " + idx + "th element timed out"));
                    }
                }
            }

            sealed class FallbackConditionalSubscriber : IConditionalSubscriber<T>
            {
                readonly TimeoutConditionalSubscriber parent;

                readonly IConditionalSubscriber<T> actual;

                internal FallbackConditionalSubscriber(TimeoutConditionalSubscriber parent, IConditionalSubscriber<T> actual)
                {
                    this.actual = actual;
                    this.parent = parent;
                }

                public void OnComplete()
                {
                    actual.OnComplete();
                }

                public void OnError(Exception e)
                {
                    actual.OnError(e);
                }

                public void OnNext(T t)
                {
                    actual.OnNext(t);
                }

                public void OnSubscribe(ISubscription s)
                {
                    parent.arbiter.Set(s);
                }

                public bool TryOnNext(T t)
                {
                    return actual.TryOnNext(t);
                }
            }
        }

        sealed class FirstTimeout : ISubscriber<U>, IDisposable
        {
            readonly TimeoutHelper parent;

            ISubscription s;

            internal FirstTimeout(TimeoutHelper parent)
            {
                this.parent = parent;
            }

            public void Dispose()
            {
                SubscriptionHelper.Cancel(ref s);
            }

            public void OnComplete()
            {
                parent.Timeout(0L);
            }

            public void OnError(Exception e)
            {
                parent.TimeoutError(0L, e);
            }

            public void OnNext(U t)
            {
                s.Cancel();
                parent.Timeout(0L);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    s.Request(long.MaxValue);
                }
            }
        }

        sealed class ItemTimeout : ISubscriber<V>, IDisposable
        {
            readonly TimeoutHelper parent;

            readonly long index;

            ISubscription s;

            internal ItemTimeout(TimeoutHelper parent, long index)
            {
                this.parent = parent;
                this.index = index;
            }

            public void Dispose()
            {
                SubscriptionHelper.Cancel(ref s);
            }

            public void OnComplete()
            {
                parent.Timeout(index);
            }

            public void OnError(Exception e)
            {
                parent.TimeoutError(index, e);
            }

            public void OnNext(V t)
            {
                s.Cancel();
                parent.Timeout(index);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    s.Request(long.MaxValue);
                }
            }
        }
    }
}
