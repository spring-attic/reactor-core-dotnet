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
    /// Represents a connectable IFlux that starts streaming
    /// only when the <see cref="Connect(Action{IDisposable})"/> is called.
    /// </summary>
    /// <typeparam name="T">The value type</typeparam>
    public interface IConnectableFlux<T> : IFlux<T>
    {
        /// <summary>
        /// Connect to the upstream IFlux.
        /// </summary>
        /// <param name="onConnect">If given, it is called with a disposable instance that allows in-sequence connectioncancellation.</param>
        /// <returns>The IDisposable to cancel the connection</returns>
        IDisposable Connect(Action<IDisposable> onConnect = null);
    }
}
