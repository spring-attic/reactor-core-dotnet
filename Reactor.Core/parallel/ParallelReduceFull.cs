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

namespace Reactor.Core.parallel
{
    sealed class ParallelReduceFull<T> : IFlux<T>, IMono<T>
    {
        readonly IParallelFlux<T> source;

        readonly Func<T, T, T> reducer;

        internal ParallelReduceFull(IParallelFlux<T> source, Func<T, T, T> reducer)
        {
            this.source = source;
            this.reducer = reducer;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            int n = source.Parallelism;

            var parent = new ReduceFullCoordinator(s, n, reducer);
            s.OnSubscribe(parent);

            source.Subscribe(parent.subscribers);
        }

        sealed class ReduceFullCoordinator : DeferredScalarSubscription<T>
        {
            internal readonly InnerSubscriber[] subscribers;

            readonly Func<T, T, T> reducer;

            Exception error;

            int remaining;

            SlotPair current;

            public ReduceFullCoordinator(ISubscriber<T> actual, int n, Func<T, T, T> reducer) : base(actual)
            {
                var a = new InnerSubscriber[n];
                for (int i = 0; i < n; i++)
                {
                    a[i] = new InnerSubscriber(this, reducer);
                }
                this.subscribers = a;
                this.reducer = reducer;
                Volatile.Write(ref remaining, n);
            }

            SlotPair AddValue(T value)
            {
                for (;;)
                {
                    var c = Volatile.Read(ref current);

                    if (c == null)
                    {
                        c = new SlotPair();
                        if (Interlocked.CompareExchange(ref current, c, null) != null)
                        {
                            continue;
                        }
                    }

                    int slot = c.TryAcquireSlot();
                    if (slot < 0)
                    {
                        Interlocked.CompareExchange(ref current, null, c);
                        continue;
                    }
                    if (slot == 0)
                    {
                        c.first = value;
                    }
                    else
                    {
                        c.second = value;
                    }

                    if (c.ReleaseSlot())
                    {
                        Interlocked.CompareExchange(ref current, null, c);
                        return c;
                    }
                    return null;
                }
            }

            public override void Cancel()
            {
                base.Cancel();
                foreach (var inner in subscribers)
                {
                    inner.Cancel();
                }
            }

            internal void InnerComplete(T value, bool hasValue)
            {
                if (hasValue)
                {
                    for (;;)
                    {
                        var sp = AddValue(value);

                        if (sp == null)
                        {
                            break;
                        }

                        try
                        {
                            value = reducer(sp.first, sp.second);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);
                            InnerError(ex);
                            return;
                        }
                    }
                }

                if (Interlocked.Decrement(ref remaining) == 0)
                {
                    var sp = Volatile.Read(ref current);
                    current = null;
                    if (sp != null)
                    {
                        Complete(sp.first);
                    }
                    else
                    {
                        Complete();
                    }
                }
            }

            internal void InnerError(Exception ex)
            {
                if (ExceptionHelper.AddError(ref error, ex))
                {
                    Cancel();
                    ex = ExceptionHelper.Terminate(ref error);
                    actual.OnError(ex);
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(ex);
                }
            }
        }

        sealed class InnerSubscriber : ISubscriber<T>
        {
            readonly ReduceFullCoordinator parent;

            readonly Func<T, T, T> reducer;

            ISubscription s;

            T value;

            bool hasValue;

            bool done;

            internal InnerSubscriber(ReduceFullCoordinator parent, Func<T, T, T> reducer)
            {
                this.parent = parent;
                this.reducer = reducer;
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.SetOnce(ref this.s, s))
                {
                    s.Request(long.MaxValue);
                }
            }

            public void OnNext(T t)
            {
                if (done)
                {
                    return;
                }

                if (!hasValue)
                {
                    value = t;
                    hasValue = true;
                }
                else
                {
                    try
                    {
                        value = reducer(value, t);
                    }
                    catch (Exception ex)
                    {
                        ExceptionHelper.ThrowIfFatal(ex);
                        done = true;
                        parent.InnerError(ex);
                    }
                }
            }

            public void OnError(Exception e)
            {
                if (done)
                {
                    ExceptionHelper.OnErrorDropped(e);
                    return;
                }
                done = true;
                parent.InnerError(e);
            }

            public void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;
                parent.InnerComplete(value, hasValue);
            }

            internal void Cancel()
            {
                SubscriptionHelper.Cancel(ref s);
            }
        }

        sealed class SlotPair
        {
            internal T first;
            internal T second;

            int acquireIndex;

            int releaseIndex;

            internal int TryAcquireSlot()
            {
                int i = Volatile.Read(ref acquireIndex);
                for (;;)
                {
                    if (i >= 2)
                    {
                        return -1;
                    }
                    int j = Interlocked.CompareExchange(ref acquireIndex, i + 1, i);
                    if (j == i)
                    {
                        return i;
                    }
                    i = j;
                }
            }

            internal bool ReleaseSlot()
            {
                return Interlocked.Increment(ref releaseIndex) == 2;
            }
        }
    }
}
