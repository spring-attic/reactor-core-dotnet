using Reactive.Streams;
using Reactor.Core.flow;
using Reactor.Core.publisher;
using Reactor.Core.scheduler;
using Reactor.Core.subscriber;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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

        /// <summary>
        /// Creates a deferred emitter that can be used with callback-based
        /// APIs to signal at most one value, a complete or an error signal.
        /// </summary>
        /// <typeparam name="T">The type of the value emitted</typeparam>
        /// <param name="callback">the consumer who will receive a per-subscriber MonoEmitter.</param>
        /// <returns>A new IMono instance.</returns>
        public static IMono<T> Create<T>(Action<IMonoEmitter<T>> callback)
        {
            // TODO implement Create
            throw new NotImplementedException();
        }

        /// <summary>
        /// Create an IMono provider that will supply a 
        /// target IMono to subscribe to for each ISubscriber downstream.
        /// </summary>
        /// <typeparam name="T">the element type of the returned Mono instance</typeparam>
        /// <param name="supplier">An IMono factory</param>
        /// <returns>A new IMono instance.</returns>
        public static IMono<T> Defer<T>(Func<IMono<T>> supplier)
        {
            return new PublisherDefer<T>(supplier);
        }

        /// <summary>
        /// Create a Mono which delays an onNext signal by the delay amount and complete
        /// on the default timed scheduler. If the demand cannot be produced in time, 
        /// an onError will be signalled instead.
        /// </summary>
        /// <param name="delay">The delay amount</param>
        /// <returns>A new IMono instance.</returns>
        public static IMono<long> Delay(TimeSpan delay)
        {
            return Delay(delay, DefaultScheduler.Instance);
        }

        /// <summary>
        /// Create a Mono which delays an onNext signal of {@code duration} milliseconds 
        /// and complete. f the demand cannot be produced in time, an onError will 
        /// be signalled instead.
        /// </summary>
        /// <param name="delay">The delay amount</param>
        /// <param name="scheduler">The timed scheduler to use for the wait.</param>
        /// <returns>A new IMono instance.</returns>
        public static IMono<long> Delay(TimeSpan delay, TimedScheduler scheduler)
        {
            // TODO implement Delay
            throw new NotImplementedException();
        }

        /// <summary>
        /// Create a new {@link Mono} that ignores onNext (dropping them) and only 
        /// react on Completion signal.
        /// </summary>
        /// <typeparam name="T">The element type of the source</typeparam>
        /// <param name="source">The source to ignore elements of.</param>
        /// <returns>A new IMono instance.</returns>
        public static IMono<Void> Empty<T>(IPublisher<T> source)
        {
            return new PublisherIgnoreElements<T, Void>(source);
        }

        /// <summary>
        /// Creates an IMono instance which signals the given exception
        /// immediately (or when requested).
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="ex">The exception to signal</param>
        /// <param name="whenRequested">Signal the exception when requested?</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> Error<T>(Exception ex, bool whenRequested = false)
        {
            return new PublisherError<T>(ex, whenRequested);
        }

        /// <summary>
        /// Pick the first result coming from any of the given monos and populate 
        /// a new IMono.
        /// </summary>
        /// <typeparam name="T">The common element type of the monos.</typeparam>
        /// <param name="sources">The deferred monos to use</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> First<T>(params IMono<T>[] sources)
        {
            // TODO implement First
            throw new NotImplementedException();
        }

        /// <summary>
        /// Pick the first result coming from any of the given monos and populate 
        /// a new IMono.
        /// </summary>
        /// <typeparam name="T">The common element type of the monos.</typeparam>
        /// <param name="sources">The deferred monos to use</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> First<T>(IEnumerable<IMono<T>> sources)
        {
            // TODO implement First
            throw new NotImplementedException();
        }

        /// <summary>
        /// Expose the specified {@link Publisher} with the {@link Mono} API, 
        /// and ensure it will emit 0 or 1 item.
        /// </summary>
        /// <typeparam name="T">the source type</typeparam>
        /// <param name="source">The IPublisher source to convert to IMono.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> From<T>(IPublisher<T> source)
        {
            if (source is IMono<T>)
            {
                return (IMono<T>)source;
            }
            return new PublisherSingle<T>(source, true, false, default(T));
        }

        /// <summary>
        /// Create a IMono producing the value for the IMono using the given supplier.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="generator">The function to generate a value.</param>
        /// <param name="nullMeansEmpty">If true and T is a class, returning null
        /// from the function is interpreted as an empty source.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> From<T>(Func<T> generator, bool nullMeansEmpty = false)
        {
            return new PublisherFunc<T>(generator, nullMeansEmpty);
        }

        /// <summary>
        /// Creates a valueless IMono instance from the task which
        /// signals when the task completes or fails.
        /// </summary>
        /// <param name="task">The tast to use as source.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<Void> From(Task task)
        {
            return new PublisherFromTask(task);
        }

        /// <summary>
        /// Creates a IMono instance from the task which
        /// signals a single value when the task completes or 
        /// signals an Exception if the task fails.
        /// </summary>
        /// <param name="task">The tast to use as source.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> From<T>(Task<T> task)
        {
            return new PublisherFromTask<T>(task);
        }

        /// <summary>
        /// Executes the given action for each subscriber and completes
        /// or signals an Exception if the action threw.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="action">The action</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> From<T>(Action action)
        {
            return new PublisherAction<T>(action);
        }

        /// <summary>
        /// Create a new {@link Mono} that ignores onNext (dropping them) and 
        /// only react on Completion signal.
        /// </summary>
        /// <typeparam name="T">The value type of the source.</typeparam>
        /// <param name="source">The source t ignore values of.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> IgnoreElements<T>(IPublisher<T> source)
        {
            return new PublisherIgnoreElements<T, T>(source);
        }

        /// <summary>
        /// Uses a resource, generated by a supplier for each individual Subscriber, while
        /// streaming the value from a Mono derived from the same resource and makes sure 
        /// the resource is released if the sequence terminates or the Subscriber cancels.
        /// </summary>
        /// <typeparam name="T">The main value type</typeparam>
        /// <typeparam name="R">The resource type.</typeparam>
        /// <param name="resourceFactory">Called to return a per-subscriber resource.</param>
        /// <param name="monoFactory">Called with the resource from the resourceFactory
        /// to generate an IMono instance.</param>
        /// <param name="resourceDisposer">Called when the generated IMono
        /// source terminates or the whole sequence gets cancelled.</param>
        /// <param name="eager">If true, the resourceDisposer is called before
        /// signalling the terminal event.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> Using<T, R>(Func<R> resourceFactory, Func<R, IMono<T>> monoFactory, Action<R> resourceDisposer, bool eager = true)
        {
            return new PublisherUsing<T, R>(resourceFactory, monoFactory, resourceDisposer, eager);
        }

        /// <summary>
        ///  Merge given monos into a new a IMono that will be fulfilled 
        ///  when all of the given IMonos have been fulfilled. If delayErrors is false
        ///  any error will cause pending results to be cancelled and immediate error 
        ///  emission  to the returned IMono.
        /// </summary>
        /// <typeparam name="T1">type of the value from m1</typeparam>
        /// <typeparam name="T2">type of the value from m2</typeparam>
        /// <param name="m1">The first upstream IMono to subscribe to.</param>
        /// <param name="m2">The second upstream IMono to subscribe to.</param>
        /// <param name="delayErrors">If true, errors will be delayed till all sources terminate.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<Tuple<T1, T2>> When<T1, T2>(
            IMono<T1> m1, IMono<T2> m2, 
            bool delayErrors = false)
        {
            // TODO implement When
            throw new NotImplementedException();
        }

        /// <summary>
        ///  Merge given monos into a new a IMono that will be fulfilled 
        ///  when all of the given IMonos have been fulfilled. If delayErrors is false
        ///  any error will cause pending results to be cancelled and immediate error 
        ///  emission  to the returned IMono.
        /// </summary>
        /// <typeparam name="T1">type of the value from m1</typeparam>
        /// <typeparam name="T2">type of the value from m2</typeparam>
        /// <typeparam name="T3">type of the value from m3</typeparam>
        /// <param name="m1">The first upstream IMono to subscribe to.</param>
        /// <param name="m2">The second upstream IMono to subscribe to.</param>
        /// <param name="m3">The third upstream IMono to subscribe to.</param>
        /// <param name="delayErrors">If true, errors will be delayed till all sources terminate.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<Tuple<T1, T2, T3>> When<T1, T2, T3>(
            IMono<T1> m1, IMono<T2> m2, 
            IMono<T3> m3,
            bool delayErrors = false)
        {
            // TODO implement When
            throw new NotImplementedException();
        }

        /// <summary>
        ///  Merge given monos into a new a IMono that will be fulfilled 
        ///  when all of the given IMonos have been fulfilled. If delayErrors is false
        ///  any error will cause pending results to be cancelled and immediate error 
        ///  emission  to the returned IMono.
        /// </summary>
        /// <typeparam name="T1">type of the value from m1</typeparam>
        /// <typeparam name="T2">type of the value from m2</typeparam>
        /// <typeparam name="T3">type of the value from m3</typeparam>
        /// <typeparam name="T4">type of the value from m4</typeparam>
        /// <param name="m1">The first upstream IMono to subscribe to.</param>
        /// <param name="m2">The second upstream IMono to subscribe to.</param>
        /// <param name="m3">The third upstream IMono to subscribe to.</param>
        /// <param name="m4">The fourth upstream IMono to subscribe to.</param>
        /// <param name="delayErrors">If true, errors will be delayed till all sources terminate.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<Tuple<T1, T2, T3, T4>> When<T1, T2, T3, T4>(
            IMono<T1> m1, IMono<T2> m2,
            IMono<T3> m3, IMono<T4> m4,
            bool delayErrors = false)
        {
            // TODO implement When
            throw new NotImplementedException();
        }

        /// <summary>
        ///  Merge given monos into a new a IMono that will be fulfilled 
        ///  when all of the given IMonos have been fulfilled. If delayErrors is false
        ///  any error will cause pending results to be cancelled and immediate error 
        ///  emission  to the returned IMono.
        /// </summary>
        /// <typeparam name="T1">type of the value from m1</typeparam>
        /// <typeparam name="T2">type of the value from m2</typeparam>
        /// <typeparam name="T3">type of the value from m3</typeparam>
        /// <typeparam name="T4">type of the value from m4</typeparam>
        /// <typeparam name="T5">type of the value from m5</typeparam>
        /// <param name="m1">The first upstream IMono to subscribe to.</param>
        /// <param name="m2">The second upstream IMono to subscribe to.</param>
        /// <param name="m3">The third upstream IMono to subscribe to.</param>
        /// <param name="m4">The fourth upstream IMono to subscribe to.</param>
        /// <param name="m5">The fifth upstream IMono to subscribe to.</param>
        /// <param name="delayErrors">If true, errors will be delayed till all sources terminate.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<Tuple<T1, T2, T3, T4, T5>> When<T1, T2, T3, T4, T5>(
            IMono<T1> m1, IMono<T2> m2,
            IMono<T3> m3, IMono<T4> m4,
            IMono<T5> m5,
            bool delayErrors = false)
        {
            // TODO implement When
            throw new NotImplementedException();
        }

        /// <summary>
        ///  Merge given monos into a new a IMono that will be fulfilled 
        ///  when all of the given IMonos have been fulfilled. If delayErrors is false
        ///  any error will cause pending results to be cancelled and immediate error 
        ///  emission  to the returned IMono.
        /// </summary>
        /// <typeparam name="T1">type of the value from m1</typeparam>
        /// <typeparam name="T2">type of the value from m2</typeparam>
        /// <typeparam name="T3">type of the value from m3</typeparam>
        /// <typeparam name="T4">type of the value from m4</typeparam>
        /// <typeparam name="T5">type of the value from m5</typeparam>
        /// <typeparam name="T6">type of the value from m6</typeparam>
        /// <param name="m1">The first upstream IMono to subscribe to.</param>
        /// <param name="m2">The second upstream IMono to subscribe to.</param>
        /// <param name="m3">The third upstream IMono to subscribe to.</param>
        /// <param name="m4">The fourth upstream IMono to subscribe to.</param>
        /// <param name="m5">The fifth upstream IMono to subscribe to.</param>
        /// <param name="m6">The sixth upstream IMono to subscribe to.</param>
        /// <param name="delayErrors">If true, errors will be delayed till all sources terminate.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<Tuple<T1, T2, T3, T4, T5, T6>> When<T1, T2, T3, T4, T5, T6>(
            IMono<T1> m1, IMono<T2> m2,
            IMono<T3> m3, IMono<T4> m4,
            IMono<T5> m5, IMono<T6> m6,
            bool delayErrors = false)
        {
            // TODO implement When
            throw new NotImplementedException();
        }

        /// <summary>
        ///  Merge given monos into a new a IMono that will be fulfilled 
        ///  when all of the given IMonos have been fulfilled. If delayErrors is false
        ///  any error will cause pending results to be cancelled and immediate error 
        ///  emission  to the returned IMono.
        /// </summary>
        /// <typeparam name="T">The common value type of the source IMonos</typeparam>
        /// <param name="delayErrors">If true, errors will be delayed till all sources terminate.</param>
        /// <param name="sources">The parameters array IMono sources.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T[]> When<T>(bool delayErrors = false, params IMono<T>[] sources)
        {
            // TODO implement When
            throw new NotImplementedException();
        }

        /// <summary>
        ///  Merge given monos into a new a IMono that will be fulfilled 
        ///  when all of the given IMonos have been fulfilled. If delayErrors is false
        ///  any error will cause pending results to be cancelled and immediate error 
        ///  emission  to the returned IMono.
        /// </summary>
        /// <typeparam name="T">The common value type of the source IMonos</typeparam>
        /// <param name="delayErrors">If true, errors will be delayed till all sources terminate.</param>
        /// <param name="sources">The IEnumerable sequence of IMono sources.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T[]> When<T>(IEnumerable<IMono<T>> sources, bool delayErrors = false)
        {
            // TODO implement When
            throw new NotImplementedException();
        }

        /// <summary>
        /// Aggregate given monos into a new a IMono that will be fulfilled 
        /// when all of the given IMono have been fulfilled. If any Mono 
        /// terminates without value, the returned sequence will be terminated
        /// immediately and pending results cancelled.
        /// </summary>
        /// <typeparam name="T">The super incoming type</typeparam>
        /// <typeparam name="R">The type of the function result.</typeparam>
        /// <param name="zipper">The function that receives a row of elements from
        /// all source Monos.</param>
        /// <param name="sources">The parameters array of IMono sources</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<R> Zip<T, R>(Func<T[], R> zipper, params IMono<T>[] sources)
        {
            // TODO implement Zip
            throw new NotImplementedException();
        }

        /// <summary>
        /// Aggregate given monos into a new a IMono that will be fulfilled 
        /// when all of the given IMono have been fulfilled. If any Mono 
        /// terminates without value, the returned sequence will be terminated
        /// immediately and pending results cancelled.
        /// </summary>
        /// <typeparam name="T">The super incoming type</typeparam>
        /// <typeparam name="R">The type of the function result.</typeparam>
        /// <param name="zipper">The function that receives a row of elements from
        /// all source Monos.</param>
        /// <param name="sources">The IEnumerable sequence of IMono sources</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<R> Zip<T, R>(IEnumerable<IMono<T>> sources, Func<T[], R> zipper)
        {
            // TODO implement Zip
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
        /// <returns>The new IMono instance.</returns>
        public static IMono<R> Map<T, R>(this IMono<T> source, Func<T, R> mapper)
        {
            return new PublisherMap<T, R>(source, mapper);
        }

        /// <summary>
        /// Transform this IMono into a target type.
        /// </summary>
        /// <typeparam name="T">The source value type</typeparam>
        /// <typeparam name="R">The result type</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="transformer">The function called with the source IMono
        /// and returns a value.</param>
        /// <returns>The new IMono instance.</returns>
        public static R As<T, R>(this IMono<T> source, Func<IMono<T>, R> transformer)
        {
            return transformer(source);
        }

        /// <summary>
        /// Combine the result from this mono and another into a Tuple.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="U">The other value type.</typeparam>
        /// <param name="source">The main source IMono instance.</param>
        /// <param name="other">The other source IMono instance.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<Tuple<T, U>> And<T, U>(this IMono<T> source, IMono<U> other)
        {
            return When(source, other);
        }

        /// <summary>
        /// Cast the current IMono produced type into a target produced type.
        /// </summary>
        /// <typeparam name="T">The source IMono value type.</typeparam>
        /// <typeparam name="R">The result IMono value type.</typeparam>
        /// <param name="source">The source IMono instance to cast elements of.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<R> Cast<T, R>(this IMono<T> source) where T: class where R : class
        {
            return Map(source, v =>
            {
                R r = v as R;
                if (v != null && r == null)
                {
                    throw new InvalidCastException();
                }
                return r;
            });
        }

        /// <summary>
        /// Turn this IMono into a hot source and cache last emitted
        /// signals for further ISubscriber.
        /// Completion and Error will also be replayed.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance to cache its signal.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> Cache<T>(this IMono<T> source)
        {
            // TODO implement Cache
            throw new NotImplementedException();
        }

        /// <summary>
        /// Defer the given transformation to this IMono in order to generate a
        /// target IMono type. A transformation will occur for each ISubscriber.
        /// </summary>
        /// <typeparam name="T">The source value type</typeparam>
        /// <typeparam name="R">The output value type</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="composer">The function that receives the source IMono
        /// and returns another IMono instance for each individual ISubscriber.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<R> Compose<T, R>(this IMono<T> source, Func<IMono<T>, IMono<R>> composer)
        {
            return Defer(() => composer(source));
        }

        /// <summary>
        /// Concatenate emissions of this IMono with the provided IPublisher
        /// (no interleave).
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="source">The beginning IMono instance</param>
        /// <param name="other">The ending IMono instance</param>
        /// <param name="delayErrors">If true, errors from the first source
        /// instance is delayed until the second IMono terminates.</param>
        /// <returns>The new IFlux instance.</returns>
        public static IFlux<T> ConcatWith<T>(this IMono<T> source, IPublisher<T> other, bool delayErrors = false)
        {
            return Flux.Concat(delayErrors, source, other);
        }

        /// <summary>
        /// Provide a default unique value if this mono is completed without any data.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="defaultValue">the alternate value if the source is empty</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> DefaultIfEmpty<T>(this IMono<T> source, T defaultValue)
        {
            // TODO implement DefaultIfEmpty
            throw new NotImplementedException();
        }

        /// <summary>
        /// Delay the subscription to this IMono source until the given period elapses.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="delay">The delay amount before subscribing to the source IMono.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> DelaySubscription<T>(this IMono<T> source, TimeSpan delay)
        {
            return DelaySubscription(source, delay, DefaultScheduler.Instance);
        }

        /// <summary>
        /// Delay the subscription to this IMono source until the given period elapses.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="delay">The delay amount before subscribing to the source IMono.</param>
        /// <param name="scheduler">The timed scheduler to use for waiting.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> DelaySubscription<T>(this IMono<T> source, TimeSpan delay, TimedScheduler scheduler)
        {
            // TODO implement DelaySubscription
            throw new NotImplementedException();
        }

        /// <summary>
        /// Delay the subscription to this {@link Mono} until another {@link Publisher}
        /// signals a value or completes.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="U">The other value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="other">The other IPublisher instance that when signals
        /// or completes, triggers the actual subscription to the source IMono.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> DelaySubscription<T, U>(this IMono<T> source, IPublisher<U> other)
        {
            // TODO implement DelaySubscription
            throw new NotImplementedException();
        }

        /// <summary>
        /// Calls the appropriate OnXXX method on the ISubscriber based on the
        /// source ISignal's contents.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> Dematerialize<T>(this IMono<ISignal<T>> source)
        {
            // TODO implement Dematerialize
            throw new NotImplementedException();
        }

        /// <summary>
        /// Call the specified action after the source has signalled an OnError or OnComplete.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono.</param>
        /// <param name="onAfterTerminate">The action to call.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> DoAfterTerminate<T>(this IMono<T> source, Action onAfterTerminate)
        {
            return PublisherPeek<T>.withOnAfterTerminate(source, onAfterTerminate);
        }

        /// <summary>
        /// Call the specified action with the current value after the source has signalled an OnNext.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono.</param>
        /// <param name="onAfterNext">The action to call.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> DoAfterNext<T>(this IMono<T> source, Action<T> onAfterNext)
        {
            return PublisherPeek<T>.withOnAfterNext(source, onAfterNext);
        }

        /// <summary>
        /// Call the specified action if the sequence gets cancelled.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono.</param>
        /// <param name="onCancel">The action to call.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> DoOnCancel<T>(this IMono<T> source, Action onCancel)
        {
            return PublisherPeek<T>.withOnCancel(source, onCancel);
        }

        /// <summary>
        /// Call the specified action before the source signals an OnComplete.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono.</param>
        /// <param name="onComplete">The action to call.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> DoOnComplete<T>(this IMono<T> source, Action onComplete)
        {
            return PublisherPeek<T>.withOnComplete(source, onComplete);
        }

        /// <summary>
        /// Call the specified action before the source signals an OnError.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono.</param>
        /// <param name="onError">The action to call.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> DoOnError<T>(this IMono<T> source, Action<Exception> onError)
        {
            return PublisherPeek<T>.withOnError(source, onError);
        }

        /// <summary>
        /// Call the specified action before the source signals OnError with the given Exception type (or its subtypes).
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="E">The exception type.</typeparam>
        /// <param name="source">The source IMono.</param>
        /// <param name="onError">The action to call.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> DoOnError<T, E>(this IMono<T> source, Action<E> onError) where E : Exception
        {
            return DoOnError(source, e =>
            {
                if (e is E)
                {
                    onError(e as E);
                }
            });
        }

        /// <summary>
        /// Call the specified action before the source signals an OnError whose Exception matches the predicate.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono.</param>
        /// <param name="predicate">The predicate that receives the Exception and if returns true, the onError action is called.</param>
        /// <param name="onError">The action to call.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> DoOnError<T>(this IMono<T> source, Func<Exception, bool> predicate, Action<Exception> onError)
        {
            return DoOnError(source, e =>
            {
                if (predicate(e))
                {
                    onError(e);
                }
            });
        }

        /// <summary>
        /// Calls the specified action with the current value before the sequence signals an OnNext.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="source">The source IMono</param>
        /// <param name="onNext">The action to call with the current value.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> DoOnNext<T>(this IMono<T> source, Action<T> onNext)
        {
            return PublisherPeek<T>.withOnNext(source, onNext);
        }

        /// <summary>
        /// Calls the specified action with the current request amount before it reaches the upstream.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="source">The source IMono</param>
        /// <param name="onRequest">The action to call.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> DoOnRequest<T>(this IMono<T> source, Action<long> onRequest)
        {
            return PublisherPeek<T>.withOnRequest(source, onRequest);
        }

        /// <summary>
        /// Calls the specified action with the incoming ISubscription before it reaches the downstream.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono.</param>
        /// <param name="onSubscribe">The action to call.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> DoOnSubscribe<T>(this IMono<T> source, Action<ISubscription> onSubscribe)
        {
            return PublisherPeek<T>.withOnSubscribe(source, onSubscribe);
        }

        /// <summary>
        /// Calls the specified action befoer the source signals an OnError or OnComplete.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono.</param>
        /// <param name="onTerminate">The action to call.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> DoOnTerminate<T>(this IMono<T> source, Action onTerminate)
        {
            return PublisherPeek<T>.withOnTerminate(source, onTerminate);
        }

        /// <summary>
        /// Wraps each source element into a Timed structure which holds the time difference between
        /// subsequent element according to the default timed scheduler. The first structure holds
        /// the time between the subscription and the emission of the first element.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<Timed<T>> Elapsed<T>(this IMono<T> source)
        {
            return Elapsed(source, DefaultScheduler.Instance);
        }

        /// <summary>
        /// Wraps each source element into a Timed structure which holds the time difference between
        /// subsequent element according to the given timed scheduler. The first structure holds
        /// the time between the subscription and the emission of the first element.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono.</param>
        /// <param name="scheduler">The scheduler supplying the notion of current time.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<Timed<T>> Elapsed<T>(this IMono<T> source, TimedScheduler scheduler)
        {
            // TODO implement Elapsed
            throw new NotImplementedException();
        }

        /// <summary>
        /// Filters out elements that don't match the predicate.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono</param>
        /// <param name="predicate">The predicate function called for each source element and returns true if that element
        /// may pass.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> Filter<T>(this IMono<T> source, Func<T, bool> predicate)
        {
            return new PublisherFilter<T>(source, predicate);
        }

        /// <summary>
        /// Transform the items emitted by the IMono into an IPublisher, then 
        /// flattens the emissions from this IPublisher by merging them into a single 
        /// IFlux.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">the merged sequence type</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="mapper">The function that receives the Mono's single
        /// value (if any) and returns an IPublisher whose value will be relayed
        /// then on.</param>
        /// <returns>The new IFlux instance.</returns>
        public static IFlux<R> FlatMap<T, R>(this IMono<T> source, Func<T, IPublisher<R>> mapper)
        {
            // TODO implement custom FlatMap
            return new PublisherConcatMap<T, R>(source, mapper, 1, ConcatErrorMode.End);
        }

        /// <summary>
        /// Transform the items emitted by the IMono into an IMono, then 
        /// signals the emission of this IMono.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">the merged sequence type</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="mapper">The function that receives the Mono's single
        /// value (if any) and returns an IMono whose signal will be relayed
        /// then on.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<R> FlatMap<T, R>(this IMono<T> source, Func<T, IMono<R>> mapper)
        {
            // TODO implement custom FlatMap
            return From(new PublisherConcatMap<T, R>(source, mapper, 1, ConcatErrorMode.End));
        }

        /// <summary>
        /// Transform the items emitted by the IMono into an IEnumerable, then 
        /// signals the values of this IEnumerable through the resulting IFlux.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">the merged sequence type</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="mapper">The function that receives the Mono's single
        /// value (if any) and returns an IEnumerable whose value will be relayed
        /// then on.</param>
        /// <returns>The new IFlux instance.</returns>
        public static IFlux<R> FlatMap<T, R>(this IMono<T> source, Func<T, IEnumerable<R>> mapper)
        {
            // TODO implement custom FlatMap
            return new PublisherFlattenEnumerable<T, R>(source, mapper, 1);
        }

        /// <summary>
        /// Based on the signal of the source IMono, continues relaying
        /// the values of an IPublisher returned by the appropriate signal
        /// mapper function.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IMono sequence.</param>
        /// <param name="onNextMapper">Function called with the value of the source
        /// IMono and returns an IPublisher to be signal elements from then on.</param>
        /// <param name="onErrorMapper">Function called with the Exception of the source
        /// IMono and returns an IPublisher to be signal elements from then on.</param>
        /// <param name="onCompleteMapper">Function called when the source
        /// IMono completes and returns an IPublisher to be signal elements 
        /// from then on.</param>
        /// <returns>The new IFlux instance.</returns>
        public static IFlux<R> FlatMap<T, R>(this IMono<T> source, 
            Func<T, IPublisher<R>> onNextMapper, Func<Exception, IPublisher<R>> onErrorMapper,
            Func<IPublisher<R>> onCompleteMapper)
        {
            return new PublisherMapNotification<T, R>(source, onNextMapper, onErrorMapper, onCompleteMapper).FlatMap(v => v);
        }

        /// <summary>
        /// Converts this IMono into an IFlux.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source to convert</param>
        /// <returns>The new IFlux instance</returns>
        public static IFlux<T> ToFlux<T>(this IMono<T> source)
        {
            if (source is IFlux<T>)
            {
                return source as IFlux<T>;
            }
            return new PublisherWrap<T>(source);
        }

        /// <summary>
        /// Emit a single boolean true if this IMono has an element.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<bool> HasElement<T>(this IMono<T> source)
        {
            return new PublisherHasElements<T>(source);
        }

        /// <summary>
        /// Hides the identity of the source IMono, including its
        /// ISubscription. Prevents fusion optimizations.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source iMono instance.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> Hide<T>(this IMono<T> source)
        {
            return new PublisherHide<T>(source);
        }

        /// <summary>
        /// Ignores onNext signal (dropping it) and only reacts on termination.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source iMono instance.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> IgnoreElement<T>(this IMono<T> source)
        {
            return new PublisherIgnoreElements<T, T>(source);
        }

        /// <summary>
        /// Maps the Exception in the OnError signal via a mapper function.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono.</param>
        /// <param name="mapper">The function that receives the Exception from the source and returns an Exception in exchange.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> MapError<T>(this IMono<T> source, Func<Exception, Exception> mapper)
        {
            return new PublisherMapError<T>(source, mapper);
        }

        /// <summary>
        /// Maps the Exception in the OnError signal, if it is of the specified type, via a mapper function.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="E">The Exception type to map.</typeparam>
        /// <param name="source">The source IMono.</param>
        /// <param name="mapper">The function that is called if the upstream Exception is of the specified 
        /// type and returns an Exception in exchange.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> MapError<T, E>(this IMono<T> source, Func<E, Exception> mapper) where E : Exception
        {
            return MapError(source, e => {
                if (e is E)
                {
                    return mapper(e as E);
                }
                return e;
            });
        }

        /// <summary>
        /// Maps the Exception in the OnError signal, if it matches a predicate, via a mapper function.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="predicate">The predicate called with the upstream Exception and if returns true, the <paramref name="mapper"/> is called.</param>
        /// <param name="mapper">The function called with the upstream Exception, if the predicate matched, and returns an Exception in exchange.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> MapError<T>(this IMono<T> source, Func<Exception, bool> predicate, Func<Exception, Exception> mapper)
        {
            return MapError(source, e => {
                if (predicate(e))
                {
                    return mapper(e);
                }
                return e;
            });
        }

        /// <summary>
        /// Transform the incoming onNext, onError and onComplete signals into ISignal.
        /// Since the error is materialized as a {@code Signal}, the propagation will
        /// be stopped and onComplete will be mitted. Complete signal will first emit
        /// a {@code Signal.complete()} and then effectively complete the flux.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<ISignal<T>> Materialize<T>(this IMono<T> source)
        {
            // TODO implement Materialize
            throw new NotImplementedException();
        }

        /// <summary>
        /// Merge emissions of this {@link Mono} with the provided IPublisher.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="other">The other IPublisher instance.</param>
        /// <returns>The new IFlux instance.</returns>
        public static IFlux<T> MergeWith<T>(this IMono<T> source, IPublisher<T> other)
        {
            return Flux.Merge(false, source, other);
        }

        /// <summary>
        /// Emit the current instance of the IMono.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<IMono<T>> Nest<T>(this IMono<T> source)
        {
            return Just(source);
        }

        /// <summary>
        /// Emit the any of the result from this mono or from the given mono.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="other">The other IMono instance</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> Or<T>(this IMono<T> source, IMono<T> other)
        {
            return First(source, other);
        }

        /// <summary>
        /// Subscribe to a returned fallback publisher when any error occurs.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="fallback">The fallback IMono instance.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> Otherwise<T>(this IMono<T> source, IMono<T> fallback)
        {
            // TODO implement Otherwise
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribe to a returned fallback publisher when an error matching 
        /// the given type occurs.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="E">The exception type to react to</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="resumeFunction">The function that receives the given
        /// exception type and returns an IMono instance to resume with.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> Otherwise<T, E>(this IMono<T> source, Func<E, IMono<T>> resumeFunction) where E : Exception
        {
            // TODO implement Otherwise
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribe to a returned fallback publisher when an error matching 
        /// the given type occurs.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="resumeFunction">The function that receives the
        /// exception type and returns an IMono instance to resume with.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> Otherwise<T>(this IMono<T> source, Func<Exception, IMono<T>> resumeFunction)
        {
            // TODO implement Otherwise
            throw new NotImplementedException();
        }


        /// <summary>
        /// Provide an alternative IMono if this mono is completed without data.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="other">The alternative IMono instance.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> OtherwiseIfEmpty<T>(this IMono<T> source, IMono<T> other)
        {
            // TODO implement OtherwiseIfEmpty
            throw new NotImplementedException();
        }

        /// <summary>
        /// Subscribe to a returned fallback value when any error occurs.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="value">The fallback value to signal if the 
        /// source terminated with any error.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> OtherwiseReturn<T>(this IMono<T> source, T value)
        {
            return OtherwiseReturn(source, e => true, value);
        }

        /// <summary>
        /// Fallback to the given value if an error of a given type is observed on this
        /// IMono.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="E">The exception type to react to.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="value">The fallback value to signal if the 
        /// source terminated with the specified class of exception error.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> OtherwiseReturn<T, E>(this IMono<T> source, T value) where E : Exception
        {
            return OtherwiseReturn(source, e => e is E, value);
        }

        /// <summary>
        /// Fallback to the given value if an error matching the given predicate is
        /// observed on this IMono.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="predicate">The predicate receiving the exception from
        /// the source and returns true if the fallback value should be used.</param>
        /// <param name="value">The fallback value to signal if the 
        /// source terminated with an exception that matches the predicate.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> OtherwiseReturn<T>(this IMono<T> source, Func<Exception, bool> predicate, T value)
        {
            // TODO implement OtherwiseReturn
            throw new NotImplementedException();
        }

        /// <summary>
        /// Detaches the references between the upstream and downstream (the upstreams ISubscription
        /// and the downstreams ISubscriber), allowing both to be garbage collected early.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> OnTerminateDetach<T>(this IMono<T> source)
        {
            return new PublisherOnTerminateDetach<T>(source);
        }

        /// <summary>
        /// Shares a IMono for the duration of a function that may transform it and
        /// consume it as many times as necessary without causing multiple subscriptions
        /// to the upstream.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="R">The result value type</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="transformer">The function that receives a shared
        /// IMono instance and returns another IMono instance to
        /// be subscribed by the downstream.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<R> Publish<T, R>(this IMono<T> source, Func<IMono<T>, IMono<R>> transformer)
        {
            // TODO implement Publish
            throw new NotImplementedException();
        }

        /// <summary>
        /// Run onNext, onComplete and onError on a supplied Scheduler.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="scheduler">The target scheduler where t exectute the
        /// OnNext, OnError and OnComplete methods.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> PublishOn<T>(this IMono<T> source, Scheduler scheduler)
        {
            return new MonoPublishOn<T>(source, scheduler);
        }

        /// <summary>
        /// Repeatedly subscribe to the source completion of the previous subscription.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <returns>The new IFlux instance.</returns>
        public static IFlux<T> Repeat<T>(this IMono<T> source)
        {
            // TODO implement Repeat
            throw new NotImplementedException();
        }

        /// <summary>
        /// Repeatedly subscribe to the source at most the given number of times.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="times">The number of times to subscribe to the source in
        /// total.</param>
        /// <returns>The new IFlux instance.</returns>
        public static IFlux<T> Repeat<T>(this IMono<T> source, long times)
        {
            // TODO implement Repeat
            throw new NotImplementedException();
        }

        /// <summary>
        /// Repeatedly subscribe to the source if the predicate returns true after 
        /// completion of the previous subscription.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="predicate">The preducate to return true to subscribe
        /// to the source again.</param>
        /// <returns>The new IFlux instance.</returns>
        public static IFlux<T> Repeat<T>(this IMono<T> source, Func<bool> predicate)
        {
            // TODO implement Repeat
            throw new NotImplementedException();
        }

        /// <summary>
        /// Repeatedly subscribe to the source if the predicate returns true after 
        /// completion of the subscription. A specified maximum of repeat will 
        /// limit the number of re-subscribe.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="times">The number of times to subscribe to the source in
        /// total.</param>
        /// <param name="predicate">The preducate to return true to subscribe
        /// to the source again.</param>
        /// <returns>The new IFlux instance.</returns>
        public static IFlux<T> Repeat<T>(this IMono<T> source, long times, Func<bool> predicate)
        {
            // TODO implement Repeat
            throw new NotImplementedException();
        }

        /// <summary>
        /// Repeatedly subscribe to this IMono when a companion sequence 
        /// signals a number of emitted elements in response to the flux completion 
        /// signal.
        /// If the companion sequence signals when this IMono is active, the 
        /// repeat attempt is suppressed and any terminal signal will terminate
        /// this IFlux with the same signal immediately.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="whenFunction">The function providing a IFlux 
        /// signalling an exclusive number of emitted elements on onComplete and 
        /// returning a IPublisher companion.</param>
        /// <returns>The new IFlux instance.</returns>
        public static IFlux<T> RepeatWhen<T>(this IMono<T> source, Func<IFlux<long>, IPublisher<object>> whenFunction)
        {
            // TODO implement RepeatWhen
            throw new NotImplementedException();
        }

        /// <summary>
        /// Repeatedly subscribe to this IMono until there is an onNext signal 
        /// when a companion sequence signals a number of emitted elements.
        /// If the companion sequence signals when this IMono is active, the repeat
        /// attempt is suppressed and any terminal signal will terminate this IMono 
        /// with the same signal immediately.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="whenFunction">The function providing a IFlux 
        /// signalling an exclusive number of emitted elements on onComplete and 
        /// returning a IPublisher companion.</param>
        /// <returns>The new Mono instance.</returns>
        public static IMono<T> RepeatWhenEmpty<T>(this IMono<T> source, Func<IFlux<long>, IPublisher<object>> whenFunction)
        {
            // TODO implement Repeat
            throw new NotImplementedException();
        }

        /// <summary>
        /// Repeatedly subscribe to this IMono until there is an onNext signal 
        /// when a companion sequence signals a number of emitted elements.
        /// If the companion sequence signals when this IMono is active, the repeat
        /// attempt is suppressed and any terminal signal will terminate this IMono 
        /// with the same signal immediately.
        /// Emits an InvalidOperationException if the max 
        /// repeat is exceeded and different from <see cref="int.MaxValue"/>.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="times"> the maximum repeat number of time (infinite if 
        /// <see cref="int.MaxValue"/>).</param>
        /// <param name="whenFunction">The function providing a IFlux 
        /// signalling an exclusive number of emitted elements on onComplete and 
        /// returning a IPublisher companion.</param>
        /// <returns>The new Mono instance.</returns>
        public static IMono<T> RepeatWhenEmpty<T>(this IMono<T> source, long times, Func<IFlux<long>, IPublisher<object>> whenFunction)
        {
            // TODO implement Repeat
            throw new NotImplementedException();
        }

        /// <summary>
        /// Re-subscribes to this IMono sequence if it signals any error
        /// indefinitely
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <returns>The new Mono instance.</returns>
        public static IMono<T> Retry<T>(this IMono<T> source)
        {
            // TODO implement Retry
            throw new NotImplementedException();
        }

        /// <summary>
        /// Re-subscribes to this IMono sequence if it signals any error
        /// either indefinitely or a fixed number of times.
        /// The times == <see cref="int.MaxValue"/> is treated as infinite retry.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="times">the number of times to tolerate an error</param>
        /// <returns>The new Mono instance.</returns>
        public static IMono<T> Retry<T>(this IMono<T> source, long times)
        {
            // TODO implement Retry
            throw new NotImplementedException();
        }

        /// <summary>
        /// e-subscribes to this IMono sequence if it signals any error
        /// and the given predicate matches otherwise push the error downstream.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="predicate">the predicate to evaluate if retry should
        /// occur based on a given error signal</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> Retry<T>(this IMono<T> source, Func<Exception> predicate)
        {
            // TODO implement Retry
            throw new NotImplementedException();
        }

        /// <summary>
        /// Re-subscribes to this IMono sequence up to the specified
        /// number of retries if it signals any error and the given 
        /// predicate matches otherwise push the error downstream.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="times">the number of times to tolerate an error</param>
        /// <param name="predicate">the predicate to evaluate if retry should
        /// occur based on a given error signal</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> Retry<T>(this IMono<T> source, long times, Func<Exception> predicate)
        {
            // TODO implement Retry
            throw new NotImplementedException();
        }

        /// <summary>
        /// Retries this {@link Mono} when a companion sequence signals
        /// an item in response to this {@link Mono} error signal.
        /// If the companion sequence signals when the {@link Mono} is 
        /// active, the retry attempt is suppressed and any terminal signal 
        /// will terminate the IMono source with the same signal immediately.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="whenFunction">the function providing a 
        /// IFlux signalling any error from the source sequence and 
        /// returning a IPublisher companion.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> RetryWhen<T>(this IMono<T> source, Func<IFlux<Exception>, IPublisher<object>> whenFunction)
        {
            // TODO implement RepeatWhen
            throw new NotImplementedException();
        }

        /// <summary>
        /// Run the requests to this IMono on a given worker assigned by the 
        /// supplied Scheduler.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="scheduler">The target scheduler to subscribe on and
        /// emit signals when requested.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> SubscribeOn<T>(this IMono<T> source, Scheduler scheduler)
        {
            return new PublisherSubscribeOn<T>(source, scheduler);
        }

        /// <summary>
        /// Return a IMono of Void which only listens for complete and error 
        /// signals from this IMono completes.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<Void> Then<T>(this IMono<T> source)
        {
            return Empty<T>(source);
        }

        /// <summary>
        ///  Convert the value of IMono to another IMono
        ///  possibly with another value type.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="transformer">the function to dynamically bind a new IMono</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<R> Then<T, R>(this IMono<T> source, Func<T, IMono<R>> transformer)
        {
            return FlatMap(source, transformer);
        }

        /// <summary>
        /// Transform the terminal signal (error or completion) into IMono
        /// that will emit at most one result in the returned IMono.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="other">a IMono to emit from after termination</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<R> Then<T, R>(this IMono<T> source, IMono<R> other)
        {
            return new PublisherThen<T, R>(source, other);
        }

        /// <summary>
        /// Transform the terminal signal (error or completion) into IMono
        /// that will emit at most one result in the returned IMono. 
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="other">The function to generate a IMono to emit from after 
        /// termination for each subscriber.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<R> Then<T, R>(this IMono<T> source, Func<IMono<R>> other)
        {
            return Then(source, Defer(other));
        }

        /// <summary>
        /// Transform the terminal signal (error or completion) into 
        /// IPublisher that will emit at most one result in the
        /// returned IFlux.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="other">The other IPublisher instance to relay signals
        /// after the source completed.</param>
        /// <returns>The new IFlux instance.</returns>
        public static IFlux<R> ThenMany<T, R>(this IMono<T> source, IPublisher<R> other)
        {
            return new PublisherThen<T, R>(source, other);
        }

        /// <summary>
        /// Transform the terminal signal (error or completion) into 
        /// IPublisher that will emit at most one result in the returned IFlux.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="other">The function to generate a IPublisher to emit from after 
        /// termination for each subscriber.</param>
        /// <returns>The new IFlux instance.</returns>
        public static IFlux<R> ThenMany<T, R>(this IMono<T> source, Func<IPublisher<R>> other)
        {
            return ThenMany(source, Flux.Defer(other));
        }

        /// <summary>
        /// Signal a TimeoutException error 
        /// in case an item doesn't arrive before the given period.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="timeout">The time to wait before switching to the
        /// fallback IMono or signalling a TimeoutException.</param>
        /// <param name="fallback">If not null, a timeout switches to
        /// this IMono instance.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> Timeout<T>(this IMono<T> source, TimeSpan timeout, IMono<T> fallback = null)
        {
            return Timeout(source, timeout, DefaultScheduler.Instance);
        }

        /// <summary>
        /// Signal a TimeoutException error in case an item doesn't arrive before 
        /// the given period, run on the specified timed scheduler.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="timeout">The time to wait before switching to the
        /// fallback IMono or signalling a TimeoutException.</param>
        /// <param name="scheduler">The scheduler to await the first value.</param>
        /// <param name="fallback">If not null, a timeout switches to
        /// this IMono instance.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> Timeout<T>(this IMono<T> source, TimeSpan timeout, TimedScheduler scheduler, IMono<T> fallback = null)
        {
            // TODO implement Timeout
            throw new NotImplementedException();
        }

        /// <summary>
        /// Signal a TimeoutException in case the 
        /// item from this IMono has  not been emitted before the given
        /// IPublisher emits.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="U">The timeout IPublisher value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="firstTimeout">the timeout IPublisher that must not 
        /// emit before the first signal from this IMono.</param>
        /// <param name="fallback">If not null, a timeout switches to
        /// this IMono instance.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<T> Timeout<T, U>(this IMono<T> source, IPublisher<U> firstTimeout, IMono<T> fallback = null)
        {
            // TODO implement Timeout
            throw new NotImplementedException();
        }

        /// <summary>
        /// Emit a Timed record with the current system time in millis
        /// the associated data from the source IMono if any.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<Timed<T>> Timestamp<T>(this IMono<T> source)
        {
            return Timestamp(source, DefaultScheduler.Instance);
        }

        /// <summary>
        /// Emit a Timed record with the current system time in millis
        /// the associated data from the source IMono if any as
        /// determined by the given timed scheduler.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="scheduler">The timed scheduler for the notion of current
        /// time.</param>
        /// <returns>The new IMono instance.</returns>
        public static IMono<Timed<T>> Timestamp<T>(this IMono<T> source, TimedScheduler scheduler)
        {
            return Map(source, v => new Timed<T>(v, scheduler.NowUtc));
        }

        /// <summary>
        /// Convert this IMono into an IObservable.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono.</param>
        /// <returns>The new IObservable.</returns>
        public static IObservable<T> ToObservable<T>(this IMono<T> source)
        {
            return new PublisherAsObservable<T>(source);
        }

        // ---------------------------------------------------------------------------------------------------------
        // Leave the reactive world
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Subscribe to the source and block until it produces a value or
        /// signals an Exception. An empty source will throw an IndexOutOfRangeException.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="source">The source</param>
        /// <returns>The value produced</returns>
        /// <exception cref="IndexOutOfRangeException">If the source is empty.</exception>
        public static T Block<T>(this IMono<T> source)
        {
            var s = new BlockingFirstSubscriber<T>();
            source.Subscribe(s);
            return s.Get(true);
        }

        /// <summary>
        /// Subscribe to the source and block until it produces a value or
        /// signals an Exception. An empty source will throw an IndexOutOfRangeException.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="source">The source</param>
        /// <param name="timeout">The maximum amount of time to wait for the value.</param>
        /// <returns>The value produced</returns>
        /// <exception cref="IndexOutOfRangeException">If the source is empty.</exception>
        /// <exception cref="TimeoutException">If the source didn't produce any value within the given timeout.</exception>
        public static T Block<T>(this IMono<T> source, TimeSpan timeout)
        {
            var s = new BlockingFirstSubscriber<T>();
            source.Subscribe(s);
            return s.Get(timeout, true, true);
        }

        /// <summary>
        /// Subscribes to the IPublisher and ignores all of its signals.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="source">The source IPublisher</param>
        /// <returns>The IDisposable that allows cancelling the subscription.</returns>
        public static IDisposable Subscribe<T>(this IMono<T> source)
        {
            var d = new CallbackSubscriber<T>(v => { }, e => { ExceptionHelper.OnErrorDropped(e); }, () => { });
            source.Subscribe(d);
            return d;
        }

        /// <summary>
        /// Subscribes to the IPublisher and consumes only its OnNext signals.
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="source">The source IPublisher</param>
        /// <param name="onNext">The callback for the OnNext signals</param>
        /// <returns>The IDisposable that allows cancelling the subscription.</returns>
        public static IDisposable Subscribe<T>(this IMono<T> source, Action<T> onNext)
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
        public static IDisposable Subscribe<T>(this IMono<T> source, Action<T> onNext, Action<Exception> onError)
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
        public static IDisposable Subscribe<T>(this IMono<T> source, Action<T> onNext, Action<Exception> onError, Action onComplete)
        {
            var d = new CallbackSubscriber<T>(onNext, onError, onComplete);
            source.Subscribe(d);
            return d;
        }

        /// <summary>
        /// Subscribes to the source IMono with the ISubscriber instance
        /// and returns it.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <typeparam name="E">The type of the ISubscriber.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <param name="subscriber">The subscriber to subscribe with to the IMono source.</param>
        /// <returns>The input ISubscriber instance</returns>
        public static E SubscribeWith<T, E>(this IMono<T> source, E subscriber) where E : ISubscriber<T>
        {
            source.Subscribe(subscriber);
            return subscriber;
        }

        /// <summary>
        /// Convert the source IMono into a blocking enumerable that
        /// signals the value or error from the given IMono.
        /// </summary>
        /// <typeparam name="T">The source value type.</typeparam>
        /// <param name="source">The source IMono instance.</param>
        /// <returns>The new IEnumerable instance.</returns>
        public static IEnumerable<T> ToEnumerable<T>(this IMono<T> source)
        {
            // TODO implement ToEnumerable
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns a Task that awaits a single item from the IMono or
        /// signals an IndexOutOfRangeException if the IMono is empty.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono</param>
        /// <returns>The task.</returns>
        public static Task<T> ToTask<T>(this IMono<T> source)
        {
            var s = new TaskFirstSubscriber<T>();
            source.Subscribe(s);
            return s.Task();
        }

        /// <summary>
        /// Returns a Task that awaits a single item from the IMono or
        /// signals an IndexOutOfRangeException if the IMono is empty.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IMono</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The task.</returns>
        public static Task<T> ToTask<T>(this IMono<T> source, CancellationToken ct)
        {
            var s = new TaskFirstSubscriber<T>();
            source.Subscribe(s);
            return s.Task(ct);
        }

        /// <summary>
        /// Return a Task that waits for the IMono source to complete.
        /// </summary>
        /// <param name="source">The source IMono</param>
        /// <returns>The task.</returns>
        public static Task WhenCompleteTask<T>(this IMono<T> source)
        {
            var s = new TaskCompleteSubscriber<T>();
            source.Subscribe(s);
            return s.Task();
        }

        /// <summary>
        /// Return a Task that waits for the IMono source to complete
        /// and support the cancellation of such wait.
        /// </summary>
        /// <param name="source">The source IMono</param>
        /// <param name="ct">The cancellation token.</param>
        /// <returns>The task.</returns>
        public static Task WhenCompleteTask<T>(this IMono<T> source, CancellationToken ct)
        {
            var s = new TaskCompleteSubscriber<T>();
            source.Subscribe(s);
            return s.Task(ct);
        }

        /// <summary>
        /// Creates a TestSubscriber with the given initial settings and returns it.
        /// </summary>
        /// <typeparam name="T">The value type received.</typeparam>
        /// <param name="source">The source IMono</param>
        /// <param name="initialRequest">The optional initial request amount.</param>
        /// <param name="fusionMode">The optional fusion mode if supported by the source.</param>
        /// <param name="cancelled">Optionally start out as cancelled.</param>
        /// <returns></returns>
        public static TestSubscriber<T> Test<T>(this IMono<T> source, long initialRequest = long.MaxValue, int fusionMode = 0, bool cancelled = false)
        {
            TestSubscriber<T> ts = new TestSubscriber<T>(initialRequest, fusionMode);
            if (cancelled)
            {
                ts.Cancel();
            }

            source.Subscribe(ts);

            return ts;
        }
    }
}
