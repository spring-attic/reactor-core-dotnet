using System;

namespace Reactive.Streams
{
    public interface Publisher<out T>
    {
        void Subscribe(Subscriber<T> s);
    }

    public interface Subscriber<in T>
    {
        void OnSubscribe(Subscription s);

        void OnNext(T t);

        void OnError(Exception e);

        void OnComplete();
    }

    public interface Subscription
    {
        void Request(long n);

        void Cancel();
    }

    public interface Processor<in T, out R> : Subscriber<T>, Publisher<R>
    {

    }
}
