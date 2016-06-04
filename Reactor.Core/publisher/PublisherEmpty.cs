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

namespace Reactor.Core.publisher
{
    sealed class PublisherEmpty<T> : IFlux<T>, IMono<T>, IFuseable
    {
        internal static readonly PublisherEmpty<T> Instance = new PublisherEmpty<T>();

        private PublisherEmpty()
        {

        }

        public void Subscribe(ISubscriber<T> s)
        {
            EmptySubscription<T>.Complete(s);
        }

        
    }
}
