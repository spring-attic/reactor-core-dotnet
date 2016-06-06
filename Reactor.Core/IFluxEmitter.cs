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
    /// API surface to signal 0 to N elements followed by an optional error or completion,
    /// hiding an actual ISubscriber.
    /// </summary>
    public interface IFluxEmitter<in T>
    {
        /// <summary>
        /// Signal the next value.
        /// </summary>
        /// <param name="t">The value.</param>
        void Next(T t);

        /// <summary>
        /// Signal an error. Disposes any associated resource.
        /// </summary>
        /// <param name="e"></param>
        void Error(Exception e);

        /// <summary>
        /// Signal a completion. Disposes any associated resource.
        /// </summary>
        void Complete();

        /// <summary>
        /// Associate a resource with the emitter that should
        /// be disposed on completion or cancellation
        /// </summary>
        /// <param name="d">The resource to associate.</param>
        void SetDisposable(IDisposable d);

        /// <summary>
        /// The current requested amount.
        /// </summary>
        long Requested { get; }
    }
}
