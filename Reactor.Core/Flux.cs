using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Reactor.Core.publisher;
using Reactor.Core.subscriber;
using Reactor.Core.flow;

namespace Reactor.Core
{
    /// <summary>
    /// Extension methods for IFlux sources.
    /// </summary>
    public static class Flux
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
        public static IFlux<T> Just<T>(T value)
        {
            return new PublisherJust<T>(value);
        }

        /// <summary>
        /// Returns an empty instance which completes the ISubscribers immediately.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <returns>The shared Empty instance.</returns>
        public static IFlux<T> Empty<T>()
        {
            return PublisherEmpty<T>.Instance;
        }

        /// <summary>
        /// Returns an never instance which sets an empty ISubscription and
        /// does nothing further.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <returns>The shared Never instance.</returns>
        public static IFlux<T> Never<T>()
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
        public static IFlux<R> Map<T, R>(this IFlux<T> source, Func<T, R> mapper)
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

        /// <summary>
        /// Subscribes to the IPublisher and consumes only its OnNext signals.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="source">The source IPublisher</param>
        /// <param name="onNext">The callback for the OnNext signals</param>
        /// <returns>The IDisposable that allows cancelling the subscription.</returns>
        public static IDisposable Subscribe<T>(this IPublisher<T> source, Action<T> onNext)
        {
            var d = new CallbackSubscriber<T>(onNext, e => { ExceptionHelper.OnErrorDropped(e); }, () => { });
            source.Subscribe(d);
            return d;
        }

        /// <summary>
        /// Subscribes to the IPublisher and consumes only its OnNext signals.
        /// </summary>
        /// <remarks>
        /// If the <paramref name="onNext"/> callback crashes, the error is routed
        /// to <paramref name="onError"/> callback.
        /// If the <paramref name="onError"/> callback crashes, the error is routed to the
        /// global error hanlder in <see cref="ExceptionHelper.OnErrorDropped(Exception)"/>.
        /// </remarks>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="source">The source IPublisher</param>
        /// <param name="onNext">The callback for the OnNext signals</param>
        /// <param name="onError">The callback for the OnError signals</param>
        /// <returns>The IDisposable that allows cancelling the subscription.</returns>
        public static IDisposable Subscribe<T>(this IPublisher<T> source, Action<T> onNext, Action<Exception> onError)
        {
            var d = new CallbackSubscriber<T>(onNext, onError, () => { });
            source.Subscribe(d);
            return d;
        }

        /// <summary>
        /// Subscribes to the IPublisher and consumes only its OnNext signals.
        /// </summary>
        /// <remarks>
        /// If the <paramref name="onNext"/> callback crashes, the error is routed
        /// to <paramref name="onError"/> callback.
        /// If the <paramref name="onError"/> or <paramref name="onComplete"/> callbackcrashes, 
        /// the error is routed to the
        /// global error hanlder in <see cref="ExceptionHelper.OnErrorDropped(Exception)"/>.
        /// </remarks>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="source">The source IPublisher</param>
        /// <param name="onNext">The callback for the OnNext signals.</param>
        /// <param name="onError">The callback for the OnError signal.</param>
        /// <param name="onComplete">The callback for the OnComplete signal.</param>
        /// <returns>The IDisposable that allows cancelling the subscription.</returns>
        public static IDisposable Subscribe<T>(this IPublisher<T> source, Action<T> onNext, Action<Exception> onError, Action onComplete)
        {
            var d = new CallbackSubscriber<T>(onNext, onError, onComplete);
            source.Subscribe(d);
            return d;
        }

    }
}
