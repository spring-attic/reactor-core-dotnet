using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core;
using System.Threading;
using Reactor.Core.flow;
using Reactor.Core.subscriber;
using Reactor.Core.subscription;
using Reactor.Core.util;

namespace Reactor.Core.publisher
{
    sealed class PublisherProcess<T, U> : IConnectableFlux<U>
    {
        readonly IPublisher<T> source;

        readonly Func<IProcessor<T, T>> processorSupplier;

        readonly Func<IFlux<T>, IPublisher<U>> selector;

        Connection connection;

        internal PublisherProcess(IPublisher<T> source,
            Func<IProcessor<T, T>> processorSupplier,
            Func<IFlux<T>, IPublisher<U>> selector)
        {
            this.source = source;
            this.processorSupplier = processorSupplier;
            this.selector = selector;
        }

        public IDisposable Connect(Action<IDisposable> onConnect = null)
        {
            for (;;)
            {
                var conn = Volatile.Read(ref connection);
                if (conn == null)
                {
                    conn = new Connection();
                    if (Interlocked.CompareExchange(ref connection, conn, null) != null)
                    {
                        continue;
                    }
                }

                if (conn.TryConnect(source, onConnect, processorSupplier, selector))
                {
                    return conn;
                }

                // clear terminated connection and retry
                Interlocked.CompareExchange(ref connection, null, conn);
            }
        }

        public void Subscribe(ISubscriber<U> s)
        {
            for (;;)
            {
                var conn = Volatile.Read(ref connection);
                if (conn == null)
                {
                    conn = new Connection();
                    if (Interlocked.CompareExchange(ref connection, conn, null) != null)
                    {
                        continue;
                    }
                }

                if (conn.Subscribe(s))
                {
                    break;
                }

                // clear terminated connection and retry
                Interlocked.CompareExchange(ref connection, null, conn);
            }
        }

        sealed class Connection : IDisposable, ISubscriber<T>, ISubscription
        {
            TrackingArray<ISubscriber<U>> subscribers;

            IProcessor<T, T> processor;

            IPublisher<U> result;

            ISubscription s;

            int once;

            int done;

            internal Connection()
            {
                subscribers.Init();
            }

            internal bool TryConnect(
                IPublisher<T> source,
                Action<IDisposable> action, 
                Func<IProcessor<T, T>> processorSupplier,
                Func<IFlux<T>, IPublisher<U>> selector)
            {
                if (Volatile.Read(ref done) != 0)
                {
                    return false;
                }
                if (Interlocked.CompareExchange(ref once, 1, 0) == 0) {
                    try
                    {
                        processor = processorSupplier();

                        result = selector(Flux.Wrap(processor));
                    }
                    catch (Exception ex)
                    {
                        ExceptionHelper.ThrowIfFatal(ex);
                        Volatile.Write(ref done, 1);
                        foreach (var s in subscribers.Terminate())
                        {
                            EmptySubscription<U>.Error(s, ex);
                        }
                        return false;
                    }

                    foreach (var s in subscribers.Terminate())
                    {
                        result.Subscribe(s);
                    }

                    action?.Invoke(this);

                    source.Subscribe(this);
                }
                return true;
            }

            internal bool Subscribe(ISubscriber<U> s)
            {
                if (Volatile.Read(ref done) != 0)
                {
                    return false;
                }

                if (!subscribers.Add(s))
                {
                    result.Subscribe(s);
                }

                return true;
            }

            public void Dispose()
            {
                SubscriptionHelper.Cancel(ref s);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    processor.OnSubscribe(this);
                }
            }

            public void OnNext(T t)
            {
                processor.OnNext(t);
            }

            public void OnError(Exception e)
            {
                processor.OnError(e);
                Interlocked.Exchange(ref done, 1);
            }

            public void OnComplete()
            {
                processor.OnComplete();
                Interlocked.Exchange(ref done, 1);
            }

            public void Request(long n)
            {
                s.Request(n);
            }

            public void Cancel()
            {
                Dispose();
            }
        }
    }
}
