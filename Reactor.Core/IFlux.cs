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
    /// A Reactive Streams <see cref="Reactive.Streams.IPublisher{T}"/> with rx operators that emits 0 to N elements, and then completes
    /// (successfully or with an error).
    /// </summary>
    /// <remarks>
    /// <p>
    /// <img width = "640" src="https://raw.githubusercontent.com/reactor/projectreactor.io/master/src/main/static/assets/img/marble/flux.png" alt="" />
    /// </p>
    ///
    /// <p>It is intended to be used in implementations and return types.Input parameters should keep using raw
    /// <see cref="Reactive.Streams.IPublisher{T}">Publisher</see> as much as possible.</p>
    ///
    /// <p>If it is known that the underlying <see cref="Reactive.Streams.IPublisher{T}">Publisher</see>
    /// will emit 0 or 1 element, <see cref="Reactor.Core.Mono">Mono</see> should be used
    /// instead.</p>
    /// </remarks>

    public interface IFlux<out T> : IPublisher<T>
    {
    }
}
