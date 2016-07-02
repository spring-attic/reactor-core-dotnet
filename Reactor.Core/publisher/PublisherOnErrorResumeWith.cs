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
    sealed class PublisherOnErrorResumeWith<T> : IFlux<T>
    {
        readonly IPublisher<T> source;

        readonly Func<Exception, bool> predicate;

        readonly Func<Exception, IPublisher<T>> publisherFactory;

        internal PublisherOnErrorResumeWith(IPublisher<T> source,
            Func<Exception, bool> predicate, Func<Exception, IPublisher<T>> publisherFactory)
        {
            this.source = source;
            this.predicate = predicate;
            this.publisherFactory = publisherFactory;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>) {
                var parent = new BaseOnErrorResumeWithConditionalSubscriber((IConditionalSubscriber<T>)s, predicate, publisherFactory);
                s.OnSubscribe(parent);
                source.Subscribe(parent);
            }
            else
            {
                var parent = new BaseOnErrorResumeWithSubscriber(s, predicate, publisherFactory);
                s.OnSubscribe(parent);
                source.Subscribe(parent);
            }
        }

        sealed class BaseOnErrorResumeWithSubscriber : ISubscriber<T>, ISubscription
        {
            readonly ISubscriber<T> actual;

            readonly Func<Exception, bool> predicate;

            readonly Func<Exception, IPublisher<T>> publisherFactory;

            SubscriptionArbiterStruct arbiter;

            long produced;

            internal BaseOnErrorResumeWithSubscriber(ISubscriber<T> actual,
                Func<Exception, bool> predicate, Func<Exception, IPublisher<T>> publisherFactory)
            {
                this.actual = actual;
                this.predicate = predicate;
                this.publisherFactory = publisherFactory;
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
                long p = produced;
                if (p != 0L)
                {
                    arbiter.Produced(p);
                }

                bool b;

                try
                {
                    b = predicate(e);
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowIfFatal(ex);
                    actual.OnError(new AggregateException(e, ex));
                    return;
                }

                if (b)
                {
                    IPublisher<T> next;

                    try
                    {
                        next = ObjectHelper.RequireNonNull(publisherFactory(e), "The publisherFactory returned a null IPublisher");
                    }
                    catch (Exception ex)
                    {
                        ExceptionHelper.ThrowIfFatal(ex);
                        actual.OnError(new AggregateException(e, ex));
                        return;
                    }

                    next.Subscribe(new ResumeWithSubscriber(this, actual));
                }
                else
                {
                    actual.OnError(e);
                }
            }

            public void OnComplete()
            {
                actual.OnComplete();
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

            sealed class ResumeWithSubscriber : ISubscriber<T>
            {
                readonly ISubscriber<T> parent;

                readonly ISubscriber<T> actual;

                internal ResumeWithSubscriber(ISubscriber<T> parent, ISubscriber<T> actual)
                {
                    this.parent = parent;
                    this.actual = actual;
                }

                public void OnComplete()
                {
                    actual.OnComplete();
                }

                public void OnError(Exception e)
                {
                    actual.OnError(e);
                }

                public void OnNext(T t)
                {
                    actual.OnNext(t);
                }

                public void OnSubscribe(ISubscription s)
                {
                    parent.OnSubscribe(s);
                }
            }
        }

        sealed class BaseOnErrorResumeWithConditionalSubscriber : IConditionalSubscriber<T>, ISubscription
        {
            readonly IConditionalSubscriber<T> actual;

            readonly Func<Exception, bool> predicate;

            readonly Func<Exception, IPublisher<T>> publisherFactory;

            SubscriptionArbiterStruct arbiter;

            long produced;

            internal BaseOnErrorResumeWithConditionalSubscriber(IConditionalSubscriber<T> actual,
                Func<Exception, bool> predicate, Func<Exception, IPublisher<T>> publisherFactory)
            {
                this.actual = actual;
                this.predicate = predicate;
                this.publisherFactory = publisherFactory;
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
                long p = produced;
                if (p != 0L)
                {
                    arbiter.Produced(p);
                }

                bool b;

                try
                {
                    b = predicate(e);
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowIfFatal(ex);
                    actual.OnError(new AggregateException(e, ex));
                    return;
                }

                if (b)
                {
                    IPublisher<T> next;

                    try
                    {
                        next = ObjectHelper.RequireNonNull(publisherFactory(e), "The publisherFactory returned a null IPublisher");
                    }
                    catch (Exception ex)
                    {
                        ExceptionHelper.ThrowIfFatal(ex);
                        actual.OnError(new AggregateException(e, ex));
                        return;
                    }

                    next.Subscribe(new ResumeWithConditionalSubscriber(this, actual));
                }
                else
                {
                    actual.OnError(e);
                }
            }

            public void OnComplete()
            {
                actual.OnComplete();
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

            sealed class ResumeWithConditionalSubscriber : IConditionalSubscriber<T>
            {
                readonly ISubscriber<T> parent;

                readonly IConditionalSubscriber<T> actual;

                internal ResumeWithConditionalSubscriber(ISubscriber<T> parent, IConditionalSubscriber<T> actual)
                {
                    this.parent = parent;
                    this.actual = actual;
                }

                public void OnComplete()
                {
                    actual.OnComplete();
                }

                public void OnError(Exception e)
                {
                    actual.OnError(e);
                }

                public void OnNext(T t)
                {
                    actual.OnNext(t);
                }

                public void OnSubscribe(ISubscription s)
                {
                    parent.OnSubscribe(s);
                }

                public bool TryOnNext(T t)
                {
                    return actual.TryOnNext(t);
                }
            }
        }
    }
}
