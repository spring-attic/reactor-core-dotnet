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
    sealed class PublisherTakeUntilPredicate<T> : IFlux<T>
    {
        readonly IPublisher<T> source;

        readonly Func<T, bool> predicate;

        internal PublisherTakeUntilPredicate(IPublisher<T> source, Func<T, bool> predicate)
        {
            this.source = source;
            this.predicate = predicate;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                source.Subscribe(new TakeUntilPredicateConditionalSubscriber((IConditionalSubscriber<T>)s, predicate));
            }
            else
            {
                source.Subscribe(new TakeUntilPredicateSubscriber(s, predicate));
            }
        }

        sealed class TakeUntilPredicateSubscriber : BasicFuseableSubscriber<T, T>
        {
            readonly Func<T, bool> predicate;

            public TakeUntilPredicateSubscriber(ISubscriber<T> actual, Func<T, bool> predicate) : base(actual)
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

                actual.OnNext(t);

                if (fusionMode != FuseableHelper.NONE)
                {
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
                    s.Cancel();
                    Complete();
                }
            }

            public override bool Poll(out T value)
            {
                if (done)
                {
                    if (fusionMode == FuseableHelper.ASYNC)
                    {
                        actual.OnComplete();
                    }
                    value = default(T);
                    return false;
                }

                T t;

                if (qs.Poll(out t))
                {
                    bool d = predicate(t);
                    done = d;
                    value = t;
                    return true;
                }
                value = default(T);
                return false;
            }

            public override int RequestFusion(int mode)
            {
                return TransitiveBoundaryFusion(mode);
            }
        }

        sealed class TakeUntilPredicateConditionalSubscriber : BasicFuseableConditionalSubscriber<T, T>
        {
            readonly Func<T, bool> predicate;

            public TakeUntilPredicateConditionalSubscriber(IConditionalSubscriber<T> actual, Func<T, bool> predicate) : base(actual)
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

                actual.OnNext(t);

                if (fusionMode != FuseableHelper.NONE)
                {
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

                bool c = actual.TryOnNext(t);

                if (fusionMode != FuseableHelper.NONE)
                {
                    return c;
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
                    s.Cancel();
                    Complete();
                    return false;
                }

                return c;
            }

            public override bool Poll(out T value)
            {
                if (done)
                {
                    if (fusionMode == FuseableHelper.ASYNC)
                    {
                        actual.OnComplete();
                    }
                    value = default(T);
                    return false;
                }

                T t;

                if (qs.Poll(out t))
                {
                    bool d = predicate(t);
                    done = d;
                    value = t;
                    return true;
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
