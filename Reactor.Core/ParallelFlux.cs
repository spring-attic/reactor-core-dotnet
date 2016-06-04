using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactor.Core
{
    /// <summary>
    /// Base class for parallel publishers that take an array of Subscribers.
    /// and allow running sequences in parallel.
    /// </summary>
    /// <remarks>
    /// <p>Use {@code fork()} to start processing a regular Publisher in 'rails'.</p>
    /// <p>Use {@code runOn()} to introduce where each 'rail' shoud run on thread-vise.</p>
    /// <p>Use {@code join()} to merge the sources back into a single Publisher.</p>
    /// </remarks>
    public static class ParallelFlux
    {
    }
}
