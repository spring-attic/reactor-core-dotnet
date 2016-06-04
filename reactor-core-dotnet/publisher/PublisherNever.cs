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
    sealed class PublisherNever<T> : IFlux<T>, IMono<T>, IFuseable
    {
        internal static readonly PublisherNever<T> Instance = new PublisherNever<T>();

        private PublisherNever()
        {

        }

        public void Subscribe(ISubscriber<T> s)
        {
            s.OnSubscribe(NeverSubscription<T>.Instance);
        }
    }
}
