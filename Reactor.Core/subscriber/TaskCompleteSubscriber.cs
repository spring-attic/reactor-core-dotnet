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
    sealed class TaskCompleteSubscriber<T> : ISubscriber<T>, IDisposable
    {
        readonly TaskCompletionSource<Void> tcs = new TaskCompletionSource<Void>();

        ISubscription s;

        public void OnComplete()
        {
            tcs.TrySetResult(null);
        }

        public void OnError(Exception e)
        {
            tcs.TrySetException(e);
        }

        public void OnNext(T t)
        {
            // values ignored
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


        internal Task Task()
        {
            return tcs.Task;
        }

        internal Task Task(CancellationToken token)
        {
            token.Register(this.Dispose);
            return tcs.Task;
        }

    }
}
