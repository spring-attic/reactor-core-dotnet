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
    sealed class PublisherArray<T> : IFlux<T>
    {
        readonly T[] array;

        internal PublisherArray(T[] array)
        {
            this.array = array;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (s is IConditionalSubscriber<T>)
            {
                s.OnSubscribe(new ArrayConditionalSubscription((IConditionalSubscriber<T>)s, array));
            }
            else
            {
                s.OnSubscribe(new ArraySubscription(s, array));
            }
        }

        abstract class ArrayBaseSubscription : IQueueSubscription<T>
        {
            protected readonly T[] array;

            protected int index;

            protected long requested;

            protected bool cancelled;

            internal ArrayBaseSubscription(T[] array)
            {
                this.array = array;
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
            }

            public void Clear()
            {
                index = array.Length;
            }

            public bool IsEmpty()
            {
                return index == array.Length;
            }

            public bool Offer(T value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out T value)
            {
                int i = index;
                var a = array;
                if (i != a.Length)
                {
                    index = i + 1;
                    value = a[i];
                    return true;
                }
                value = default(T);
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

        sealed class ArraySubscription : ArrayBaseSubscription
        {
            readonly ISubscriber<T> actual;


            internal ArraySubscription(ISubscriber<T> actual, T[] array) : base(array)
            {
                this.actual = actual;
            }

            protected override void FastPath()
            {
                var b = array;
                int e = b.Length;
                var a = actual;

                for (int i = index; i != e; i++)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }

                    a.OnNext(b[i]);
                }

                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                a.OnComplete();
            }

            protected override void SlowPath(long r)
            {
                var b = array;
                int f = b.Length;

                long e = 0L;
                int i = index;
                var a = actual;

                for (;;)
                {

                    while (e != r && i != f)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            return;
                        }

                        a.OnNext(b[i]);

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

        sealed class ArrayConditionalSubscription : ArrayBaseSubscription
        {
            readonly IConditionalSubscriber<T> actual;

            internal ArrayConditionalSubscription(IConditionalSubscriber<T> actual, T[] array) : base(array)
            {
                this.actual = actual;
            }

            protected override void FastPath()
            {
                var b = array;
                int e = b.Length;
                var a = actual;

                for (int i = index; i != e; i++)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }

                    a.TryOnNext(b[i]);
                }

                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                a.OnComplete();
            }

            protected override void SlowPath(long r)
            {
                var b = array;
                int f = b.Length;

                long e = 0L;
                int i = index;
                var a = actual;

                for (;;)
                {

                    while (e != r && i != f)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            return;
                        }

                        if (a.TryOnNext(b[i]))
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
