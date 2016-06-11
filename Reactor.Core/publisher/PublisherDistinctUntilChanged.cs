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
    sealed class PublisherDistinctUntilChanged<T, K> : IFlux<T>
    {
        readonly IPublisher<T> source;

        readonly Func<T, K> keySelector;

        readonly IEqualityComparer<K> comparer;

        internal PublisherDistinctUntilChanged(IPublisher<T> source, Func<T, K> keySelector, IEqualityComparer<K> comparer)
        {
            this.source = source;
            this.keySelector = keySelector;
            this.comparer = comparer;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                source.Subscribe(new DistinctUntilChangedConditionalSubscriber((IConditionalSubscriber<T>)s, keySelector, comparer));
            }
            else
            {
                source.Subscribe(new DistinctUntilChangedSubscriber(s, keySelector, comparer));
            }
        }

        sealed class DistinctUntilChangedSubscriber : BasicFuseableSubscriber<T, T>, IConditionalSubscriber<T>
        {
            readonly Func<T, K> keySelector;

            readonly IEqualityComparer<K> comparer;

            K last;

            bool nonEmpty;

            public DistinctUntilChangedSubscriber(ISubscriber<T> actual, Func<T, K> keySelector, IEqualityComparer<K> comparer) : base(actual)
            {
                this.keySelector = keySelector;
                this.comparer = comparer;
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

                if (!TryOnNext(t))
                {
                    s.Request(1);
                }
            }

            public bool TryOnNext(T t)
            {

                if (!nonEmpty)
                {
                    nonEmpty = true;
                    last = keySelector(t);
                    actual.OnNext(t);
                }
                else
                {
                    K k;
                    bool c;

                    try
                    {
                        k = keySelector(t);
                        c = comparer.Equals(last, k);
                    }
                    catch (Exception ex)
                    {
                        Fail(ex);
                        return true;
                    }

                    if (c)
                    {
                        last = k;
                        return false;
                    }
                    last = k;
                    actual.OnNext(t);
                }

                return true;
            }

            public override int RequestFusion(int mode)
            {
                var qs = this.qs;
                if (qs != null)
                {
                    int m = qs.RequestFusion(mode);
                    if (m != FuseableHelper.NONE)
                    {
                        fusionMode = m;
                    }
                    return m;
                }
                return FuseableHelper.NONE;
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
                            K k = keySelector(local);
                            if (!nonEmpty)
                            {
                                nonEmpty = true;
                                last = k;
                                value = local;
                                return true;
                            }
                            if (comparer.Equals(last, k))
                            {
                                last = k;
                                continue;
                            }

                            last = k;
                            value = local;
                            return true;
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
                    long p = 0L;
                    for (;;)
                    {
                        if (qs.Poll(out local))
                        {
                            K k = keySelector(local);
                            if (comparer.Equals(last, k))
                            {
                                last = k;
                                p++;
                                continue;
                            }

                            last = k;
                            if (p != 0L)
                            {
                                qs.Request(p);
                            }
                            value = local;
                            return true;
                        }
                        else
                        {
                            if (p != 0L)
                            {
                                qs.Request(p);
                            }
                            value = default(T);
                            return false;
                        }
                    }
                }
            }
        }

        sealed class DistinctUntilChangedConditionalSubscriber : BasicFuseableConditionalSubscriber<T, T>
        {
            readonly Func<T, K> keySelector;

            readonly IEqualityComparer<K> comparer;

            K last;

            bool nonEmpty;

            public DistinctUntilChangedConditionalSubscriber(IConditionalSubscriber<T> actual, Func<T, K> keySelector, IEqualityComparer<K> comparer) : base(actual)
            {
                this.keySelector = keySelector;
                this.comparer = comparer;
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

                if (!TryOnNext(t))
                {
                    s.Request(1);
                }
            }

            public override bool TryOnNext(T t)
            {

                if (!nonEmpty)
                {
                    nonEmpty = true;
                    last = keySelector(t);
                    actual.OnNext(t);
                }
                else
                {
                    K k;
                    bool c;

                    try
                    {
                        k = keySelector(t);
                        c = comparer.Equals(last, k);
                    }
                    catch (Exception ex)
                    {
                        Fail(ex);
                        return true;
                    }

                    if (c)
                    {
                        last = k;
                        return false;
                    }
                    last = k;
                    return actual.TryOnNext(t);
                }

                return true;
            }

            public override int RequestFusion(int mode)
            {
                var qs = this.qs;
                if (qs != null)
                {
                    int m = qs.RequestFusion(mode);
                    if (m != FuseableHelper.NONE)
                    {
                        fusionMode = m;
                    }
                    return m;
                }
                return FuseableHelper.NONE;
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
                            K k = keySelector(local);
                            if (!nonEmpty)
                            {
                                nonEmpty = true;
                                last = k;
                                value = local;
                                return true;
                            }
                            if (comparer.Equals(last, k))
                            {
                                last = k;
                                continue;
                            }

                            last = k;
                            value = local;
                            return true;
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
                    long p = 0L;
                    for (;;)
                    {
                        if (qs.Poll(out local))
                        {
                            K k = keySelector(local);
                            if (comparer.Equals(last, k))
                            {
                                last = k;
                                p++;
                                continue;
                            }

                            last = k;
                            if (p != 0L)
                            {
                                qs.Request(p);
                            }
                            value = local;
                            return true;
                        }
                        else
                        {
                            if (p != 0L)
                            {
                                qs.Request(p);
                            }
                            value = default(T);
                            return false;
                        }
                    }
                }
            }
        }
    }
}
