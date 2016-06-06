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
    sealed class PublisherConcatEnumerable<T> : IFlux<T>
    {
        readonly IEnumerable<IPublisher<T>> sources;

        readonly bool delayError;

        public PublisherConcatEnumerable(IEnumerable<IPublisher<T>> sources, bool delayError)
        {
            this.sources = sources;
            this.delayError = delayError;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            IEnumerator<IPublisher<T>> enumerator;

            bool hasValue;

            try
            {
                enumerator = sources.GetEnumerator();

                hasValue = enumerator.MoveNext();
            }
            catch (Exception ex)
            {
                ExceptionHelper.ThrowIfFatal(ex);
                EmptySubscription<T>.Error(s, ex);
                return;
            }

            if (!hasValue)
            {
                EmptySubscription<T>.Complete(s);
                return;
            }

            if (s is IConditionalSubscriber<T>)
            {
                ConcatConditionalSubscriber parent = new ConcatConditionalSubscriber((IConditionalSubscriber<T>)s, enumerator, delayError);
                s.OnSubscribe(parent);

                parent.Drain();
            }
            else
            {
                ConcatSubscriber parent = new ConcatSubscriber(s, enumerator, delayError);
                s.OnSubscribe(parent);

                parent.Drain();
            }
        }

        sealed class ConcatSubscriber : ISubscriber<T>, ISubscription
        {
            readonly ISubscriber<T> actual;

            readonly IEnumerator<IPublisher<T>> enumerator;

            readonly bool delayErrors;

            int index;

            SubscriptionArbiterStruct arbiter;

            long produced;

            Exception error;

            bool active;

            int wip;

            internal ConcatSubscriber(ISubscriber<T> actual, IEnumerator<IPublisher<T>> enumerator, bool delayErrors)
            {
                this.actual = actual;
                this.enumerator = enumerator;
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

                var a = enumerator;

                for (;;)
                {
                    if (!Volatile.Read(ref active))
                    {
                        int i = index;

                        if (i != 0)
                        {

                            bool hasNext;

                            try
                            {
                                hasNext = enumerator.MoveNext();
                            }
                            catch (Exception ex)
                            {
                                ExceptionHelper.ThrowIfFatal(ex);
                                Exception exc = error;
                                if (exc != null)
                                {
                                    exc = new AggregateException(exc, ex);
                                }

                                actual.OnError(exc);
                                return;
                            }

                            if (!hasNext)
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
                        }

                        long p = produced;
                        if (p != 0L)
                        {
                            produced = 0L;
                            arbiter.Produced(p);
                        }

                        IPublisher<T> next = a.Current;

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

            readonly IEnumerator<IPublisher<T>> enumerator;

            readonly bool delayErrors;

            int index;

            SubscriptionArbiterStruct arbiter;

            long produced;

            Exception error;

            bool active;

            int wip;

            internal ConcatConditionalSubscriber(IConditionalSubscriber<T> actual, IEnumerator<IPublisher<T>> enumerator, bool delayErrors)
            {
                this.actual = actual;
                this.enumerator = enumerator;
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

                var a = enumerator;

                for (;;)
                {
                    if (!Volatile.Read(ref active))
                    {
                        int i = index;

                        if (i != 0)
                        {

                            bool hasNext;

                            try
                            {
                                hasNext = enumerator.MoveNext();
                            }
                            catch (Exception ex)
                            {
                                ExceptionHelper.ThrowIfFatal(ex);
                                Exception exc = error;
                                if (exc != null)
                                {
                                    exc = new AggregateException(exc, ex);
                                }

                                actual.OnError(exc);
                                return;
                            }

                            if (!hasNext)
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
                        }

                        long p = produced;
                        if (p != 0L)
                        {
                            produced = 0L;
                            arbiter.Produced(p);
                        }

                        IPublisher<T> next = a.Current;

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
