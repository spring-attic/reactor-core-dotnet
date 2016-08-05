using System;

namespace Reactive.Streams
{
    /*
    /// <summary>
    /// <para>
    /// A <see cref="IPublisher{T}"/> is a provider of a potentially unbounded number of sequenced elements,
    /// publishing them according to the demand received from its <see cref="ISubscriber{T}"/>.
    /// </para>
    /// <para>
    /// A <see cref="IPublisher{T}"/> can serve multiple <see cref="ISubscriber{T}"/>s subscribed dynamically
    /// at various points in time.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The type of element signaled.</typeparam>
    public interface IPublisher<out T>
    {
        /// <summary>
        /// <para>
        /// Request <see cref="IPublisher{T}"/> to start streaming data.
        /// </para>
        /// <para>
        /// This is a "factory method" and can be called multiple times, each time starting a new
        /// <see cref="ISubscription"/>.
        /// </para>
        /// <para>
        /// Each <see cref="ISubscription"/> will work for only a single <see cref="ISubscriber{T}"/>.
        /// </para>
        /// <para>
        /// A <see cref="ISubscriber{T}"/> should only subscribe once to a single
        /// <see cref="IPublisher{T}"/>.
        /// </para>
        /// <para>
        /// If the <see cref="IPublisher{T}"/> rejects the subscription attempt or otherwise fails
        /// it will signal the error via <see cref="ISubscriber{T}.OnError"/>.
        /// </para>
        /// </summary>
        /// <param name="s">The <see cref="ISubscriber{T}"/> that will consume signals
        /// from this <see cref="IPublisher{T}"/></param>
        void Subscribe(ISubscriber<T> s);
    }

    /// <summary>
    /// <para>
    /// Will receive call to <see cref="ISubscriber{T}.OnSubscribe"/> once after passing an instance of
    /// <see cref="ISubscriber{T}"/> to <see cref="IPublisher{T}.Subscribe"/>.
    /// </para>
    /// <para>
    /// No further notifications will be received until <see cref="ISubscription.Request"/> is called.
    /// </para>
    /// <para>After signaling demand:</para>
    /// <para>1. One or more invocations of <see cref="OnNext"/> up to the maximum number defined by
    /// <see cref="ISubscription.Request"/></para>
    /// <para>2. Single invocation of <see cref="ISubscriber{T}.OnError"/> or
    /// <see cref="ISubscriber{T}.OnComplete"/> which signals a terminal state after which no further
    /// events will be sent.</para>
    /// <para>
    /// Demand can be signaled via <see cref="ISubscription.Request"/> whenever the
    /// <see cref="ISubscriber{T}"/> instance is capable of handling more.</para>
    /// </summary>
    /// <typeparam name="T">The type of element signaled.</typeparam>
    public interface ISubscriber<in T>
    {
        /// <summary>
        /// <para>
        /// Invoked after calling <see cref="IPublisher{T}.Subscribe"/>.
        /// </para>
        /// <para>
        /// No data will start flowing until <see cref="ISubscription.Request"/> is invoked.
        /// </para>
        /// <para>
        /// It is the responsibility of this <see cref="ISubscriber{T}"/> instance to call
        /// <see cref="ISubscription.Request"/> whenever more data is wanted.
        /// </para>
        /// <para>
        /// The <see cref="IPublisher{T}"/> will send notifications only in response to
        /// <see cref="ISubscription.Request"/>.
        /// </para>
        /// </summary>
        /// <param name="s"><see cref="ISubscription"/> that allows requesting data
        /// via <see cref="ISubscription.Request"/></param>
        void OnSubscribe(ISubscription s);

        /// <summary>
        /// Data notification sent by the <see cref="IPublisher{T}"/> in response to requests to
        /// <see cref="ISubscription.Request"/>.
        /// </summary>
        /// <param name="t">The element signaled</param>
        void OnNext(T t);

        /// <summary>
        /// <para>
        /// Failed terminal state.
        /// </para>
        /// <para>
        /// No further events will be sent even if <see cref="ISubscription.Request"/> is
        /// invoked again.
        /// </para>
        /// </summary>
        /// <param name="e">The exception signaled</param>
        void OnError(Exception e);

        /// <summary>
        /// <para>
        /// Successful terminal state.
        /// </para>
        /// <para>
        /// No further events will be sent even if <see cref="ISubscription.Request"/> is
        /// invoked again.
        /// </para>
        /// </summary>
        void OnComplete();
    }

    /// <summary>
    /// <para>
    /// A <see cref="ISubscription"/> represents a one-to-one lifecycle of a <see cref="ISubscriber{T}"/>
    /// subscribing to a <see cref="ISubscriber{T}"/>.
    /// </para>
    /// <para>
    /// It can only be used once by a single <see cref="IPublisher{T}"/>.
    /// </para>
    /// <para>
    /// It is used to both signal desire for data and cancel demand (and allow resource cleanup).
    /// </para>
    /// </summary>
    public interface ISubscription
    {
        /// <summary>
        /// <para>
        /// No events will be sent by a <see cref="IPublisher{T}"/> until demand is signaled via this method.
        /// </para>
        /// <para>
        /// It can be called however often and whenever needed—but the outstanding cumulative demand
        /// must never exceed <see cref="long.MaxValue"/>.
        /// An outstanding cumulative demand of <see cref="long.MaxValue"/> may be treated by the
        /// <see cref="IPublisher{T}"/> as "effectively unbounded".
        /// </para>
        /// <para>
        /// Whatever has been requested can be sent by the <see cref="IPublisher{T}"/> so only signal demand
        /// for what can be safely handled.
        /// </para>
        /// <para>
        /// A <see cref="IPublisher{T}"/> can send less than is requested if the stream ends but
        /// then must emit either <see cref="ISubscriber{T}.OnError"/> or <see cref="ISubscriber{T}.OnComplete"/>.
        /// </para>
        /// </summary>
        /// <param name="n">The strictly positive number of elements to requests to the upstream
        /// <see cref="IPublisher{T}"/></param>
        void Request(long n);

        /// <summary>
        /// <para>
        /// Request the <see cref="IPublisher{T}"/> to stop sending data and clean up resources.
        /// </para>
        /// <para>
        /// Data may still be sent to meet previously signalled demand after calling cancel as this request is asynchronous.
        /// </para>
        /// </summary>
        void Cancel();
    }

    /// <summary>
    /// A Processor represents a processing stage—which is both a <see cref="ISubscriber{T}"/>
    /// and a <see cref="IPublisher{T}"/> and obeys the contracts of both.
    /// </summary>
    /// <typeparam name="T">The type of element signaled to the <see cref="ISubscriber{T}"/></typeparam>
    /// <typeparam name="R">The type of element signaled to the <see cref="IPublisher{T}"/></typeparam>
    public interface IProcessor<in T, out R> : ISubscriber<T>, IPublisher<R>
    {

    }
    */
    /// <summary>
    /// A Processor represents a processing stage—which is both a <see cref="ISubscriber{T}"/>
    /// and a <see cref="IPublisher{T}"/> and obeys the contracts of both.
    /// </summary>
    /// <typeparam name="T">The type of element signaled to the <see cref="ISubscriber{T}"/> and to the <see cref="IPublisher{T}"/> side</typeparam>
    public interface IProcessor<T> : IProcessor<T, T>
    {

    }
}
