using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core;
using System.Threading;
using Reactor.Core.flow;
using Reactor.Core.subscription;
using Reactor.Core.util;

namespace Reactor.Core.subscriber
{
    internal sealed class CallbackSubscriber<T> : ISubscriber<T>, IDisposable
    {
        readonly Action<T> onNext;

        readonly Action<Exception> onError;

        readonly Action onComplete;

        ISubscription s;

        bool done;

        internal CallbackSubscriber(Action<T> onNext, Action<Exception> onError, Action onComplete)
        {
            this.onNext = onNext;
            this.onError = onError;
            this.onComplete = onComplete;
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
            try
            {
                onNext(t);
            } catch (Exception ex)
            {
                ExceptionHelper.ThrowIfFatal(ex);
                s.Cancel();
                OnError(ex);
                return;
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

            try
            {
                onError(e);
            }
            catch (Exception ex)
            {
                ExceptionHelper.ThrowIfFatal(ex);
                ExceptionHelper.OnErrorDropped(new AggregateException(e, ex));
            }
        }

        public void OnComplete()
        {
            if (done)
            {
                return;
            }
            done = true;
            try
            {
                onComplete();
            }
            catch (Exception ex)
            {
                ExceptionHelper.ThrowIfFatal(ex);
                ExceptionHelper.OnErrorDropped(ex);
            }
        }

        public void Dispose()
        {
            SubscriptionHelper.Cancel(ref s);
        }
    }
}
