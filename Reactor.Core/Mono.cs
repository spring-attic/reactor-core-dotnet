using Reactor.Core.flow;
using Reactor.Core.publisher;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactor.Core
{

    /// <summary>
    /// Extension methods for IMono sources.
    /// </summary>
    public static class Mono
    {
        // ---------------------------------------------------------------------------------------------------------
        // Enter the reactive world
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Create a new IPublisher that will only emit the passed data then onComplete.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="value">The unique data to emit</param>
        /// <returns>The new IPublisher instance</returns>
        public static IMono<T> Just<T>(T value)
        {
            return new PublisherJust<T>(value);
        }

        /// <summary>
        /// Returns an empty instance which completes the ISubscribers immediately.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <returns>The shared Empty instance.</returns>
        public static IMono<T> Empty<T>()
        {
            return PublisherEmpty<T>.Instance;
        }

        /// <summary>
        /// Returns an never instance which sets an empty ISubscription and
        /// does nothing further.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <returns>The shared Never instance.</returns>
        public static IMono<T> Never<T>()
        {
            return PublisherNever<T>.Instance;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Transform the reactive world
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Transform the items emitted by this IFlux by applying a function to each item.
        /// </summary>
        /// <typeparam name="T">The input value type</typeparam>
        /// <typeparam name="R">The output value type</typeparam>
        /// <param name="source">The source IFlux</param>
        /// <param name="mapper">The mapper from Ts to Rs</param>
        /// <returns>The new IFlux instance</returns>
        public static IMono<R> Map<T, R>(this IMono<T> source, Func<T, R> mapper)
        {
            if (source is IFuseable)
            {
                return new PublisherMapFuseable<T, R>(source, mapper);
            }
            return new PublisherMap<T, R>(source, mapper);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Leave the reactive world
        // ---------------------------------------------------------------------------------------------------------

    }
}
