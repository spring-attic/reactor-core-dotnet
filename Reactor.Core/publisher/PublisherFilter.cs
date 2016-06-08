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
    sealed class PublisherFilter<T> : IFlux<T>, IMono<T>, IFuseable
    {
        readonly IPublisher<T> source;

        readonly Func<T, bool> predicate;

        internal PublisherFilter(IPublisher<T> source, Func<T, bool> predicate)
        {
            this.source = source;
            this.predicate = predicate;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                source.Subscribe(new FilterConditionalSubscriber((IConditionalSubscriber<T>)s, predicate));
            }
            else
            {
                source.Subscribe(new FilterSubscriber(s, predicate));
            }
        }

        sealed class FilterSubscriber : BasicFuseableSubscriber<T, T>, IConditionalSubscriber<T>
        {
            readonly Func<T, bool> predicate;

            internal FilterSubscriber(ISubscriber<T> actual, Func<T, bool> predicate) : base(actual)
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

                if (fusionMode == FuseableHelper.ASYNC)
                {
                    actual.OnNext(t);
                    return;
                }

                if (done)
                {
                    return;
                }

                if (!TryOnNext(t))
                {
                    s.Request(1);
                }
            }

            public bool TryOnNext(T t)
            {
                bool b;

                try
                {
                    b = predicate(t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return true;
                }


                return b;
            }

            public override bool Poll(out T value)
            {
                var qs = this.qs;
                T local;

                if (fusionMode == FuseableHelper.SYNC)
                {
                    for (;;)
                    {
                        if (qs.Poll(out local))
                        {
                            if (predicate(local))
                            {
                                value = local;
                                return true;
                            }
                        }
                        else
                        {
                            value = default(T);
                            return false;
                        }
                    }
                }
                else
                {
                    long p = 0;
                    for (;;)
                    {
                        if (qs.Poll(out local))
                        {
                            if (predicate(local))
                            {
                                if (p != 0)
                                {
                                    qs.Request(p);
                                }
                                value = local;
                                return true;
                            }
                            p++;
                        }
                        else
                        {
                            if (p != 0)
                            {
                                qs.Request(p);
                            }
                            value = default(T);
                            return false;
                        }
                    }
                }
            }

            public override int RequestFusion(int mode)
            {
                var qs = this.qs;
                if (qs != null)
                {
                    return TransitiveBoundaryFusion(mode);
                }
                return FuseableHelper.NONE;
            }
        }

        sealed class FilterConditionalSubscriber : BasicFuseableConditionalSubscriber<T, T>
        {
            readonly Func<T, bool> predicate;

            internal FilterConditionalSubscriber(IConditionalSubscriber<T> actual, Func<T, bool> predicate) : base(actual)
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

                if (fusionMode == FuseableHelper.ASYNC)
                {
                    actual.OnNext(t);
                    return;
                }

                if (done)
                {
                    return;
                }

                if (!TryOnNext(t))
                {
                    s.Request(1);
                }
            }

            public override bool TryOnNext(T t)
            {
                bool b;

                try
                {
                    b = predicate(t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return true;
                }


                return b;
            }

            public override bool Poll(out T value)
            {
                var qs = this.qs;
                T local;

                if (fusionMode == FuseableHelper.SYNC)
                {
                    for (;;)
                    {
                        if (qs.Poll(out local))
                        {
                            if (predicate(local))
                            {
                                value = local;
                                return true;
                            }
                        }
                        else
                        {
                            value = default(T);
                            return false;
                        }
                    }
                }
                else
                {
                    long p = 0;
                    for (;;)
                    {
                        if (qs.Poll(out local))
                        {
                            if (predicate(local))
                            {
                                if (p != 0)
                                {
                                    qs.Request(p);
                                }
                                value = local;
                                return true;
                            }
                            p++;
                        }
                        else
                        {
                            if (p != 0)
                            {
                                qs.Request(p);
                            }
                            value = default(T);
                            return false;
                        }
                    }
                }
            }

            public override int RequestFusion(int mode)
            {
                var qs = this.qs;
                if (qs != null)
                {
                    return TransitiveBoundaryFusion(mode);
                }
                return FuseableHelper.NONE;
            }
        }
    }
}
