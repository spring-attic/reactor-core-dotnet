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
    /// <summary>
    /// A constant scalar source.
    /// </summary>
    /// <remarks>
    /// It is based on IMono to facilitate easy reuse for Flux and Mono extension methods.
    /// </remarks>
    /// <typeparam name="T">The value type</typeparam>
    internal sealed class PublisherJust<T> : IFlux<T>, IMono<T>, IScalarCallable<T>, IFuseable
    {
        readonly T value;

        internal PublisherJust(T value)
        {
            this.value = value;
        }

        public T Value
        {
            get
            {
                return value;
            }
        }

        public void Subscribe(ISubscriber<T> s)
        {
            s.OnSubscribe(new ScalarSubscription<T>(s, value));
        }
    }
}
