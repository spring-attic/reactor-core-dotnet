using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Reactor.Core.publisher;
using Reactor.Core.subscriber;
using Reactor.Core.flow;
using Reactor.Core.scheduler;

namespace Reactor.Core
{
    /// <summary>
    /// Extension methods for IFlux sources.
    /// </summary>
    public static class Flux
    {
        /// <summary>
        /// The default buffer size and prefetch amount.
        /// </summary>
        public static int BufferSize { get { return 128; } }

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

        public static IFlux<R> CombineLatest<T, R>(Func<T[], R> combiner, params IPublisher<T>[] sources)
        {
            return CombineLatest(combiner, BufferSize, sources);
        }

        public static IFlux<R> CombineLatest<T, R>(Func<T[], R> combiner, int prefetch, params IPublisher<T>[] sources)
        {
            // TODO implement CombineLatest
            throw new NotImplementedException();
        }

        public static IFlux<R> CombineLatest<T1, T2, R>(IPublisher<T1> p1, IPublisher<T2> p2, Func<T1, T2, R> combiner)
        {
            // TODO implement CombineLatest
            throw new NotImplementedException();
        }

        public static IFlux<R> CombineLatest<T1, T2, T3, R>(
            IPublisher<T1> p1, IPublisher<T2> p2, 
            IPublisher<T3> p3,
            Func<T1, T2, T3, R> combiner)
        {
            // TODO implement CombineLatest
            throw new NotImplementedException();
        }

        public static IFlux<R> CombineLatest<T1, T2, T3, T4, R>(
            IPublisher<T1> p1, IPublisher<T2> p2,
            IPublisher<T3> p3, IPublisher<T4> p4,
            Func<T1, T2, T3, T4, R> combiner)
        {
            // TODO implement CombineLatest
            throw new NotImplementedException();
        }

        public static IFlux<R> CombineLatest<T1, T2, T3, T4, T5, R>(
            IPublisher<T1> p1, IPublisher<T2> p2,
            IPublisher<T3> p3, IPublisher<T4> p4,
            IPublisher<T5> p5,
            Func<T1, T2, T3, T4, T5, R> combiner)
        {
            // TODO implement CombineLatest
            throw new NotImplementedException();
        }

        public static IFlux<R> CombineLatest<T1, T2, T3, T4, T5, T6, R>(
            IPublisher<T1> p1, IPublisher<T2> p2,
            IPublisher<T3> p3, IPublisher<T4> p4,
            IPublisher<T5> p5, IPublisher<T6> p6,
            Func<T1, T2, T3, T4, T5, T6, R> combiner)
        {
            // TODO implement CombineLatest
            throw new NotImplementedException();
        }

        public static IFlux<R> CombineLatest<T, R>(IEnumerable<IPublisher<T>> sources, Func<T[], R> combiner)
        {
            return CombineLatest(sources, combiner, BufferSize);
        }

        public static IFlux<R> CombineLatest<T, R>(IEnumerable<IPublisher<T>> sources, Func<T[], R> combiner, int prefetch)
        {
            // TODO implement CombineLatest
            throw new NotImplementedException();
        }

        public static IFlux<T> Concat<T>(IEnumerable<IPublisher<T>> sources, bool delayError = false)
        {
            // TODO implement CombineLatest
            throw new NotImplementedException();
        }

        public static IFlux<T> Concat<T>(IEnumerable<IPublisher<T>> sources, int prefetch, bool delayError = false)
        {
            // TODO implement CombineLatest
            throw new NotImplementedException();
        }

        public static IFlux<T> Concat<T>(bool delayError = false, params IPublisher<T>[] sources)
        {
            return Concat(BufferSize, delayError, sources);
        }

        public static IFlux<T> Concat<T>(int prefetch, bool delayError = false, params IPublisher<T>[] sources)
        {
            // TODO implement CombineLatest
            throw new NotImplementedException();
        }

        public static IFlux<T> Create<T>(Action<IFluxEmitter<T>> emitter, BackpressureHandling backpressure = BackpressureHandling.Error)
        {
            // TODO implement Create
            throw new NotImplementedException();
        }

        /// <summary>
        /// Supply a IPublisher everytime subscribe is called on the returned flux. The passed supplier function
        /// will be invoked and it's up to the developer to choose to return a new instance of a IPublisher or reuse
        /// one effecitvely behaving like from(IPublisher).
        /// </summary>
        /// <typeparam name="T">the type of values passing through the IFlux</typeparam>
        /// <param name="supplier">the IPublisher supplier function to call on subscribe</param>
        /// <returns>a deferred IFlux</returns>
        public static IFlux<T> Defer<T>(Func<IPublisher<T>> supplier)
        {
            return new PublisherDefer<T>(supplier);
        }

        public static IFlux<T> Error<T>(Exception error, bool whenRequested = false)
        {
            // TODO implement Error
            throw new NotImplementedException();
        }

        public static IFlux<T> Error<T>(Func<Exception> errorSupplier, bool whenRequested = false)
        {
            // TODO implement Error
            throw new NotImplementedException();
        }

        public static IFlux<T> FirstEmitting<T>(params IPublisher<T>[] sources)
        {
            // TODO implement FirstEmitting
            throw new NotImplementedException();
        }

        public static IFlux<T> FirstEmitting<T>(IEnumerable<IPublisher<T>> sources)
        {
            // TODO implement FirstEmitting
            throw new NotImplementedException();
        }

        /// <summary>
        /// Expose the specified IPublisher with the IFlux API.
        /// </summary>
        /// <typeparam name="T">the source sequence type</typeparam>
        /// <param name="source">the source to decorate</param>
        /// <returns>a new IFlux</returns>
        public static IFlux<T> From<T>(IPublisher<T> source)
        {
            if (source is IFlux<T>)
            {
                return (IFlux<T>)source;
            }
            if (source is IFuseable)
            {
                return new PublisherWrapFuseable<T>(source);
            }
            return new PublisherWrap<T>(source);
        }

        public static IFlux<T> From<T>(params T[] values)
        {
            int n = values.Length;
            ;
            if (n == 0)
            {
                return Empty<T>();
            }
            else
            if (n == 1)
            {
                return Just(values[0]);
            }
            return new PublisherArray<T>(values);
        }

        public static IFlux<T> From<T>(IEnumerable<T> enumerable)
        {
            return new PublisherEnumerable<T>(enumerable);
        }

        public static IFlux<T> From<T>(IObservable<T> source, BackpressureHandling backpressure = BackpressureHandling.Error)
        {
            // TODO implement From
            throw new NotImplementedException();
        }

        public static IFlux<T> Generate<T>(Action<ISignalEmitter<T>> generator)
        {
            return Generate<T, object>(() => default(object), (s, e) => { generator(e); return s; }, s => { });
        }

        public static IFlux<T> Generate<T, S>(Func<S> stateSupplier, Func<S, ISignalEmitter<T>, S> generator)
        {
            return Generate(stateSupplier, generator, s => { });
        }

        public static IFlux<T> Generate<T, S>(Func<S> stateSupplier, Func<S, ISignalEmitter<T>, S> generator, Action<S> stateDisposer)
        {
            // TODO implement Generate
            throw new NotImplementedException();
        }

        public static IFlux<long> Interval(TimeSpan period)
        {
            return Interval(period, period, DefaultScheduler.Instance);
        }

        public static IFlux<long> Interval(TimeSpan period, TimedScheduler scheduler)
        {
            return Interval(period, period, scheduler);
        }

        public static IFlux<long> Interval(TimeSpan initialDelay, TimeSpan period)
        {
            return Interval(initialDelay, period, DefaultScheduler.Instance);
        }

        public static IFlux<long> Interval(TimeSpan initialDelay, TimeSpan period, TimedScheduler scheduler)
        {
            return new PublisherInterval(initialDelay, period, scheduler);
        }

        public static IFlux<T> Merge<T>(int maxConcurrency = int.MaxValue, bool delayErrors = false, params IPublisher<T>[] sources)
        {
            return Merge(BufferSize, maxConcurrency, delayErrors, sources);
        }

        public static IFlux<T> Merge<T>(int prefetch, int maxConcurrency = int.MaxValue, bool delayErrors = false, params IPublisher<T>[] sources)
        {
            // TODO implement Merge
            throw new NotImplementedException();
        }

        public static IFlux<T> Merge<T>(IEnumerable<IPublisher<T>> sources, int maxConcurrency = int.MaxValue, bool delayErrors = false)
        {
            return Merge(sources, BufferSize, maxConcurrency, delayErrors);
        }

        public static IFlux<T> Merge<T>(IEnumerable<IPublisher<T>> sources, int prefetch, int maxConcurrency = int.MaxValue, bool delayErrors = false)
        {
            // TODO implement Merge
            throw new NotImplementedException();
        }

        public static IFlux<T> Merge<T>(this IPublisher<IPublisher<T>> sources, int maxConcurrency = int.MaxValue, bool delayErrors = false)
        {
            return Merge(sources, BufferSize, maxConcurrency, delayErrors);
        }

        public static IFlux<T> Merge<T>(this IPublisher<IPublisher<T>> sources, int prefetch, int maxConcurrency = int.MaxValue, bool delayErrors = false)
        {
            // TODO implement Merge
            throw new NotImplementedException();
        }

        /// <summary>
        /// Signals a range of values from start to start + count (exclusive).
        /// </summary>
        /// <param name="start">The start value</param>
        /// <param name="count">The number of items, non-negative</param>
        /// <returns></returns>
        public static IFlux<int> Range(int start, int count)
        {
            if (count == 0)
            {
                return Empty<int>();
            }
            else
            if (count == 1)
            {
                return Just(start);
            }
            return new PublisherRange(start, count);
        }

        public static IFluxProcessor<IPublisher<T>, T> SwitchOnNext<T>()
        {
            return SwitchOnNext<T>(BufferSize);
        }

        public static IFluxProcessor<IPublisher<T>, T> SwitchOnNext<T>(int prefetch)
        {
            // TODO implement SwitchOnNext
            throw new NotImplementedException();
        }

        public static IFlux<T> SwitchOnNext<T>(this IPublisher<IPublisher<T>> sources)
        {
            return SwitchOnNext<T>(sources, BufferSize);
        }

        public static IFlux<T> SwitchOnNext<T>(IPublisher<IPublisher<T>> sources, int prefetch)
        {
            // TODO implement SwitchOnNext
            throw new NotImplementedException();
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
