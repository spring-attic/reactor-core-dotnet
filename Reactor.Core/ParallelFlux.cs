using Reactive.Streams;
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
        public static IParallelFlux<T> Parallel<T>(this IFlux<T> source, bool ordered = false)
        {
            // TODO implement Parallel
            throw new NotImplementedException();
        }

        public static IParallelFlux<T> Parallel<T>(this IFlux<T> source, int parallelism, bool ordered = false)
        {
            // TODO implement Parallel
            throw new NotImplementedException();
        }

        public static IParallelFlux<T> Parallel<T>(this IFlux<T> source, int parallelism, int prefetch, bool ordered = false)
        {
            // TODO implement Parallel
            throw new NotImplementedException();
        }

        public static IParallelFlux<R> Map<T, R>(this IParallelFlux<T> source, Func<T, R> mapper)
        {
            if (source.IsOrdered)
            {

            }
            // TODO implement Map
            throw new NotImplementedException();
        }

        public static IParallelFlux<T> Filter<T>(this IParallelFlux<T> source, Func<T, bool> predicate)
        {
            if (source.IsOrdered)
            {

            }
            // TODO implement Filter
            throw new NotImplementedException();
        }

        public static IParallelFlux<T> RunOn<T>(this IParallelFlux<T> source, Scheduler scheduler)
        {
            return RunOn(source, scheduler, Flux.BufferSize);
        }

        public static IParallelFlux<T> RunOn<T>(this IParallelFlux<T> source, Scheduler scheduler, int prefetch)
        {
            if (source.IsOrdered)
            {

            }
            // TODO implement RunOn
            throw new NotImplementedException();
        }

        public static IFlux<T> Reduce<T>(this IParallelFlux<T> source, Func<T, T, T> reducer)
        {
            if (source.IsOrdered)
            {

            }
            // TODO implement Reduce
            throw new NotImplementedException();
        }

        public static IParallelFlux<R> Reduce<T, R>(this IParallelFlux<T> source, Func<R> initialValue, Func<R, T, R> reducer)
        {
            if (source.IsOrdered)
            {

            }
            // TODO implement Reduce
            throw new NotImplementedException();
        }

        public static IFlux<T> Sequential<T>(this IParallelFlux<T> source)
        {
            return Sequential(source, Flux.BufferSize);
        }

        public static IFlux<T> Sequential<T>(this IParallelFlux<T> source, int prefetch)
        {
            if (source.IsOrdered)
            {

            }
            // TODO implement Sequential
            throw new NotImplementedException();
        }

        public static IFlux<T> Sorted<T>(this IParallelFlux<T> source, IComparer<T> comparer, int capacityHint = 16)
        {
            if (source.IsOrdered)
            {

            }
            // TODO implement Sorted
            throw new NotImplementedException();
        }

        public static IFlux<List<T>> ToSortedList<T>(this IParallelFlux<T> source, IComparer<T> comparer, int capacityHint = 16)
        {
            if (source.IsOrdered)
            {

            }
            // TODO implement Sorted
            throw new NotImplementedException();
        }

        public static IParallelFlux<T> DoAfterTerminate<T>(this IParallelFlux<T> source, Action onAfterTerminate)
        {
            // return PublisherPeek<T>.withOnAfterTerminate(source, onAfterTerminate);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoAfterTerminate
            throw new NotImplementedException();
        }

        public static IParallelFlux<T> DoAfterNext<T>(this IParallelFlux<T> source, Action<T> onAfterNext)
        {
            // return PublisherPeek<T>.withOnAfterNext(source, onAfterNext);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoAfterNext
            throw new NotImplementedException();
        }

        public static IParallelFlux<T> DoOnCancel<T>(this IParallelFlux<T> source, Action onCancel)
        {
            // return PublisherPeek<T>.withOnCancel(source, onCancel);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoOnCancel
            throw new NotImplementedException();
        }

        public static IParallelFlux<T> DoOnComplete<T>(this IParallelFlux<T> source, Action onComplete)
        {
            // return PublisherPeek<T>.withOnComplete(source, onComplete);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoOnComplete
            throw new NotImplementedException();
        }

        public static IParallelFlux<T> DoOnError<T>(this IParallelFlux<T> source, Action<Exception> onError)
        {
            // return PublisherPeek<T>.withOnError(source, onError);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoOnError
            throw new NotImplementedException();
        }

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

        public static IParallelFlux<T> DoOnNext<T>(this IParallelFlux<T> source, Action<T> onNext)
        {
            // return PublisherPeek<T>.withOnNext(source, onNext);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoOnError
            throw new NotImplementedException();
        }

        public static IParallelFlux<T> DoOnRequest<T>(this IParallelFlux<T> source, Action<long> onRequest)
        {
            // return PublisherPeek<T>.withOnRequest(source, onRequest);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoOnRequest
            throw new NotImplementedException();
        }

        public static IParallelFlux<T> DoOnSubscribe<T>(this IParallelFlux<T> source, Action<ISubscription> onSubscribe)
        {
            // return PublisherPeek<T>.withOnSubscribe(source, onSubscribe);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoOnSubscribe
            throw new NotImplementedException();
        }

        public static IParallelFlux<T> DoOnTerminate<T>(this IParallelFlux<T> source, Action onTerminate)
        {
            // return PublisherPeek<T>.withOnTerminate(source, onTerminate);
            if (source.IsOrdered)
            {

            }
            // TODO implement DoOnTerminate
            throw new NotImplementedException();
        }

        public static IParallelFlux<C> Collect<T, C>(this IParallelFlux<T> source, Func<C> initialCollection, Action<C, T> collector)
        {
            if (source.IsOrdered)
            {

            }
            // TODO implement Collect
            throw new NotImplementedException();
        }

        public static IFlux<IGroupedFlux<int, T>> Groups<T>(this IParallelFlux<T> source)
        {
            // TODO implement Groups
            throw new NotImplementedException();
        }

        public static IParallelFlux<T> From<T>(params IPublisher<T>[] sources)
        {
            // TODO implement From
            throw new NotImplementedException();
        }

        public static IParallelFlux<T> Ordered<T>(this IParallelFlux<T> source, bool global = false)
        {
            if (source.IsOrdered)
            {
                return source;
            }
            // TODO implement Ordered
            throw new NotImplementedException();
        }

        public static IParallelFlux<T> Unordered<T>(this IParallelFlux<T> source)
        {
            if (!source.IsOrdered)
            {
                return source;
            }
            // TODO implement Unordered
            throw new NotImplementedException();
        }

        public static R As<T, R>(this IParallelFlux<T> source, Func<IParallelFlux<T>, R> converter)
        {
            return converter(source);
        }

        public static IParallelFlux<R> FlatMap<T, R>(this IParallelFlux<T> source, Func<T, IPublisher<R>> mapper, bool delayErrors = false)
        {
            return FlatMap(source, mapper, Flux.BufferSize, Flux.BufferSize, delayErrors);
        }

        public static IParallelFlux<R> FlatMap<T, R>(this IParallelFlux<T> source, Func<T, IPublisher<R>> mapper, int maxConcurrency, bool delayErrors = false)
        {
            return FlatMap(source, mapper, maxConcurrency, Flux.BufferSize, delayErrors);
        }

        public static IParallelFlux<R> FlatMap<T, R>(this IParallelFlux<T> source, Func<T, IPublisher<R>> mapper, int maxConcurrency, int prefetch, bool delayErrors = false)
        {
            // TODO implement FlatMap
            throw new NotImplementedException();
        }

        public static IParallelFlux<R> ConcatMap<T, R>(this IParallelFlux<T> source, Func<T, IPublisher<R>> mapper, ConcatErrorMode errorMode = ConcatErrorMode.Immediate)
        {
            return ConcatMap(source, mapper, 2, errorMode);
        }

        public static IParallelFlux<R> ConcatMap<T, R>(this IParallelFlux<T> source, Func<T, IPublisher<R>> mapper, int prefetch, ConcatErrorMode errorMode = ConcatErrorMode.Immediate)
        {
            // TODO implement ConcatMap
            throw new NotImplementedException();
        }
    }
}
