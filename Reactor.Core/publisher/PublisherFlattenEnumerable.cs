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
    sealed class PublisherFlattenEnumerable<T, R> : IFlux<R>
    {
        readonly IPublisher<T> source;

        readonly Func<T, IEnumerable<R>> mapper;

        readonly int prefetch;

        internal PublisherFlattenEnumerable(IPublisher<T> source, Func<T, IEnumerable<R>> mapper,
            int prefetch)
        {
            this.source = source;
            this.mapper = mapper;
            this.prefetch = prefetch;
        }

        public void Subscribe(ISubscriber<R> s)
        {
            source.Subscribe(new FlattenEnumerableSubscriber(s, mapper, prefetch));
        }

        sealed class FlattenEnumerableSubscriber : BasicFuseableSubscriber<T, R>
        {
            readonly Func<T, IEnumerable<R>> mapper;

            readonly int prefetch;

            IQueue<T> queue;

            int wip;

            long requested;

            int outputMode;

            Exception error;

            bool cancelled;

            IEnumerator<R> enumerator;

            bool hasValue;

            public FlattenEnumerableSubscriber(ISubscriber<R> actual, Func<T, IEnumerable<R>> mapper, int prefetch) : base(actual)
            {
                this.mapper = mapper;
                this.prefetch = prefetch;
            }

            public override void OnComplete()
            {
                Volatile.Write(ref done, true);
                Drain();
            }

            public override void OnError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public override void OnNext(T t)
            {
                if (fusionMode != FuseableHelper.ASYNC)
                {
                    if (!queue.Offer(t))
                    {
                        OnError(BackpressureHelper.MissingBackpressureException());
                        return;
                    }
                }
                Drain();
            }

            public override bool Poll(out R value)
            {
                var en = enumerator;
                for (;;)
                {
                    if (en == null)
                    {
                        T t;
                        if (queue.Poll(out t))
                        {
                            en = mapper(t).GetEnumerator();
                            enumerator = en;
                        }
                        else
                        {
                            value = default(R);
                            return false;
                        }
                    }

                    if (en.MoveNext())
                    {
                        value = en.Current;
                        return true;
                    }
                    en = null;
                }
            }

            public override bool IsEmpty()
            {
                return queue.IsEmpty();
            }

            public override int RequestFusion(int mode)
            {
                if ((mode & FuseableHelper.SYNC) != 0 && fusionMode == FuseableHelper.SYNC)
                {
                    outputMode = FuseableHelper.SYNC;
                    return FuseableHelper.SYNC;
                }
                return FuseableHelper.NONE;
            }

            public override void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                    Drain();
                }
            }

            public override void Cancel()
            {
                Volatile.Write(ref cancelled, true);
                s.Cancel();
                if (QueueDrainHelper.Enter(ref wip))
                {
                    queue.Clear();
                    enumerator?.Dispose();
                    enumerator = null;
                }
            }

            protected override bool BeforeSubscribe()
            {
                var qs = this.qs;
                if (qs != null)
                {
                    int m = qs.RequestFusion(FuseableHelper.ANY);
                    if (m == FuseableHelper.SYNC)
                    {
                        queue = qs;
                        fusionMode = m;
                        Volatile.Write(ref done, true);

                        actual.OnSubscribe(this);

                        Drain();
                        return false;
                    }
                    else
                    if (m == FuseableHelper.ASYNC)
                    {
                        queue = qs;
                        fusionMode = m;

                        actual.OnSubscribe(this);

                        s.Request(prefetch < 0 ? long.MaxValue : prefetch);

                        return false;
                    }
                }

                queue = QueueDrainHelper.CreateQueue<T>(prefetch);
                return true;
            }

            void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                int missed = 1;
                var a = actual;
                var q = queue;
                var en = enumerator;

                for (;;)
                {
                    if (en == null)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            en?.Dispose();
                            enumerator = null;

                            return;
                        }

                        Exception ex = Volatile.Read(ref error);
                        if (ex != null)
                        {
                            ex = ExceptionHelper.Terminate(ref error);

                            q.Clear();
                            en?.Dispose();
                            enumerator = null;

                            a.OnError(ex);
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        T v;

                        if (queue.Poll(out v))
                        {
                            en = mapper(v).GetEnumerator();
                            enumerator = en;
                        }
                        else
                        {
                            if (d)
                            {
                                en?.Dispose();
                                enumerator = null;

                                a.OnComplete();
                                return;
                            }
                        }
                    }

                    if (en != null)
                    {
                        long r = Volatile.Read(ref requested);
                        long e = 0L;

                        while (e != r)
                        {
                            if (Volatile.Read(ref cancelled))
                            {
                                q.Clear();
                                en?.Dispose();
                                enumerator = null;

                                return;
                            }

                            Exception ex = Volatile.Read(ref error);
                            if (ex != null)
                            {
                                ex = ExceptionHelper.Terminate(ref error);

                                q.Clear();
                                en?.Dispose();
                                enumerator = null;

                                a.OnError(ex);
                                return;
                            }

                            bool b = hasValue;

                            if (!b)
                            {
                                try
                                {
                                    b = en.MoveNext();
                                }
                                catch (Exception exc)
                                {
                                    ExceptionHelper.ThrowIfFatal(exc);

                                    s.Cancel();

                                    q.Clear();

                                    ExceptionHelper.AddError(ref error, exc);
                                    exc = ExceptionHelper.Terminate(ref error);

                                    a.OnError(exc);

                                    return;
                                }
                                hasValue = b;
                            }

                            if (b)
                            {

                                a.OnNext(en.Current);

                                e++;
                                hasValue = false;
                            }
                            else
                            {
                                en?.Dispose();
                                en = null;
                                enumerator = en;
                                break;
                            }
                        }

                        if (e == r)
                        {
                            if (Volatile.Read(ref cancelled))
                            {
                                q.Clear();
                                en?.Dispose();
                                enumerator = null;

                                return;
                            }

                            Exception ex = Volatile.Read(ref error);
                            if (ex != null)
                            {
                                ex = ExceptionHelper.Terminate(ref error);

                                q.Clear();
                                en?.Dispose();
                                enumerator = null;

                                a.OnError(ex);
                                return;
                            }

                            bool b = hasValue;
                            if (!b)
                            {
                                try
                                {
                                    b = en.MoveNext();
                                }
                                catch (Exception exc)
                                {
                                    ExceptionHelper.ThrowIfFatal(exc);

                                    s.Cancel();

                                    q.Clear();

                                    ExceptionHelper.AddError(ref error, exc);
                                    exc = ExceptionHelper.Terminate(ref error);

                                    a.OnError(exc);

                                    return;
                                }
                                hasValue = b;
                            }

                            if (!b)
                            {
                                en?.Dispose();
                                en = null;
                                enumerator = en;
                            }
                        }

                        if (e != 0 && r != long.MaxValue)
                        {
                            Interlocked.Add(ref requested, -e);
                        }

                        if (en == null)
                        {
                            continue;
                        }
                    }

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }
        }
    }
}
