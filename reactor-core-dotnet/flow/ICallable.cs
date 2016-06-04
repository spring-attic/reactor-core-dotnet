using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core;
using System.Threading;
using Reactor.Core.flow;

namespace Reactor.Core.flow
{
    /// <summary>
    /// Indicates an IPublisher holds a single value that can be computed or
    /// retrieved at subscription time.
    /// </summary>
    /// <typeparam name="T">The returned value type.</typeparam>
    public interface ICallable<out T>
    {
        /// <summary>
        /// Returns the value.
        /// </summary>
        T Value { get; }
    }

    /// <summary>
    /// Indicates an IPublisher holds a single, constant value that can
    /// be retrieved at assembly time.
    /// </summary>
    /// <typeparam name="T">The returned value type.</typeparam>
    public interface IScalarCallable<T> : ICallable<T>
    {

    }
}
