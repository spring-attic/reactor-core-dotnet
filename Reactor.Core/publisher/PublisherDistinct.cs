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
    sealed class PublisherDistinct<T, K> : IFlux<T>, IFuseable
    {
        readonly IPublisher<T> source;

        readonly Func<T, K> keySelector;

        readonly IEqualityComparer<K> comparer;

        internal PublisherDistinct(IPublisher<T> source, Func<T, K> keySelector, IEqualityComparer<K> comparer)
        {
            this.source = source;
            this.keySelector = keySelector;
            this.comparer = comparer;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                source.Subscribe(new DistinctConditionalSubscriber((IConditionalSubscriber<T>)s, keySelector, comparer));
            }
            else
            {
                source.Subscribe(new DistinctSubscriber(s, keySelector, comparer));
            }
        }

        sealed class DistinctSubscriber : BasicFuseableSubscriber<T, T>, IConditionalSubscriber<T>
        {
            readonly Func<T, K> keySelector;

            readonly HashSet<K> set;

            public DistinctSubscriber(ISubscriber<T> actual, Func<T, K> keySelector, IEqualityComparer<K> comparer) : base(actual)
            {
                this.keySelector = keySelector;
                this.set = new HashSet<K>(comparer);
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
                if (fusionMode != FuseableHelper.NONE)
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
                K k;
                
                k = keySelector(t);

                if (set.Contains(k))
                {
                    return false;
                }

                set.Add(k);
                actual.OnNext(t);
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
                            if (set.Contains(k))
                            {
                                continue;
                            }
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
                            if (set.Contains(k))
                            {
                                p++;
                                continue;
                            }
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

        sealed class DistinctConditionalSubscriber : BasicFuseableConditionalSubscriber<T, T>
        {
            readonly Func<T, K> keySelector;

            readonly HashSet<K> set;

            public DistinctConditionalSubscriber(IConditionalSubscriber<T> actual, Func<T, K> keySelector, IEqualityComparer<K> comparer) : base(actual)
            {
                this.keySelector = keySelector;
                this.set = new HashSet<K>(comparer);
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
                if (fusionMode != FuseableHelper.NONE)
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
                K k;

                k = keySelector(t);

                if (set.Contains(k))
                {
                    return false;
                }

                set.Add(k);
                return actual.TryOnNext(t);
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
                            if (set.Contains(k))
                            {
                                continue;
                            }
                            set.Add(k);
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
                            if (set.Contains(k))
                            {
                                p++;
                                continue;
                            }
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
