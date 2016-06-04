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
    /// Represents a conditional ISubscriber that has a TryOnNext() method
    /// to avoid requesting replenishments one-by-one
    /// </summary>
    /// <typeparam name="T">The value type</typeparam>
    public interface IConditionalSubscriber<T> : ISubscriber<T>
    {
        /// <summary>
        /// Try signalling a value and return true if successful,
        /// false to indicate a new value can be immediately sent out.
        /// </summary>
        /// <param name="t">The value signalled</param>
        /// <returns>True if the value has been consumed, false if a new value can be
        /// sent immediately</returns>
        bool TryOnNext(T t);
    }
}
