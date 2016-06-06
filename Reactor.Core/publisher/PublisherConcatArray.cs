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
    sealed class PublisherConcatArray<T> : IFlux<T>
    {
        readonly IPublisher<T>[] sources;

        readonly bool delayError;

        public PublisherConcatArray(IPublisher<T>[] sources, bool delayError)
        {
            this.sources = sources;
            this.delayError = delayError;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                ConcatConditionalSubscriber parent = new ConcatConditionalSubscriber((IConditionalSubscriber<T>)s, sources, delayError);
                s.OnSubscribe(parent);

                parent.Drain();
            }
            else
            {
                ConcatSubscriber parent = new ConcatSubscriber(s, sources, delayError);
                s.OnSubscribe(parent);

                parent.Drain();
            }
        }

        internal PublisherConcatArray<T> StartWith(IPublisher<T> other, bool delayError)
        {
            if (this.delayError == delayError)
            {
                IPublisher<T>[] a = MultiSourceHelper.AppendFirst(this.sources, other);
                return new PublisherConcatArray<T>(a, this.delayError);
            }

            return new PublisherConcatArray<T>(new IPublisher<T>[] { other, this }, delayError);
        }

        internal PublisherConcatArray<T> EndWith(IPublisher<T> other, bool delayError)
        {
            if (this.delayError == delayError)
            {
                IPublisher<T>[] a = MultiSourceHelper.AppendLast(this.sources, other);
                return new PublisherConcatArray<T>(a, this.delayError);
            }

            return new PublisherConcatArray<T>(new IPublisher<T>[] { this, other }, delayError);
        }

        sealed class ConcatSubscriber : ISubscriber<T>, ISubscription
        {
            readonly ISubscriber<T> actual;

            readonly IPublisher<T>[] sources;

            readonly bool delayErrors;

            int index;

            SubscriptionArbiterStruct arbiter;

            long produced;

            Exception error;

            bool active;

            int wip;

            internal ConcatSubscriber(ISubscriber<T> actual, IPublisher<T>[] sources, bool delayErrors)
            {
                this.actual = actual;
                this.sources = sources;
                this.delayErrors = delayErrors;
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    arbiter.Request(n);
                }
            }

            public void Cancel()
            {
                arbiter.Cancel();
            }

            public void OnSubscribe(ISubscription s)
            {
                arbiter.Set(s);
            }

            public void OnNext(T t)
            {
                produced++;
                actual.OnNext(t);
            }

            public void OnError(Exception e)
            {
                if (!delayErrors)
                {
                    actual.OnError(e);
                    return;
                }

                var ex = error;
                if (ex == null)
                {
                    error = e;
                }
                else
                {
                    error = new AggregateException(ex, e);
                }
                Volatile.Write(ref active, false);
                Drain();
            }

            public void OnComplete()
            {
                Volatile.Write(ref active, false);
                Drain();
            }

            internal void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                var a = sources;

                for (;;)
                {
                    if (!Volatile.Read(ref active))
                    {
                        int i = index;

                        if (i == a.Length)
                        {
                            Exception ex = error;
                            if (ex != null)
                            {
                                actual.OnError(ex);
                            }
                            else
                            {
                                actual.OnComplete();
                            }
                            return;
                        }

                        long p = produced;
                        if (p != 0L)
                        {
                            produced = 0L;
                            arbiter.Produced(p);
                        }

                        IPublisher<T> next = a[i];

                        index = i + 1;
                        Volatile.Write(ref active, true);

                        next.Subscribe(this);
                    }


                    if (Interlocked.Decrement(ref wip) == 0)
                    {
                        break;
                    }
                }
            }
        }

        sealed class ConcatConditionalSubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IConditionalSubscriber<T> actual;

            readonly IPublisher<T>[] sources;

            readonly bool delayErrors;

            int index;

            SubscriptionArbiterStruct arbiter;

            long produced;

            Exception error;

            bool active;

            int wip;

            internal ConcatConditionalSubscriber(IConditionalSubscriber<T> actual, IPublisher<T>[] sources, bool delayErrors)
            {
                this.actual = actual;
                this.sources = sources;
                this.delayErrors = delayErrors;
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    arbiter.Request(n);
                }
            }

            public void Cancel()
            {
                arbiter.Cancel();
            }

            public void OnSubscribe(ISubscription s)
            {
                arbiter.Set(s);
            }

            public void OnNext(T t)
            {
                produced++;
                actual.OnNext(t);
            }

            public bool TryOnNext(T t)
            {
                
                if (actual.TryOnNext(t))
                {
                    produced++;
                    return true;
                }
                return false;
            }

            public void OnError(Exception e)
            {
                if (!delayErrors)
                {
                    actual.OnError(e);
                    return;
                }

                var ex = error;
                if (ex == null)
                {
                    error = e;
                }
                else
                {
                    error = new AggregateException(ex, e);
                }
                Volatile.Write(ref active, false);
                Drain();
            }

            public void OnComplete()
            {
                Volatile.Write(ref active, false);
                Drain();
            }

            internal void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                var a = sources;

                for (;;)
                {
                    if (!Volatile.Read(ref active))
                    {
                        int i = index;

                        if (i == a.Length)
                        {
                            Exception ex = error;
                            if (ex != null)
                            {
                                actual.OnError(ex);
                            }
                            else
                            {
                                actual.OnComplete();
                            }
                            return;
                        }

                        long p = produced;
                        if (p != 0L)
                        {
                            produced = 0L;
                            arbiter.Produced(p);
                        }

                        IPublisher<T> next = a[i];

                        index = i + 1;
                        Volatile.Write(ref active, true);

                        next.Subscribe(this);
                    }


                    if (Interlocked.Decrement(ref wip) == 0)
                    {
                        break;
                    }
                }
            }
        }
    }
}
