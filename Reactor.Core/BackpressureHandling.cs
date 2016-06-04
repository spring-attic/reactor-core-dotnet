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

namespace Reactor.Core
{
    /// <summary>
    /// Handling of backpressure for FluxEmitter and IObservable conversions.
    /// </summary>
    public enum BackpressureHandling
    {
        /// <summary>
        /// Completely ignore backpressure.
        /// </summary>
        None,
        /// <summary>
        /// Signal an error if the downstream can't keep up.
        /// </summary>
        Error,
        /// <summary>
        /// Drop the overflown item.
        /// </summary>
        Drop,
        /// <summary>
        /// Keep only the latest item.
        /// </summary>
        Latest,
        /// <summary>
        /// Buffer all items.
        /// </summary>
        Buffer
    }
}
