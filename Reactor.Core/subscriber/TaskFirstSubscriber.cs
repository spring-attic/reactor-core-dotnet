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

namespace Reactor.Core.subscriber
{
    sealed class TaskFirstSubscriber<T> : ISubscriber<T>, IDisposable
    {
        readonly TaskCompletionSource<T> tcs = new TaskCompletionSource<T>();

        ISubscription s;

        bool hasValue;

        public void OnComplete()
        {
            if (!hasValue)
            {
                tcs.TrySetException(new IndexOutOfRangeException("The upstream didn't produce any value."));
            }
        }

        public void OnError(Exception e)
        {
            if (hasValue)
            {
                ExceptionHelper.OnErrorDropped(e);
                return;
            }
            tcs.TrySetException(e);
        }

        public void OnNext(T t)
        {
            if (hasValue)
            {
                return;
            }
            hasValue = true;
            s.Cancel();
            tcs.TrySetResult(t);
        }

        public void OnSubscribe(ISubscription s)
        {
            if (SubscriptionHelper.SetOnce(ref this.s, s))
            {
                s.Request(long.MaxValue);
            }
        }

        public void Dispose()
        {
            SubscriptionHelper.Cancel(ref s);
        }


        internal Task<T> Task()
        {
            return tcs.Task;
        }

        internal Task<T> Task(CancellationToken token)
        {
            token.Register(this.Dispose);
            return tcs.Task;
        }

    }
}
