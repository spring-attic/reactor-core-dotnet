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
    /// Indicates when an error should be delivered in a ConcatMap scenario
    /// </summary>
    public enum ConcatErrorMode
    {
        /// <summary>
        /// Immediately.
        /// </summary>
        Immediate,
        /// <summary>
        /// When a source completes.
        /// </summary>
        Boundary,
        /// <summary>
        /// When all sources have completed.
        /// </summary>
        End
    }
}
