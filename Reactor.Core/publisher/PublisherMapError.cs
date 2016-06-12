using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core.flow;
using Reactor.Core.subscriber;
using Reactor.Core.subscription;
using Reactor.Core.util;
using System.Threading;

namespace Reactor.Core.publisher
{
    sealed class PublisherMapError<T> : IFlux<T>, IMono<T>
    {
        readonly IPublisher<T> source;

        readonly Func<Exception, Exception> errorMapper;

        internal PublisherMapError(IPublisher<T> source, Func<Exception, Exception> errorMapper)
        {
            this.source = source;
            this.errorMapper = errorMapper;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                source.Subscribe(new MapErrorConditionalSubscriber((IConditionalSubscriber<T>)s, errorMapper));
            }
            else
            {
                source.Subscribe(new MapErrorSubscriber(s, errorMapper));
            }
        }

        sealed class MapErrorSubscriber : BasicFuseableSubscriber<T, T>
        {
            readonly Func<Exception, Exception> errorMapper;

            public MapErrorSubscriber(ISubscriber<T> actual, Func<Exception, Exception> errorMapper) : base(actual)
            {
                this.errorMapper = errorMapper;
            }

            public override void OnComplete()
            {
                Complete();
            }

            public override void OnError(Exception e)
            {
                Exception ex;
                try
                {
                    ex = errorMapper(e);
                }
                catch (Exception exc)
                {
                    Fail(exc);
                    return;
                }
                Error(ex);
            }

            public override void OnNext(T t)
            {
                actual.OnNext(t);
            }

            public override bool Poll(out T value)
            {
                try
                {
                    return qs.Poll(out value);
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowIfFatal(ex);

                    Exception exc;
                    try
                    {
                        exc = errorMapper(ex);
                    }
                    catch (Exception exc2)
                    {
                        ExceptionHelper.ThrowIfFatal(exc2);
                        throw exc2;
                    }

                    throw exc;
                }
            }

            public override int RequestFusion(int mode)
            {
                return TransitiveAnyFusion(mode);
            }
        }

        sealed class MapErrorConditionalSubscriber : BasicFuseableConditionalSubscriber<T, T>
        {
            readonly Func<Exception, Exception> errorMapper;

            public MapErrorConditionalSubscriber(IConditionalSubscriber<T> actual, Func<Exception, Exception> errorMapper) : base(actual)
            {
                this.errorMapper = errorMapper;
            }

            public override void OnComplete()
            {
                Complete();
            }

            public override void OnError(Exception e)
            {
                Exception ex;
                try
                {
                    ex = errorMapper(e);
                }
                catch (Exception exc)
                {
                    Fail(exc);
                    return;
                }
                Error(ex);
            }

            public override void OnNext(T t)
            {
                actual.OnNext(t);
            }

            public override bool TryOnNext(T t)
            {
                return actual.TryOnNext(t);
            }

            public override bool Poll(out T value)
            {
                try
                {
                    return qs.Poll(out value);
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowIfFatal(ex);

                    Exception exc;
                    try
                    {
                        exc = errorMapper(ex);
                    }
                    catch (Exception exc2)
                    {
                        ExceptionHelper.ThrowIfFatal(exc2);
                        throw exc2;
                    }

                    throw exc;
                }
            }

            public override int RequestFusion(int mode)
            {
                return TransitiveAnyFusion(mode);
            }
        }

    }
}
