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
    sealed class PublisherEnumerable<T> : IFlux<T>, IFuseable
    {
        readonly IEnumerable<T> enumerable;

        internal PublisherEnumerable(IEnumerable<T> enumerable)
        {
            this.enumerable = enumerable;
        }

        public void Subscribe(ISubscriber<T> s)
        {

            IEnumerator<T> enumerator;

            bool empty;

            try
            {
                enumerator = enumerable.GetEnumerator();

                empty = enumerator.MoveNext();
            }
            catch (Exception ex)
            {
                ExceptionHelper.ThrowIfFatal(ex);
                EmptySubscription<T>.Error(s, ex);
                return;
            }

            if (empty)
            {
                EmptySubscription<T>.Complete(s);
                return;
            }

            if (s is IConditionalSubscriber<T>)
            {
                s.OnSubscribe(new EnumerableConditionalSubscription((IConditionalSubscriber<T>)s, enumerator));
            }
            else
            {
                s.OnSubscribe(new EnumerableSubscription(s, enumerator));
            }
        }

        abstract class EnumerableBaseSubscription : IQueueSubscription<T>
        {
            protected readonly IEnumerator<T> enumerator;

            protected long requested;

            protected bool cancelled;

            protected bool empty;

            internal EnumerableBaseSubscription(IEnumerator<T> enumerator)
            {
                this.enumerator = enumerator;
            }

            public void Cancel()
            {
                Volatile.Write(ref cancelled, true);
            }

            protected void Dispose(IEnumerator<T> e)
            {
                try
                {
                    e.Dispose();
                }
                catch (Exception ex)
                {
                    ExceptionHelper.OnErrorDropped(ex);
                }

            }

            public void Clear()
            {
                Dispose(enumerator);
            }

            public bool IsEmpty()
            {
                return empty;
            }

            public bool Offer(T value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out T value)
            {
                if (empty)
                {
                    value = default(T);
                    return false;
                }

                if (!enumerator.MoveNext())
                {
                    empty = true;
                    value = default(T);
                    return false;
                }
                value = enumerator.Current;
                return true;
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

        sealed class EnumerableSubscription : EnumerableBaseSubscription
        {
            readonly ISubscriber<T> actual;

            internal EnumerableSubscription(ISubscriber<T> actual, IEnumerator<T> enumerator) : base(enumerator)
            {
                this.actual = actual;
            }

            protected override void FastPath()
            {
                var a = actual;
                var e = enumerator;
                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        Dispose(e);
                        return;
                    }

                    a.OnNext(e.Current);

                    bool b;

                    try
                    {
                        b = e.MoveNext();
                    }
                    catch (Exception ex)
                    {
                        ExceptionHelper.ThrowIfFatal(ex);

                        a.OnError(ex);
                        return;
                    }

                    if (!b)
                    {
                        if (!Volatile.Read(ref cancelled))
                        {
                            a.OnComplete();
                        }

                        return;
                    }
                }                
            }

            protected override void SlowPath(long r)
            {
                var et = enumerator;
                var e = 0L;
                var a = actual;

                for (;;)
                {

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Dispose(et);
                            return;
                        }

                        a.OnNext(et.Current);

                        bool b;

                        try
                        {
                            b = et.MoveNext();
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);
                            a.OnError(ex);
                            return;
                        }

                        if (!b)
                        {
                            if (!Volatile.Read(ref cancelled))
                            {
                                a.OnComplete();
                            }
                            return;
                        }

                        e++;
                    }

                    r = Volatile.Read(ref requested);
                    if (r == e)
                    {
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

        sealed class EnumerableConditionalSubscription : EnumerableBaseSubscription
        {
            readonly IConditionalSubscriber<T> actual;

            internal EnumerableConditionalSubscription(IConditionalSubscriber<T> actual, IEnumerator<T> enumerator) : base(enumerator)
            {
                this.actual = actual;
            }

            protected override void FastPath()
            {
                var a = actual;
                var e = enumerator;
                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        Dispose(e);
                        return;
                    }

                    a.TryOnNext(e.Current);

                    bool b;

                    try
                    {
                        b = e.MoveNext();
                    }
                    catch (Exception ex)
                    {
                        ExceptionHelper.ThrowIfFatal(ex);

                        a.OnError(ex);
                        return;
                    }

                    if (!b)
                    {
                        if (!Volatile.Read(ref cancelled))
                        {
                            a.OnComplete();
                        }

                        return;
                    }
                }
            }

            protected override void SlowPath(long r)
            {
                var et = enumerator;
                var e = 0L;
                var a = actual;

                for (;;)
                {

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            Dispose(et);
                            return;
                        }

                        bool consumed = a.TryOnNext(et.Current);

                        bool b;

                        try
                        {
                            b = et.MoveNext();
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);
                            a.OnError(ex);
                            return;
                        }

                        if (!b)
                        {
                            if (!Volatile.Read(ref cancelled))
                            {
                                a.OnComplete();
                            }
                            return;
                        }

                        if (consumed)
                        {
                            e++;
                        }
                    }

                    r = Volatile.Read(ref requested);
                    if (r == e)
                    {
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
