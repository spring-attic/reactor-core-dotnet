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

namespace Reactor.Core.Test.tck
{
    /*
    class ConcatTest : FluxPublisherVerification<int>
    {
        public override IPublisher<int> CreatePublisher(long elements)
            => Flux.From(Enumerate(elements/2)).ConcatWith(Flux.From(Enumerate((elements + 1)/2)));
    }
    */
}
