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
using System.Runtime.InteropServices;

namespace Reactor.Core.parallel
{
    sealed class ParallelSortedJoin<T> : IFlux<T>
    {
        readonly IParallelFlux<IList<T>> source;

        readonly IComparer<T> comparer;

        internal ParallelSortedJoin(IParallelFlux<IList<T>> source, IComparer<T> comparer)
        {
            this.source = source;
            this.comparer = comparer;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            var parent = new SortedJoinSubscription(s, source.Parallelism, comparer);
            s.OnSubscribe(parent);
            source.Subscribe(parent.subscribers);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class SortedJoinSubscription : ISubscription
        {
            readonly ISubscriber<T> actual;

            internal readonly InnerSubscriber[] subscribers;

            readonly IList<T>[] values;

            readonly int[] indexes;

            readonly IComparer<T> comparer;

            int remaining;

            long requested;

            bool cancelled;

            bool done;
            Exception error;

            Pad128 p0;

            int wip;

            Pad120 p1;

            long emitted;

            Pad120 p2;

            internal SortedJoinSubscription(ISubscriber<T> actual, int n, IComparer<T> comparer)
            {
                this.actual = actual;
                this.values = new IList<T>[n];
                this.indexes = new int[n];
                var a = new InnerSubscriber[n];
                for (int i = 0; i < n; i++)
                {
                    a[i] = new InnerSubscriber(this, i);
                }
                this.subscribers = a;
                this.remaining = n;
                this.comparer = comparer;
            }

            public void Cancel()
            {
                Volatile.Write(ref done, true);
                CancelAll();

                if (QueueDrainHelper.Enter(ref wip))
                {
                    Clear();
                }
            }

            void Clear()
            {
                var vs = values;
                for (int i = 0; i < vs.Length; i++)
                {
                    vs[i] = null;
                }
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                    Drain();
                }
            }

            internal void InnerNext(int index, IList<T> value)
            {
                values[index] = value;
            }

            internal void InnerError(Exception ex)
            {
                if (ExceptionHelper.AddError(ref error, ex))
                {
                    ex = ExceptionHelper.Terminate(ref error);
                    if (!ExceptionHelper.IsTerminated(ex))
                    {
                        actual.OnError(ex);

                        if (QueueDrainHelper.Enter(ref wip))
                        {
                            Clear();
                        }
                    }
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(ex);
                }
            }

            internal void InnerComplete()
            {
                if (Interlocked.Decrement(ref remaining) == 0)
                {
                    Drain();
                }
            }

            void CancelAll()
            {
                foreach (var inner in subscribers)
                {
                    inner.Cancel();
                }
            }

            void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                int missed = 1;
                var vs = values;
                var ix = indexes;
                int n = vs.Length;
                var a = actual;
                long e = emitted;
                
                for (;;)
                {
                    if (Volatile.Read(ref remaining) == 0)
                    {
                        for (;;)
                        {
                            if (Volatile.Read(ref cancelled))
                            {
                                Clear();
                                return;
                            }

                            long r = Volatile.Read(ref requested);

                            int finished = 0;

                            T min = default(T);
                            int minIndex = -1;

                            for (int i = 0; i < n; i++)
                            {
                                var list = vs[i];
                                int idx = ix[i];

                                if (idx == list.Count)
                                {
                                    finished++;
                                }
                                else
                                {
                                    var t = list[idx];
                                    if (minIndex == -1 || comparer.Compare(min, t) > 0)
                                    {
                                        min = t;
                                        minIndex = i;
                                    }
                                }
                            }

                            if (finished == n)
                            {
                                a.OnComplete();
                                return;
                            }

                            if (r == e || minIndex < 0)
                            {
                                break;
                            }

                            a.OnNext(min);

                            e++;
                            ix[minIndex]++;
                        }
                    } else
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Clear();
                            return;
                        }
                    }

                    emitted = e;
                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }
        }

        sealed class InnerSubscriber : ISubscriber<IList<T>>
        {
            readonly SortedJoinSubscription parent;

            readonly int index;

            ISubscription s;

            internal InnerSubscriber(SortedJoinSubscription parent, int index)
            {
                this.parent = parent;
                this.index = index;
            }

            public void OnComplete()
            {
                parent.InnerComplete();
            }

            public void OnError(Exception e)
            {
                parent.InnerError(e);
            }

            public void OnNext(IList<T> t)
            {
                parent.InnerNext(index, t);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    s.Request(long.MaxValue);
                }
            }

            internal void Cancel()
            {
                SubscriptionHelper.Cancel(ref s);
            }
        }
    }
}
