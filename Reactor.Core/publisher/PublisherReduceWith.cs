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
    sealed class PublisherReduceWith<T, A> : IFlux<A>, IMono<A>
    {
        readonly IPublisher<T> source;

        readonly Func<A> initialSupplier;

        readonly Func<A, T, A> reducer;

        internal PublisherReduceWith(IPublisher<T> source, Func<A> initialSupplier, Func<A, T, A> reducer)
        {
            this.source = source;
            this.reducer = reducer;
            this.initialSupplier = initialSupplier;
        }

        public void Subscribe(ISubscriber<A> s)
        {
            A accumulator;

            try
            {
                accumulator = initialSupplier();
            }
            catch (Exception ex)
            {
                ExceptionHelper.ThrowIfFatal(ex);
                EmptySubscription<A>.Error(s, ex);
                return;
            }

            source.Subscribe(new ReduceWithSubscriber(s, accumulator, reducer));
        }

        sealed class ReduceWithSubscriber : DeferredScalarSubscriber<T, A>
        {
            readonly Func<A, T, A> reducer;

            public ReduceWithSubscriber(ISubscriber<A> actual, A accumulator, Func<A, T, A> reducer) : base(actual)
            {
                this.value = accumulator;
                this.reducer = reducer;
            }

            protected override void OnStart()
            {
                s.Request(long.MaxValue);
            }

            public override void OnComplete()
            {
                Complete(value);
            }

            public override void OnError(Exception e)
            {
                value = default(A);
                actual.OnError(e);
            }

            public override void OnNext(T t)
            {
                try
                {
                    value = reducer(value, t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                }
            }
        }
    }
}
