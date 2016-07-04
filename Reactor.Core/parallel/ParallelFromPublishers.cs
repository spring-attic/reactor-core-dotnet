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

namespace Reactor.Core.parallel
{
    sealed class ParallelFromPublishers<T> : ParallelUnorderedFlux<T>
    {
        readonly IPublisher<T>[] sources;

        internal ParallelFromPublishers(IPublisher<T>[] sources)
        {
            this.sources = sources;
        }

        public override int Parallelism
        {
            get
            {
                return sources.Length;
            }
        }

        public override void Subscribe(ISubscriber<T>[] subscribers)
        {
            if (!this.Validate(subscribers))
            {
                return;
            }
            int n = subscribers.Length;
            for (int i = 0; i < n; i++)
            {
                sources[i].Subscribe(subscribers[i]);
            } 
        }
    }
}
