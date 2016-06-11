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
    sealed class PublisherPeek<T> : IFlux<T>, IMono<T>
    {
        readonly IPublisher<T> source;

        readonly Action<T> onNext;

        readonly Action<T> onAfterNext;

        readonly Action<Exception> onError;

        readonly Action onComplete;

        readonly Action onTerminate;

        readonly Action onAfterTerminate;

        readonly Action<long> onRequest;

        readonly Action<ISubscription> onSubscribe;

        readonly Action onCancel;

        internal PublisherPeek(
            IPublisher<T> source,
            Action<T> onNext,
            Action<T> onAfterNext,
            Action<Exception> onError,
            Action onComplete,
            Action onTerminate,
            Action onAfterTerminate,
            Action<long> onRequest,
            Action<ISubscription> onSubscribe,
            Action onCancel
        )
        {
            this.source = source;
            this.onNext = onNext;
            this.onAfterNext = onAfterNext;
            this.onError = onError;
            this.onComplete = onComplete;
            this.onTerminate = onTerminate;
            this.onAfterTerminate = onAfterTerminate;
            this.onRequest = onRequest;
            this.onSubscribe = onSubscribe;
            this.onCancel = onCancel;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                source.Subscribe(new PeekConditionalSubscriber((IConditionalSubscriber<T>)s, onNext, onAfterNext, onError, onComplete, onTerminate, onAfterTerminate, onRequest, onSubscribe, onCancel));
            }
            else
            {
                source.Subscribe(new PeekSubscriber(s, onNext, onAfterNext, onError, onComplete, onTerminate, onAfterTerminate, onRequest, onSubscribe, onCancel));
            }
        }

        internal static PublisherPeek<T> withOnNext(IPublisher<T> source, Action<T> onNext)
        {
            PublisherPeek<T> p = source as PublisherPeek<T>;
            if (p != null)
            {
                return new PublisherPeek<T>(p.source, v => { p.onNext(v); onNext(v); }, p.onAfterNext, p.onError, p.onComplete, p.onTerminate, p.onAfterTerminate, p.onRequest, p.onSubscribe, p.onCancel);
            }
            return new PublisherPeek<T>(source, onNext, v => { }, e => { }, () => { }, () => { }, () => { }, r => { }, s => { }, () => { });
        }

        internal static PublisherPeek<T> withOnAfterNext(IPublisher<T> source, Action<T> onAfterNext)
        {
            PublisherPeek<T> p = source as PublisherPeek<T>;
            if (p != null)
            {
                return new PublisherPeek<T>(p.source, p.onNext, v => { p.onAfterNext(v); onAfterNext(v); }, p.onError, p.onComplete, p.onTerminate, p.onAfterTerminate, p.onRequest, p.onSubscribe, p.onCancel);
            }
            return new PublisherPeek<T>(source, v => { }, onAfterNext, e => { }, () => { }, () => { }, () => { }, r => { }, s => { }, () => { });
        }

        internal static PublisherPeek<T> withOnError(IPublisher<T> source, Action<Exception> onError)
        {
            PublisherPeek<T> p = source as PublisherPeek<T>;
            if (p != null)
            {
                return new PublisherPeek<T>(p.source, p.onNext, p.onAfterNext, e => { p.onError(e); onError(e); }, p.onComplete, p.onTerminate, p.onAfterTerminate, p.onRequest, p.onSubscribe, p.onCancel);
            }
            return new PublisherPeek<T>(source, v => { }, v => { }, onError, () => { }, () => { }, () => { }, r => { }, s => { }, () => { });
        }

        internal static PublisherPeek<T> withOnComplete(IPublisher<T> source, Action onComplete)
        {
            PublisherPeek<T> p = source as PublisherPeek<T>;
            if (p != null)
            {
                return new PublisherPeek<T>(p.source, p.onNext, p.onAfterNext, p.onError, () => { p.onComplete(); onComplete(); }, p.onTerminate, p.onAfterTerminate, p.onRequest, p.onSubscribe, p.onCancel);
            }
            return new PublisherPeek<T>(source, v => { }, v => { }, e => { }, onComplete, () => { }, () => { }, r => { }, s => { }, () => { });
        }

        internal static PublisherPeek<T> withOnTerminate(IPublisher<T> source, Action onTerminate)
        {
            PublisherPeek<T> p = source as PublisherPeek<T>;
            if (p != null)
            {
                return new PublisherPeek<T>(p.source, p.onNext, p.onAfterNext, p.onError, p.onComplete, () => { p.onTerminate(); onTerminate(); }, p.onAfterTerminate, p.onRequest, p.onSubscribe, p.onCancel);
            }
            return new PublisherPeek<T>(source, v => { }, v => { }, e => { }, () => { }, onTerminate, () => { }, r => { }, s => { }, () => { });
        }

        internal static PublisherPeek<T> withOnAfterTerminate(IPublisher<T> source, Action onAfterTerminate)
        {
            PublisherPeek<T> p = source as PublisherPeek<T>;
            if (p != null)
            {
                return new PublisherPeek<T>(p.source, p.onNext, p.onAfterNext, p.onError, p.onComplete, p.onTerminate, () => { p.onAfterTerminate(); onAfterTerminate(); }, p.onRequest, p.onSubscribe, p.onCancel);
            }
            return new PublisherPeek<T>(source, v => { }, v => { }, e => { }, () => { }, () => { }, onAfterTerminate, r => { }, s => { }, () => { });
        }

        internal static PublisherPeek<T> withOnRequest(IPublisher<T> source, Action<long> onRequest)
        {
            PublisherPeek<T> p = source as PublisherPeek<T>;
            if (p != null)
            {
                return new PublisherPeek<T>(p.source, p.onNext, p.onAfterNext, p.onError, p.onComplete, p.onTerminate, p.onAfterTerminate, r => { p.onRequest(r); onRequest(r); }, p.onSubscribe, p.onCancel);
            }
            return new PublisherPeek<T>(source, v => { }, v => { }, e => { }, () => { }, () => { }, () => { }, onRequest, s => { }, () => { });
        }

        internal static PublisherPeek<T> withOnSubscribe(IPublisher<T> source, Action<ISubscription> onSubscribe)
        {
            PublisherPeek<T> p = source as PublisherPeek<T>;
            if (p != null)
            {
                return new PublisherPeek<T>(p.source, p.onNext, p.onAfterNext, p.onError, p.onComplete, p.onTerminate, p.onAfterTerminate, p.onRequest, s => { p.onSubscribe(s); onSubscribe(s); }, p.onCancel);
            }
            return new PublisherPeek<T>(source, v => { }, v => { }, e => { }, () => { }, () => { }, () => { }, r => { }, onSubscribe, () => { });
        }

        internal static PublisherPeek<T> withOnCancel(IPublisher<T> source, Action onCancel)
        {
            PublisherPeek<T> p = source as PublisherPeek<T>;
            if (p != null)
            {
                return new PublisherPeek<T>(p.source, p.onNext, p.onAfterNext, p.onError, p.onComplete, p.onTerminate, p.onAfterTerminate, p.onRequest, p.onSubscribe, () => { p.onCancel(); onCancel(); });
            }
            return new PublisherPeek<T>(source, v => { }, v => { }, e => { }, () => { }, () => { }, () => { }, r => { }, s => { }, onCancel);
        }

        sealed class PeekSubscriber : BasicFuseableSubscriber<T, T>
        {
            readonly Action<T> onNext;

            readonly Action<T> onAfterNext;

            readonly Action<Exception> onError;

            readonly Action onComplete;

            readonly Action onTerminate;

            readonly Action onAfterTerminate;

            readonly Action<long> onRequest;

            readonly Action<ISubscription> onSubscribe;

            readonly Action onCancel;

            internal PeekSubscriber(
                ISubscriber<T> actual,
                Action<T> onNext,
                Action<T> onAfterNext,
                Action<Exception> onError,
                Action onComplete,
                Action onTerminate,
                Action onAfterTerminate,
                Action<long> onRequest,
                Action<ISubscription> onSubscribe,
                Action onCancel
            ) : base(actual)
            {
                this.onNext = onNext;
                this.onAfterNext = onAfterNext;
                this.onError = onError;
                this.onComplete = onComplete;
                this.onTerminate = onTerminate;
                this.onAfterTerminate = onAfterTerminate;
                this.onRequest = onRequest;
                this.onSubscribe = onSubscribe;
                this.onCancel = onCancel;
            }

            protected override void OnSubscribe()
            {
                try
                {
                    onSubscribe(s);
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowOrDrop(ex);
                }
            }

            public override void OnComplete()
            {
                try
                {
                    onComplete();
                    onTerminate();
                } catch (Exception ex)
                {
                    ExceptionHelper.ThrowIfFatal(ex);
                    Fail(ex);
                    return;
                }

                Complete();

                try
                {
                    onAfterTerminate();
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowOrDrop(ex);
                }
            }

            public override void OnError(Exception e)
            {
                try
                {
                    onError(e);
                    onTerminate();
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowIfFatal(ex);
                    e = new AggregateException(e, ex);
                }

                try
                {
                    onAfterTerminate();
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowOrDrop(ex);
                }
            }

            public override void OnNext(T t)
            {
                if (done)
                {
                    return;
                }

                try
                {
                    onNext(t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return;
                }

                actual.OnNext(t);

                try { 
                    onAfterNext(t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return;
                }

            }

            public override int RequestFusion(int mode)
            {
                return TransitiveAnyFusion(mode);
            }

            public override bool Poll(out T value)
            {
                if (fusionMode == FuseableHelper.SYNC)
                {

                    T local;
                    if (qs.Poll(out local))
                    {
                        onNext(local);
                        value = local;
                        return true;
                    }
                    onComplete();
                    onTerminate();
                    onAfterTerminate();
                    value = default(T);
                    return false;
                }
                else
                {
                    return qs.Poll(out value);
                }
            }

            public override void Request(long n)
            {
                try
                {
                    onRequest(n);
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowOrDrop(ex);
                }
                s.Request(n);
            }

            public override void Cancel()
            {
                try { 
                    onCancel();
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowOrDrop(ex);
                }

                s.Cancel();
            }
        }

        sealed class PeekConditionalSubscriber : BasicFuseableConditionalSubscriber<T, T>
        {
            readonly Action<T> onNext;

            readonly Action<T> onAfterNext;

            readonly Action<Exception> onError;

            readonly Action onComplete;

            readonly Action onTerminate;

            readonly Action onAfterTerminate;

            readonly Action<long> onRequest;

            readonly Action<ISubscription> onSubscribe;

            readonly Action onCancel;

            internal PeekConditionalSubscriber(
                IConditionalSubscriber<T> actual,
                Action<T> onNext,
                Action<T> onAfterNext,
                Action<Exception> onError,
                Action onComplete,
                Action onTerminate,
                Action onAfterTerminate,
                Action<long> onRequest,
                Action<ISubscription> onSubscribe,
                Action onCancel
            ) : base(actual)
            {
                this.onNext = onNext;
                this.onAfterNext = onAfterNext;
                this.onError = onError;
                this.onComplete = onComplete;
                this.onTerminate = onTerminate;
                this.onAfterTerminate = onAfterTerminate;
                this.onRequest = onRequest;
                this.onSubscribe = onSubscribe;
                this.onCancel = onCancel;
            }

            protected override void OnSubscribe()
            {
                try
                {
                    onSubscribe(s);
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowOrDrop(ex);
                }
            }

            public override void OnComplete()
            {
                try
                {
                    onComplete();
                    onTerminate();
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowIfFatal(ex);
                    Fail(ex);
                    return;
                }

                Complete();

                try
                {
                    onAfterTerminate();
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowOrDrop(ex);
                }
            }

            public override void OnError(Exception e)
            {
                try
                {
                    onError(e);
                    onTerminate();
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowIfFatal(ex);
                    e = new AggregateException(e, ex);
                }

                try
                {
                    onAfterTerminate();
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowOrDrop(ex);
                }
            }

            public override void OnNext(T t)
            {
                if (done)
                {
                    return;
                }

                try
                {
                    onNext(t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return;
                }

                actual.OnNext(t);

                try
                {
                    onAfterNext(t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return;
                }

            }

            public override bool TryOnNext(T t)
            {
                if (done)
                {
                    return true;
                }

                try
                {
                    onNext(t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return true;
                }

                bool b = actual.TryOnNext(t);

                try
                {
                    onAfterNext(t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return true;
                }

                return b;
            }

            public override int RequestFusion(int mode)
            {
                return TransitiveAnyFusion(mode);
            }

            public override bool Poll(out T value)
            {
                if (fusionMode == FuseableHelper.SYNC)
                {

                    T local;
                    if (qs.Poll(out local))
                    {
                        onNext(local);
                        value = local;
                        return true;
                    }
                    onComplete();
                    onTerminate();
                    onAfterTerminate();
                    value = default(T);
                    return false;
                }
                else
                {
                    return qs.Poll(out value);
                }
            }

            public override void Request(long n)
            {
                try
                {
                    onRequest(n);
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowOrDrop(ex);
                }
                s.Request(n);
            }

            public override void Cancel()
            {
                try
                {
                    onCancel();
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowOrDrop(ex);
                }

                s.Cancel();
            }
        }
    }
}
