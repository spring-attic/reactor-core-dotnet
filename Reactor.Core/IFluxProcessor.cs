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
    /// An IFlux-typed <see cref="IProcessor{T}"/>.
    /// </summary>
    /// <typeparam name="T">The input and output value type.</typeparam>
    public interface IFluxProcessor<T> : IFluxProcessor<T, T>, IProcessor<T>
    {
    }

    /// <summary>
    /// An IFlux-typed <see cref="IProcessor{T, R}"/>.
    /// </summary>
    /// <typeparam name="T">The input value type.</typeparam>
    /// <typeparam name="R">The output value type</typeparam>
    public interface IFluxProcessor<T, R> : IFlux<R>, IProcessor<T, R>
    {
        /// <summary>
        /// Returns true if this IProcessor has subscribers.
        /// </summary>
        bool HasSubscribers { get; }

        /// <summary>
        /// Returns true if this IProcessor has completed normally.
        /// </summary>
        bool IsComplete { get; }

        /// <summary>
        /// Returns true if this IProcessor has failed.
        /// </summary>
        bool HasError { get; }

        /// <summary>
        /// Returns the failure Exception if <see cref="HasError"/>
        /// returns true, null otherwise.
        /// </summary>
        Exception Error { get; }
    }
}
