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
    /// Represents an IFlux with a key.
    /// </summary>
    /// <typeparam name="K">The key type</typeparam>
    /// <typeparam name="V">The value type</typeparam>
    public interface IGroupedFlux<K, V> : IFlux<V>
    {
        /// <summary>
        /// The key associated with this group.
        /// </summary>
        K Key { get; }
    }
}
