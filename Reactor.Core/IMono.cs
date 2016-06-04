using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core;
using System.Threading;
using Reactor.Core.flow;

namespace Reactor.Core
{
    /// <summary>
    /// A Reactive Streams <see cref="Reactive.Streams.IPublisher{T}">Publisher</see>
    /// with basic rx operators that completes successfully by emitting an element, or
    /// with an error.
    /// </summary>
    public interface IMono<out T> : IPublisher<T>
    {
    }
}
