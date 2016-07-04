using Reactive.Streams;
using Reactor.Core.parallel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactor.Core
{
    /// <summary>
    /// Base class for parallel publishers that take an array of Subscribers.
    /// and allow running sequences in parallel.
    /// </summary>
    /// <remarks>
    /// <p>Use {@code fork()} to start processing a regular Publisher in 'rails'.</p>
    /// <p>Use {@code runOn()} to introduce where each 'rail' shoud run on thread-vise.</p>
    /// <p>Use {@code join()} to merge the sources back into a single Publisher.</p>
    /// </remarks>
    public static class ParallelFlux
    {
        /// <summary>
        /// Take a Publisher and prepare to consume it on multiple 'rails' (number 
        /// of CPUs) in a round-robin fashion.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlux instance.</param>
        /// <param name="ordered">if converted back to a IFlux, should the end result be ordered?</param>
        /// <returns>the new IParallelPublisher instance</returns>
        public static IParallelFlux<T> Parallel<T>(this IFlux<T> source, bool ordered = false)
        {
            return Parallel(source, Environment.ProcessorCount, ordered);
        }

        /// <summary>
        /// ake a Publisher and prepare to consume it on parallallism number of 'rails', 
        /// possibly ordered and round-robin fashion.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlux instance.</param>
        /// <param name="parallelism">the number of parallel rails</param>
        /// <param name="ordered">if converted back to a IFlux, should the end result be ordered?</param>
        /// <returns>the new IParallelPublisher instance</returns>
        public static IParallelFlux<T> Parallel<T>(this IFlux<T> source, int parallelism, bool ordered = false)
        {
            return Parallel(source, parallelism, Flux.BufferSize, ordered);
        }

        /// <summary>
        /// Take a Publisher and prepare to consume it on parallallism number of 'rails',
        /// possibly ordered and round-robin fashion and use custom prefetch amount and queue
        /// for dealing with the source Publisher's values.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IFlux instance.</param>
        /// <param name="parallelism">the number of parallel rails</param>
        /// <param name="prefetch">the number of values to prefetch from the source</param>
        /// <param name="ordered">if converted back to a IFlux, should the end result be ordered?</param>
        /// <returns>the new IParallelPublisher instance</returns>
        public static IParallelFlux<T> Parallel<T>(this IFlux<T> source, int parallelism, int prefetch, bool ordered = false)
        {
            if (ordered)
            {
                return new ParallelOrderedFork<T>(source, parallelism, prefetch);
            }
            return new ParallelUnorderedFork<T>(source, parallelism, prefetch);
        }

        /// <summary>
        /// aps the source values on each 'rail' to another value.
        /// </summary>
        /// <remarks>
        /// Note that the same predicate may be called from multiple threads concurrently.
        /// </remarks>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="mapper">the mapper function turning Ts into Us</param>
        /// <returns>the new IParallelPublisher instance</returns>
        public static IParallelFlux<R> Map<T, R>(this IParallelFlux<T> source, Func<T, R> mapper)
        {
            if (source.IsOrdered)
            {
                return new ParallelOrderedMap<T, R>((ParallelOrderedFlux<T>)source, mapper);
            }
            return new ParallelUnorderedMap<T, R>(source, mapper);
        }

        /// <summary>
        /// Filters the source values on each 'rail'.
        /// </summary>
        /// <remarks>
        /// Note that the same predicate may be called from multiple threads concurrently.
        /// </remarks>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="predicate">the function returning true to keep a value or false to drop a value</param>
        /// <returns>the new IParallelPublisher instance</returns>
        public static IParallelFlux<T> Filter<T>(this IParallelFlux<T> source, Func<T, bool> predicate)
        {
            if (source.IsOrdered)
            {
                return new ParallelOrderedFilter<T>((ParallelOrderedFlux<T>)source, predicate);
            }
            return new ParallelUnorderedFilter<T>(source, predicate);
        }

        /// <summary>
        /// Specifies where each 'rail' will observe its incoming values with
        /// no work-stealing and default prefetch amount.
        /// </summary>
        /// <remarks>
        /// This operator uses the default prefetch size returned by {@code Px.bufferSize()}.
        /// <p/>
        /// The operator will call {@code Scheduler.createWorker()} as many
        /// times as this ParallelPublisher's parallelism level is.
        /// <p/>
        /// No assumptions are made about the Scheduler's parallelism level,
        /// if the Scheduler's parallelism level is lwer than the ParallelPublisher's,
        /// some rails may end up on the same thread/worker.
        /// <p/>
        /// This operator doesn't require the Scheduler to be trampolining as it
        /// does its own built-in trampolining logic.
        /// </remarks>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="scheduler">The scheduler whose workers to use.</param>
        /// <returns>the new IParallelPublisher instance</returns>
        public static IParallelFlux<T> RunOn<T>(this IParallelFlux<T> source, Scheduler scheduler)
        {
            return RunOn(source, scheduler, Flux.BufferSize);
        }

        /// <summary>
        /// Specifies where each 'rail' will observe its incoming values with
        /// no work-stealing and a given prefetch amount.
        /// </summary>
        /// <remarks>
        /// This operator uses the default prefetch size returned by {@code Px.bufferSize()}.
        /// <p/>
        /// The operator will call {@code Scheduler.createWorker()} as many
        /// times as this ParallelPublisher's parallelism level is.
        /// <p/>
        /// No assumptions are made about the Scheduler's parallelism level,
        /// if the Scheduler's parallelism level is lwer than the ParallelPublisher's,
        /// some rails may end up on the same thread/worker.
        /// <p/>
        /// This operator doesn't require the Scheduler to be trampolining as it
        /// does its own built-in trampolining logic.
        /// </remarks>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="scheduler">The scheduler whose workers to use.</param>
        /// <param name="prefetch">the number of values to request on each 'rail' from the source</param>
        /// <returns>the new IParallelPublisher instance</returns>
        public static IParallelFlux<T> RunOn<T>(this IParallelFlux<T> source, Scheduler scheduler, int prefetch)
        {
            if (source.IsOrdered)
            {
                return new ParallelOrderedRunOn<T>((ParallelOrderedFlux<T>)source, scheduler, prefetch);
            }
            return new ParallelUnorderedRunOn<T>(source, scheduler, prefetch);
        }

        /// <summary>
        /// Reduces all values within a 'rail' and across 'rails' with a reducer function into a single
        /// sequential value.
        /// </summary>
        /// <remarks>
        /// Note that the same reducer function may be called from multiple threads concurrently.
        /// </remarks>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="reducer">the function to reduce two values into one</param>
        /// <returns>the new IFlux instance emitting the reduced value or empty if the IParallelPublisher was empty</returns>
        public static IFlux<T> Reduce<T>(this IParallelFlux<T> source, Func<T, T, T> reducer)
        {
            return new ParallelReduceFull<T>(source, reducer);
        }

        /// <summary>
        /// Reduces all values within a 'rail' to a single value (with a possibly different type) via
        /// a reducer function that is initialized on each rail from an initialSupplier value.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="R">The reduced output type</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="initialValue">the supplier for the initial value</param>
        /// <param name="reducer">the function to reduce a previous output of reduce (or the initial value supplied)
        /// with a current source value.</param>
        /// <returns>the new IParallelPublisher instance</returns>
        public static IParallelFlux<R> Reduce<T, R>(this IParallelFlux<T> source, Func<R> initialValue, Func<R, T, R> reducer)
        {
            return new ParallelReduce<T, R>(source, initialValue, reducer);
        }

        /// <summary>
        /// Merges the values from each 'rail' in a round-robin or same-order fashion and
        /// exposes it as a regular IFLux sequence, running with a default prefetch value
        /// for the rails.
        /// </summary>
        /// <remarks>
        /// This operator uses the default prefetch size returned by <see cref="Flux.BufferSize"/>.
        /// </remarks>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <returns>The new IFlux instance</returns>
        public static IFlux<T> Sequential<T>(this IParallelFlux<T> source)
        {
            return Sequential(source, Flux.BufferSize);
        }

        /// <summary>
        /// Merges the values from each 'rail' in a round-robin or same-order fashion and
        /// exposes it as a regular Publisher sequence, running with a give prefetch value
        /// for the rails.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="prefetch">the prefetch amount to use for each rail</param>
        /// <returns>The new IFlux instance</returns>
        public static IFlux<T> Sequential<T>(this IParallelFlux<T> source, int prefetch)
        {
            if (source.IsOrdered)
            {
                return new ParallelOrderedJoin<T>((ParallelOrderedFlux<T>)source, prefetch);
            }
            return new ParallelUnorderedJoin<T>(source, prefetch);
        }

        /// <summary>
        /// Sorts the 'rails' of this ParallelPublisher and returns a Publisher that sequentially
        /// picks the smallest next value from the rails based on the type-default comparer.
        /// </summary>
        /// <remarks>
        /// This operator requires a finite source IParallelPublisher.
        /// </remarks>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="capacityHint">the expected number of total elements</param>
        /// <returns>The new IFlux instance</returns>
        public static IFlux<T> Sorted<T>(this IParallelFlux<T> source, int capacityHint = 16)
        {
            return Sorted(source, Comparer<T>.Default, capacityHint);
        }

        /// <summary>
        /// Sorts the 'rails' of this ParallelPublisher and returns a Publisher that sequentially
        /// picks the smallest next value from the rails.
        /// </summary>
        /// <remarks>
        /// This operator requires a finite source IParallelPublisher.
        /// </remarks>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="comparer">the IComparer to use</param>
        /// <param name="capacityHint">the expected number of total elements</param>
        /// <returns>The new IFlux instance</returns>
        public static IFlux<T> Sorted<T>(this IParallelFlux<T> source, IComparer<T> comparer, int capacityHint = 16)
        {
            int ch = capacityHint / source.Parallelism + 1;
            IParallelFlux<List<T>> lists = source.Reduce(() => new List<T>(ch), (a, b) =>
            {
                a.Add(b);
                return a;
            })
            .Map(v =>
            {
                v.Sort(comparer);
                return v;
            });

            return new ParallelSortedJoin<T>(lists, comparer);
        }

        /// <summary>
        /// Sorts the 'rails' according to the comparator and returns a full sorted list as a Publisher.
        /// </summary>
        /// <remarks>
        /// This operator requires a finite source IParallelPublisher.
        /// </remarks>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="comparer">the IComparer to use</param>
        /// <param name="capacityHint">the expected number of total elements</param>
        /// <returns>The new IFlux instance</returns>
        public static IMono<IList<T>> ToSortedList<T>(this IParallelFlux<T> source, IComparer<T> comparer, int capacityHint = 16)
        {
            return Sorted<T>(source, comparer, capacityHint).CollectList(capacityHint);
        }

        /// <summary>
        /// Call the specified action after each 'rail' terminates.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="onAfterTerminate">The action to call</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<T> DoAfterTerminate<T>(this IParallelFlux<T> source, Action onAfterTerminate)
        {
            // return PublisherPeek<T>.withOnAfterTerminate(source, onAfterTerminate);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoAfterTerminate
            throw new NotImplementedException();
        }

        /// <summary>
        /// Call the specified consumer with the current element passing through any 'rail'
        /// after it has been delivered to downstream within the rail.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="onAfterNext">the action to call</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<T> DoAfterNext<T>(this IParallelFlux<T> source, Action<T> onAfterNext)
        {
            // return PublisherPeek<T>.withOnAfterNext(source, onAfterNext);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoAfterNext
            throw new NotImplementedException();
        }

        /// <summary>
        /// Run the specified runnable when a 'rail' receives a cancellation.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="onCancel">The action to call</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<T> DoOnCancel<T>(this IParallelFlux<T> source, Action onCancel)
        {
            // return PublisherPeek<T>.withOnCancel(source, onCancel);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoOnCancel
            throw new NotImplementedException();
        }

        /// <summary>
        /// Run the specified runnable when a 'rail' completes.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="onComplete">The action to call</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<T> DoOnComplete<T>(this IParallelFlux<T> source, Action onComplete)
        {
            // return PublisherPeek<T>.withOnComplete(source, onComplete);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoOnComplete
            throw new NotImplementedException();
        }

        /// <summary>
        /// Call the specified consumer with the exception passing through any 'rail'.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="onError">The action to call</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<T> DoOnError<T>(this IParallelFlux<T> source, Action<Exception> onError)
        {
            // return PublisherPeek<T>.withOnError(source, onError);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoOnError
            throw new NotImplementedException();
        }

        /// <summary>
        /// Call the specified consumer with a specific exception class passing through any 'rail'.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="E">The exception type</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="onError">The action to call</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<T> DoOnError<T, E>(this IParallelFlux<T> source, Action<E> onError) where E : Exception
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
        /// Call the specified predicate with a errors passing through any 'rail' 
        /// and if it returns true, call the action with it.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="predicate">The predicate to call with any errors passing through.</param>
        /// <param name="onError">The action to call</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<T> DoOnError<T>(this IParallelFlux<T> source, Func<Exception, bool> predicate, Action<Exception> onError)
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
        /// Call the specified consumer with the current element passing through any 'rail'.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="onNext">The action to call</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<T> DoOnNext<T>(this IParallelFlux<T> source, Action<T> onNext)
        {
            // return PublisherPeek<T>.withOnNext(source, onNext);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoOnError
            throw new NotImplementedException();
        }

        /// <summary>
        /// Call the specified consumer with the request amount if any rail receives a request.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="onRequest">the action to call</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<T> DoOnRequest<T>(this IParallelFlux<T> source, Action<long> onRequest)
        {
            // return PublisherPeek<T>.withOnRequest(source, onRequest);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoOnRequest
            throw new NotImplementedException();
        }

        /// <summary>
        /// Call the specified callback when a 'rail' receives a Subscription from its upstream.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="onSubscribe">The action to call</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<T> DoOnSubscribe<T>(this IParallelFlux<T> source, Action<ISubscription> onSubscribe)
        {
            // return PublisherPeek<T>.withOnSubscribe(source, onSubscribe);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoOnSubscribe
            throw new NotImplementedException();
        }

        /// <summary>
        /// Call the specified callback before the terminal event is delivered on each 'rail'.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="onTerminate">The action to call</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<T> DoOnTerminate<T>(this IParallelFlux<T> source, Action onTerminate)
        {
            // return PublisherPeek<T>.withOnTerminate(source, onTerminate);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoOnTerminate
            throw new NotImplementedException();
        }

        /// <summary>
        /// Collect the elements in each rail into a collection supplied via a collectionSupplier
        /// and collected into with a collector action, emitting the collection at the end.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <typeparam name="C">the collection type</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="initialCollection">the supplier of the collection in each rail</param>
        /// <param name="collector">the collector, taking the per-rali collection and the current item</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<C> Collect<T, C>(this IParallelFlux<T> source, Func<C> initialCollection, Action<C, T> collector)
        {
            return Reduce<T, C>(source, initialCollection, (a, b) => { collector(a, b); return a; });
        }

        /// <summary>
        /// Exposes the 'rails' as individual GroupedPublisher instances, keyed by the rail index (zero based).
        /// </summary>
        /// <remarks>
        /// Each group can be consumed only once; requests and cancellation compose through. Note
        /// that cancelling only one rail may result in undefined behavior.
        /// </remarks>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <returns>The new IFlux instance with the inner IGroupedFlux instances.</returns>
        public static IFlux<IGroupedFlux<int, T>> Groups<T>(this IParallelFlux<T> source)
        {
            // TODO implement Groups
            throw new NotImplementedException();
        }

        /// <summary>
        /// Wraps multiple Publishers into a ParallelPublisher which runs them
        /// in parallel and unordered.
        /// </summary>
        /// <typeparam name="T">the value type</typeparam>
        /// <param name="sources">the params array of IPublishers</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<T> From<T>(params IPublisher<T>[] sources)
        {
            // TODO implement From
            throw new NotImplementedException();
        }

        /// <summary>
        /// Turns this Parallel sequence into an ordered sequence via local indexing,
        /// if not already ordered.
        /// </summary>
        /// <typeparam name="T">the value type</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="global">hould the indexing local (per rail) or globar FIFO?</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<T> Ordered<T>(this IParallelFlux<T> source, bool global = false)
        {
            if (source.IsOrdered)
            {
                return source;
            }
            // TODO implement Ordered
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes any ordering information from this Parallel sequence,
        /// if not already unordered.
        /// </summary>
        /// <typeparam name="T">the value type</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<T> Unordered<T>(this IParallelFlux<T> source)
        {
            if (!source.IsOrdered)
            {
                return source;
            }
            // TODO implement Unordered
            throw new NotImplementedException();
        }

        /// <summary>
        /// Perform a fluent transformation to a value via a converter function which
        /// receives this ParallelPublisher.
        /// </summary>
        /// <typeparam name="T">the value type</typeparam>
        /// <typeparam name="R">The result type</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="converter">the converter function from IParallelPublisher to some type</param>
        /// <returns>the value returned by the converter function</returns>
        public static R As<T, R>(this IParallelFlux<T> source, Func<IParallelFlux<T>, R> converter)
        {
            return converter(source);
        }

        /// <summary>
        /// Generates and flattens IPublishers on each 'rail'.
        /// </summary>
        /// <remarks>
        /// Errors are not delayed and uses unbounded concurrency along with default inner prefetch.
        /// </remarks>
        /// <typeparam name="T">the value type</typeparam>
        /// <typeparam name="R">The result type</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="mapper">the function to map each rail's value into a IPublisher</param>
        /// <param name="delayErrors">should the errors from the main and the inner sources delayed till everybody terminates?</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<R> FlatMap<T, R>(this IParallelFlux<T> source, Func<T, IPublisher<R>> mapper, bool delayErrors = false)
        {
            return FlatMap(source, mapper, Flux.BufferSize, Flux.BufferSize, delayErrors);
        }

        /// <summary>
        /// Generates and flattens Publishers on each 'rail', optionally delaying errors 
        /// and having a total number of simultaneous subscriptions to the inner IPublishers.
        /// </summary>
        /// <typeparam name="T">the value type</typeparam>
        /// <typeparam name="R">The result type</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="mapper">the function to map each rail's value into a IPublisher</param>
        /// <param name="maxConcurrency">the maximum number of simultaneous subscriptions to the generated inner IPublishers</param>
        /// <param name="delayErrors">should the errors from the main and the inner sources delayed till everybody terminates?</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<R> FlatMap<T, R>(this IParallelFlux<T> source, Func<T, IPublisher<R>> mapper, int maxConcurrency, bool delayErrors = false)
        {
            return FlatMap(source, mapper, maxConcurrency, Flux.BufferSize, delayErrors);
        }

        /// <summary>
        /// Generates and flattens Publishers on each 'rail', optionally delaying errors, 
        /// having a total number of simultaneous subscriptions to the inner Publishers
        /// and using the given prefetch amount for the inner Publishers.
        /// </summary>
        /// <typeparam name="T">the value type</typeparam>
        /// <typeparam name="R">The result type</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="mapper">the function to map each rail's value into a IPublisher</param>
        /// <param name="maxConcurrency">the maximum number of simultaneous subscriptions to the generated inner IPublishers</param>
        /// <param name="prefetch">the number of items to prefetch from each inner IPublisher</param>
        /// <param name="delayErrors">should the errors from the main and the inner sources delayed till everybody terminates?</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<R> FlatMap<T, R>(this IParallelFlux<T> source, Func<T, IPublisher<R>> mapper, int maxConcurrency, int prefetch, bool delayErrors = false)
        {
            // TODO implement FlatMap
            throw new NotImplementedException();
        }

        /// <summary>
        /// Generates and concatenates Publishers on each 'rail', signalling errors immediately 
        /// and using the given prefetch amount for generating Publishers upfront.
        /// </summary>
        /// <typeparam name="T">the value type</typeparam>
        /// <typeparam name="R">The result type</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="mapper">the function to map each rail's value into a IPublisher</param>
        /// <param name="errorMode">the error handling, i.e., when to report errors from the main
        /// source and the inner Publishers (immediate, boundary, end)</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<R> ConcatMap<T, R>(this IParallelFlux<T> source, Func<T, IPublisher<R>> mapper, ConcatErrorMode errorMode = ConcatErrorMode.Immediate)
        {
            return ConcatMap(source, mapper, 2, errorMode);
        }

        /// <summary>
        /// Generates and concatenates Publishers on each 'rail', optionally delaying errors
        /// and using the given prefetch amount for generating IPublishers upfront.
        /// </summary>
        /// <typeparam name="T">the value type</typeparam>
        /// <typeparam name="R">The result type</typeparam>
        /// <param name="source">The source IParallelPublisher instance.</param>
        /// <param name="mapper">the function to map each rail's value into a IPublisher</param>
        /// <param name="prefetch">the number of items to prefetch from each inner IPublisher</param>
        /// <param name="errorMode">the error handling, i.e., when to report errors from the main
        /// source and the inner Publishers (immediate, boundary, end)</param>
        /// <returns>The new IParallelFlux instance</returns>
        public static IParallelFlux<R> ConcatMap<T, R>(this IParallelFlux<T> source, Func<T, IPublisher<R>> mapper, int prefetch, ConcatErrorMode errorMode = ConcatErrorMode.Immediate)
        {
            // TODO implement ConcatMap
            throw new NotImplementedException();
        }
    }
}
