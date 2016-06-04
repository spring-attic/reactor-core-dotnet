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

namespace Reactor.Core.publisher
{
    sealed class PublisherDefer<T> : IFlux<T>, IMono<T>
    {
        readonly Func<IPublisher<T>> supplier;

        internal PublisherDefer(Func<IPublisher<T>> supplier)
        {
            this.supplier = supplier;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            IPublisher<T> p;

            try
            {
                p = supplier();
            }
            catch (Exception ex)
            {
                ExceptionHelper.ThrowIfFatal(ex);
                EmptySubscription<T>.Error(s, ex);
                return;
            }

            if (p == null)
            {
                EmptySubscription<T>.Error(s, new NullReferenceException("The supplier returned a null IPublisher"));
                return;
            }

            p.Subscribe(s);
        }
    }
}
