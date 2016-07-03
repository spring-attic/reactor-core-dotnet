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
    sealed class PublisherTakeWhile<T> : IFlux<T>
    {
        readonly IPublisher<T> source;

        readonly Func<T, bool> predicate;

        internal PublisherTakeWhile(IPublisher<T> source, Func<T, bool> predicate)
        {
            this.source = source;
            this.predicate = predicate;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                source.Subscribe(new TakeWhileConditionalSubscriber((IConditionalSubscriber<T>)s, predicate));
            }
            else
            {
                source.Subscribe(new TakeWhileSubscriber(s, predicate));
            }
        }

        sealed class TakeWhileSubscriber : BasicFuseableSubscriber<T, T>
        {
            readonly Func<T, bool> predicate;

            public TakeWhileSubscriber(ISubscriber<T> actual, Func<T, bool> predicate) : base(actual)
            {
                this.predicate = predicate;
            }

            public override void OnComplete()
            {
                Complete();
            }

            public override void OnError(Exception e)
            {
                Error(e);
            }

            public override void OnNext(T t)
            {
                if (done)
                {
                    return;
                }

                if (fusionMode != FuseableHelper.NONE)
                {
                    actual.OnNext(t);
                    return;
                }

                bool b;

                try
                {
                    b = predicate(t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return;
                }

                if (b)
                {
                    actual.OnNext(t);
                }
                else
                {
                    s.Cancel();
                    Complete();
                }
            }

            public override bool Poll(out T value)
            {
                T t;

                if (qs.Poll(out t))
                {
                    if (predicate(t))
                    {
                        value = t;
                        return true;
                    }
                    if (fusionMode == FuseableHelper.ASYNC)
                    {
                        actual.OnComplete();
                    }
                }
                value = default(T);
                return false;
            }

            public override int RequestFusion(int mode)
            {
                return TransitiveBoundaryFusion(mode);
            }
        }

        sealed class TakeWhileConditionalSubscriber : BasicFuseableConditionalSubscriber<T, T>
        {
            readonly Func<T, bool> predicate;

            public TakeWhileConditionalSubscriber(IConditionalSubscriber<T> actual, Func<T, bool> predicate) : base(actual)
            {
                this.predicate = predicate;
            }

            public override void OnComplete()
            {
                Complete();
            }

            public override void OnError(Exception e)
            {
                Error(e);
            }

            public override void OnNext(T t)
            {
                if (done)
                {
                    return;
                }

                if (fusionMode != FuseableHelper.NONE)
                {
                    actual.OnNext(t);
                    return;
                }

                bool b;

                try
                {
                    b = predicate(t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return;
                }

                if (b)
                {
                    actual.OnNext(t);
                }
                else
                {
                    s.Cancel();
                    Complete();
                }
            }

            public override bool TryOnNext(T t)
            {
                if (done)
                {
                    return false;
                }

                if (fusionMode != FuseableHelper.NONE)
                {
                    return actual.TryOnNext(t);
                }

                bool b;

                try
                {
                    b = predicate(t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return false;
                }

                if (b)
                {
                    return actual.TryOnNext(t);
                }
                s.Cancel();
                Complete();
                return false;
            }

            public override bool Poll(out T value)
            {
                T t;

                if (qs.Poll(out t))
                {
                    if (predicate(t))
                    {
                        value = t;
                        return true;
                    }
                    if (fusionMode == FuseableHelper.ASYNC)
                    {
                        actual.OnComplete();
                    }
                }
                value = default(T);
                return false;
            }

            public override int RequestFusion(int mode)
            {
                return TransitiveBoundaryFusion(mode);
            }
        }
    }
}
