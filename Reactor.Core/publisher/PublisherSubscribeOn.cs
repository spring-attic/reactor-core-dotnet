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
    sealed class PublisherSubscribeOn<T> : IFlux<T>, IMono<T>
    {
        readonly IPublisher<T> source;

        readonly Scheduler scheduler;

        internal PublisherSubscribeOn(IPublisher<T> source, Scheduler scheduler)
        {
            this.source = source;
            this.scheduler = scheduler;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (TrySingleSchedule(source, s, scheduler))
            {
                return;
            }

            var worker = scheduler.CreateWorker();

            if (s is IConditionalSubscriber<T>)
            {
                var parent = new SubscribeOnConditionalSubscriber((IConditionalSubscriber<T>)s, worker);

                s.OnSubscribe(parent);

                worker.Schedule(() => source.Subscribe(parent));
            }
            else
            {
                var parent = new SubscribeOnSubscriber(s, worker);

                s.OnSubscribe(parent);

                worker.Schedule(() => source.Subscribe(parent));
            }
        }

        /// <summary>
        /// Checks if the source is ICallable and if so, subscribes with a custom
        /// Subscription that schedules the first request on the scheduler directly
        /// and reads the Callable.Value on that thread.
        /// </summary>
        /// <param name="source">The source IPublisher to check</param>
        /// <param name="s">The target subscriber.</param>
        /// <param name="scheduler">The Scheduler to use.</param>
        /// <returns></returns>
        public static bool TrySingleSchedule(IPublisher<T> source, ISubscriber<T> s, Scheduler scheduler)
        {
            if (source is ICallable<T>)
            {
                s.OnSubscribe(new CallableSubscribeOn(s, (ICallable<T>)source, scheduler));
                return true;
            }
            return false;
        }

        sealed class SubscribeOnSubscriber : ISubscriber<T>, ISubscription
        {
            readonly ISubscriber<T> actual;

            readonly Worker worker;

            ISubscription s;

            long requested;

            internal SubscribeOnSubscriber(ISubscriber<T> actual, Worker worker)
            {
                this.actual = actual;
                this.worker = worker;
            }

            public void Cancel()
            {
                worker.Dispose();
                SubscriptionHelper.Cancel(ref s);
            }

            public void OnComplete()
            {
                try
                {
                    actual.OnComplete();
                }
                finally
                {
                    worker.Dispose();
                }
            }

            public void OnError(Exception e)
            {
                try
                {
                    actual.OnError(e);
                }
                finally
                {
                    worker.Dispose();
                }

            }
    
            public void OnNext(T t)
            {
                actual.OnNext(t);
            }

            public void OnSubscribe(ISubscription s)
            {
                BackpressureHelper.DeferredSetOnce(ref this.s, ref requested, s);
            }

            public void Request(long n)
            {
                worker.Schedule(() =>
                {
                    BackpressureHelper.DeferredRequest(ref s, ref requested, n);
                });
            }
        }

        sealed class SubscribeOnConditionalSubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IConditionalSubscriber<T> actual;

            readonly Worker worker;

            ISubscription s;

            long requested;

            internal SubscribeOnConditionalSubscriber(IConditionalSubscriber<T> actual, Worker worker)
            {
                this.actual = actual;
                this.worker = worker;
            }

            public void Cancel()
            {
                worker.Dispose();
                SubscriptionHelper.Cancel(ref s);
            }

            public void OnComplete()
            {
                try
                {
                    actual.OnComplete();
                }
                finally
                {
                    worker.Dispose();
                }
            }

            public void OnError(Exception e)
            {
                try
                {
                    actual.OnError(e);
                }
                finally
                {
                    worker.Dispose();
                }

            }

            public void OnNext(T t)
            {
                actual.OnNext(t);
            }

            public void OnSubscribe(ISubscription s)
            {
                BackpressureHelper.DeferredSetOnce(ref this.s, ref requested, s);
            }

            public void Request(long n)
            {
                worker.Schedule(() =>
                {
                    BackpressureHelper.DeferredRequest(ref s, ref requested, n);
                });
            }

            public bool TryOnNext(T t)
            {
                return actual.TryOnNext(t);
            }
        }

        sealed class CallableSubscribeOn : IQueueSubscription<T>
        {
            readonly ISubscriber<T> actual;

            readonly ICallable<T> callable;

            readonly Scheduler scheduler;

            IDisposable cancel;

            int once;

            int fusionMode;

            bool hasValue;

            internal CallableSubscribeOn(ISubscriber<T> actual, ICallable<T> callable, Scheduler scheduler)
            {
                this.actual = actual;
                this.callable = callable;
                this.scheduler = scheduler;
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
                    {
                        var d = scheduler.Schedule(Run);
                        DisposableHelper.Replace(ref cancel, d);
                    }
                }
            }

            void Run()
            {
                if (fusionMode != FuseableHelper.NONE)
                {
                    hasValue = true;
                    actual.OnNext(default(T));
                    actual.OnComplete();
                    return;
                }

                T t;

                try
                {
                    t = callable.Value;
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowIfFatal(ex);
                    if (!DisposableHelper.IsDisposed(ref cancel))
                    {
                        actual.OnError(ex);

                        cancel = null;
                    }
                    return;
                }

                if (!DisposableHelper.IsDisposed(ref cancel))
                {
                    actual.OnNext(t);

                    if (!DisposableHelper.IsDisposed(ref cancel))
                    {
                        actual.OnComplete();

                        cancel = null;
                    }
                }
            }

            public void Cancel()
            {
                DisposableHelper.Dispose(ref cancel);
            }

            public int RequestFusion(int mode)
            {
                if ((mode & FuseableHelper.BOUNDARY) != 0)
                {
                    return FuseableHelper.NONE;
                }
                int m = mode & FuseableHelper.ASYNC;
                fusionMode = m;
                return m;
            }

            public bool Offer(T value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out T value)
            {
                if (hasValue)
                {
                    hasValue = false;
                    value = callable.Value;
                    return true;
                }
                value = default(T);
                return false;
            }

            public bool IsEmpty()
            {
                return hasValue;
            }

            public void Clear()
            {
                hasValue = false;
            }
        }
    }
}
