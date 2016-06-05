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
    sealed class PublisherAction<T>: IFlux<T>, IMono<T>, IFuseable
    {
        readonly Action action;

        public PublisherAction(Action action)
        {
            this.action = action;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                ExceptionHelper.ThrowIfFatal(ex);
                EmptySubscription<T>.Error(s, ex);
                return;
            }
            EmptySubscription<T>.Complete(s);
        }
    }
}
