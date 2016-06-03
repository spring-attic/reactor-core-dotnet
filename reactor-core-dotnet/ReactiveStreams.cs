using System;

namespace Reactive.Streams
{
    public interface IPublisher<out T>
    {
        void Subscribe(ISubscriber<T> s);
    }

    public interface ISubscriber<in T>
    {
        void OnSubscribe(ISubscription s);

        void OnNext(T t);

        void OnError(Exception e);

        void OnComplete();
    }

    public interface ISubscription
    {
        void Request(long n);

        void Cancel();
    }

    public interface IProcessor<in T, out R> : ISubscriber<T>, IPublisher<R>
    {

    }
}
