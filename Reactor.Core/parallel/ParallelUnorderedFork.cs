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

namespace Reactor.Core.parallel
{
    internal sealed class ParallelUnorderedFork<T> : ParallelUnorderedFlux<T>
    {
        readonly IPublisher<T> source;

        readonly int parallelism;


        internal ParallelUnorderedFork(IPublisher<T> source, int parallelism)
        {
            this.source = source;
            this.parallelism = parallelism;
        }

        public override int Parallelism
        {
            get
            {
                return parallelism;
            }
        }

        public override void Subscribe(ISubscriber<T>[] subscribers)
        {
            // TODO implement 
            throw new NotImplementedException();
        }
    }
}
