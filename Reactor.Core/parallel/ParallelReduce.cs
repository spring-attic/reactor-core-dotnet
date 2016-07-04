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
using Reactor.Core.publisher;

namespace Reactor.Core.parallel
{
    sealed class ParallelReduce<T, R> : ParallelUnorderedFlux<R>
    {
        readonly IParallelFlux<T> source;

        readonly Func<R> initialFactory;

        readonly Func<R, T, R> reducer;

        public override int Parallelism
        {
            get
            {
                return source.Parallelism;
            }
        }

        internal ParallelReduce(IParallelFlux<T> source, Func<R> initialFactory, Func<R, T, R> reducer)
        {
            this.source = source;
            this.initialFactory = initialFactory;
            this.reducer = reducer;
        }

        public override void Subscribe(ISubscriber<R>[] subscribers)
        {
            if (!this.Validate(subscribers))
            {
                return;
            }

            int n = subscribers.Length;
            var parents = new ISubscriber<T>[n];

            for (int i = 0; i < n; i++)
            {
                R accumulator;

                try
                {
                    accumulator = initialFactory();
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowIfFatal(ex);
                    foreach (var s in subscribers)
                    {
                        EmptySubscription<R>.Error(s, ex);
                    }
                    return;
                }

                parents[i] = new PublisherReduceWith<T, R>.ReduceWithSubscriber(subscribers[i], accumulator, reducer);
            }

            source.Subscribe(parents);
        }
    }
}
