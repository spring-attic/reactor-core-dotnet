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
    sealed class PublisherSkipWhile<T> : IFlux<T>
    {
        readonly IPublisher<T> source;

        readonly Func<T, bool> predicate;

        internal PublisherSkipWhile(IPublisher<T> source, Func<T, bool> predicate)
        {
            this.source = source;
            this.predicate = predicate;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                source.Subscribe(new SkipWhileConditionalSubscriber((IConditionalSubscriber<T>)s, predicate));
            }
            else
            {
                source.Subscribe(new SkipWhileSubscriber(s, predicate));
            }
        }

        sealed class SkipWhileSubscriber : BasicFuseableSubscriber<T, T>, IConditionalSubscriber<T>
        {
            readonly Func<T, bool> predicate;

            bool passThrough;

            public SkipWhileSubscriber(ISubscriber<T> actual, Func<T, bool> predicate) : base(actual)
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
                
                if (!TryOnNext(t))
                {
                    s.Request(1);
                }
            }

            public bool TryOnNext(T t)
            {
                if (done)
                {
                    return false;
                }

                if (fusionMode != FuseableHelper.NONE)
                {
                    actual.OnNext(t);
                    return true;
                }

                if (passThrough)
                {
                    actual.OnNext(t);
                    return true;
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

                if (!b)
                {
                    passThrough = true;
                    actual.OnNext(t);
                    return true;
                }
                return false;
            }

            public override bool Poll(out T value)
            {
                for (;;)
                {
                    if (passThrough)
                    {
                        return qs.Poll(out value);
                    }

                    T t;
                    bool b = qs.Poll(out t);

                    if (b)
                    {
                        if (!predicate(t))
                        {
                            passThrough = true;
                            value = t;
                            return true;
                        }
                        else
                        {
                            if (fusionMode != FuseableHelper.SYNC)
                            {
                                s.Request(1);
                            }
                        }
                    } else
                    {
                        value = default(T);
                        return false;
                    }
                }
            }

            public override int RequestFusion(int mode)
            {
                return TransitiveBoundaryFusion(mode);
            }
        }

        sealed class SkipWhileConditionalSubscriber : BasicFuseableConditionalSubscriber<T, T>
        {
            readonly Func<T, bool> predicate;

            bool passThrough;

            public SkipWhileConditionalSubscriber(IConditionalSubscriber<T> actual, Func<T, bool> predicate) : base(actual)
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
                if (!TryOnNext(t))
                {
                    s.Request(1);
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

                if (passThrough)
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

                if (!b)
                {
                    passThrough = true;
                    return actual.TryOnNext(t);
                }
                return false;
            }

            public override bool Poll(out T value)
            {
                for (;;)
                {
                    if (passThrough)
                    {
                        return qs.Poll(out value);
                    }

                    T t;
                    bool b = qs.Poll(out t);

                    if (b)
                    {
                        if (!predicate(t))
                        {
                            passThrough = true;
                            value = t;
                            return true;
                        }
                        else
                        {
                            if (fusionMode != FuseableHelper.SYNC)
                            {
                                s.Request(1);
                            }
                        }
                    }
                    else
                    {
                        value = default(T);
                        return false;
                    }
                }
            }

            public override int RequestFusion(int mode)
            {
                return TransitiveBoundaryFusion(mode);
            }
        }
    }
}
