using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core;
using System.Threading;
using Reactor.Core.flow;
using Reactor.Core.subscription;
using Reactor.Core.util;

namespace Reactor.Core.publisher
{
    sealed class PublisherRange : IFlux<int>, IFuseable
    {
        readonly int start;

        readonly int end;

        internal PublisherRange(int start, int count)
        {
            this.start = start;
            this.end = start + count;
        }

        public void Subscribe(ISubscriber<int> s)
        {
            if (s is IConditionalSubscriber<int>)
            {
                s.OnSubscribe(new RangeConditionalSubscription((IConditionalSubscriber<int>)s, start, end));
            }
            else
            {
                s.OnSubscribe(new RangeSubscription(s, start, end));
            }
        }

        abstract class RangeBaseSubscription : IQueueSubscription<int>
        {
            protected readonly int end;

            protected int index;

            protected long requested;

            protected bool cancelled;

            internal RangeBaseSubscription(int start, int end)
            {
                this.index = start;
                this.end = end;
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
            }

            public void Clear()
            {
                index = end;
            }

            public bool IsEmpty()
            {
                return index == end;
            }

            public bool Offer(int value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out int value)
            {
                int i = index;
                if (i != end)
                {
                    index = i + 1;
                    value = i;
                    return true;
                }
                value = 0;
                return false;
            }

            public void Request(long n)
            {
                if (BackpressureHelper.ValidateAndAddCap(ref requested, n) == 0L)
                {
                    if (n == long.MaxValue)
                    {
                        FastPath();
                    }
                    else
                    {
                        SlowPath(n);
                    }
                }
            }

            protected abstract void FastPath();

            protected abstract void SlowPath(long r);

            public int RequestFusion(int mode)
            {
                return mode & FuseableHelper.SYNC;
            }

        }

        sealed class RangeSubscription : RangeBaseSubscription
        {
            readonly ISubscriber<int> actual;


            internal RangeSubscription(ISubscriber<int> actual, int start, int end) : base(start, end)
            {
                this.actual = actual;
            }

            protected override void FastPath()
            {
                int e = end;
                var a = actual;

                for (int i = index; i != e; i++)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }

                    a.OnNext(i);
                }

                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                a.OnComplete();
            }

            protected override void SlowPath(long r)
            {
                long e = 0L;
                int i = index;
                int f = end;
                var a = actual;

                for (;;)
                {

                    while (e != r && i != f)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            return;
                        }

                        a.OnNext(i);

                        i++;
                        e++;
                    }

                    if (i == f)
                    {
                        if (!Volatile.Read(ref cancelled))
                        {
                            a.OnComplete();
                        }
                        return;
                    }

                    r = Volatile.Read(ref requested);
                    if (e == r)
                    {
                        index = i;
                        r = Interlocked.Add(ref requested, -e);
                        if (r == 0L)
                        {
                            break;
                        }
                        e = 0L;
                    }
                }
            }
        }

        sealed class RangeConditionalSubscription : RangeBaseSubscription
        {
            readonly IConditionalSubscriber<int> actual;

            internal RangeConditionalSubscription(IConditionalSubscriber<int> actual, int start, int end) : base(start, end)
            {
                this.actual = actual;
            }

            protected override void FastPath()
            {
                int e = end;
                var a = actual;

                for (int i = index; i != e; i++)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }

                    a.TryOnNext(i);
                }

                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                a.OnComplete();
            }

            protected override void SlowPath(long r)
            {
                long e = 0L;
                int i = index;
                int f = end;
                var a = actual;

                for (;;)
                {

                    while (e != r && i != f)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            return;
                        }

                        if (a.TryOnNext(i))
                        {
                            e++;
                        }

                        i++;
                    }

                    if (i == f)
                    {
                        if (!Volatile.Read(ref cancelled))
                        {
                            a.OnComplete();
                        }
                        return;
                    }

                    r = Volatile.Read(ref requested);
                    if (e == r)
                    {
                        index = i;
                        r = Interlocked.Add(ref requested, -e);
                        if (r == 0L)
                        {
                            break;
                        }
                        e = 0L;
                    }
                }
            }
        }
    }
}
