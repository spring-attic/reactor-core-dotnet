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

namespace Reactor.Core.publisher
{
    sealed class PublisherConcatMap<T, R> : IFlux<R>
    {
        readonly IPublisher<T> source;

        readonly Func<T, IPublisher<R>> mapper;

        readonly int prefetch;

        readonly ConcatErrorMode errorMode;

        internal PublisherConcatMap(IPublisher<T> source, Func<T, IPublisher<R>> mapper,
            int prefetch, ConcatErrorMode errorMode)
        {
            this.source = source;
            this.mapper = mapper;
            this.prefetch = prefetch;
            this.errorMode = errorMode;
        }

        public void Subscribe(ISubscriber<R> s)
        {
            if (PublisherCallableXMap<T, R>.CallableXMap(source, s, mapper))
            {
                return;
            }

            if (errorMode == ConcatErrorMode.Immediate)
            {
                if (s is IConditionalSubscriber<R>)
                {
                    source.Subscribe(new ConcatImmediateConditionalSubscriber((IConditionalSubscriber<R>)s, mapper, prefetch));
                }
                else
                {
                    source.Subscribe(new ConcatImmediateSubscriber(s, mapper, prefetch));
                }
            }
            else
            if (errorMode == ConcatErrorMode.Boundary) 
            {
                if (s is IConditionalSubscriber<R>)
                {
                    source.Subscribe(new ConcatBoundaryConditionalSubscriber((IConditionalSubscriber<R>)s, mapper, prefetch));
                }
                else
                {
                    source.Subscribe(new ConcatBoundarySubscriber(s, mapper, prefetch));
                }
            }
            else
            {
                if (s is IConditionalSubscriber<R>)
                {
                    source.Subscribe(new ConcatEndConditionalSubscriber((IConditionalSubscriber<R>)s, mapper, prefetch));
                }
                else
                {
                    source.Subscribe(new ConcatEndSubscriber(s, mapper, prefetch));
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        abstract class ConcatBaseSubscriber : ISubscriber<T>, ISubscription, ConcatParent
        {
            protected readonly ISubscriber<R> actual;

            protected readonly Func<T, IPublisher<R>> mapper;

            protected readonly int prefetch;

            protected readonly int limit;

            protected readonly ConcatInnerSubscriber inner;

            protected ISubscription s;

            protected IQueue<T> queue;

            protected int fusionMode;

            protected bool cancelled;

            protected bool done;

            protected Exception error;

            protected SubscriptionArbiterStruct arbiter;

            /// <summary>
            /// Work-in-progress indicator for inner subscriptions.
            /// </summary>
            protected int wip;

            protected bool active;

            protected int consumed;

            Pad112 p1;

            internal ConcatBaseSubscriber(ISubscriber<R> actual, Func<T, IPublisher<R>> mapper, int prefetch)
            {
                this.actual = actual;
                this.mapper = mapper;
                this.prefetch = prefetch;
                this.limit = prefetch - (prefetch >> 2);
                this.inner = new ConcatInnerSubscriber(this);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    var qs = s as IQueueSubscription<T>;
                    if (qs != null)
                    {
                        int m = qs.RequestFusion(FuseableHelper.ANY);
                        if (m == FuseableHelper.SYNC)
                        {
                            fusionMode = m;
                            queue = qs;
                            Volatile.Write(ref done, true);

                            actual.OnSubscribe(this);

                            Drain();
                            return;
                        }
                        else
                        if (m == FuseableHelper.ASYNC)
                        {
                            fusionMode = m;
                            queue = qs;

                            actual.OnSubscribe(this);

                            s.Request(prefetch < 0 ? long.MaxValue : prefetch);

                            return;
                        }
                    }

                    queue = QueueDrainHelper.CreateQueue<T>(prefetch);

                    actual.OnSubscribe(this);

                    s.Request(prefetch < 0 ? long.MaxValue : prefetch);
                }
            }

            public void OnNext(T t)
            {
                if (fusionMode != FuseableHelper.ASYNC)
                {
                    if (!queue.Offer(t))
                    {
                        OnError(new InvalidOperationException("ConcatMap-Immediate Queue is full?"));
                        return;
                    }
                }
                Drain();
            }

            public abstract void OnError(Exception e);

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                Drain();
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    arbiter.Request(n);
                }
            }

            public void Cancel()
            {
                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                Volatile.Write(ref cancelled, true);
                s.Cancel();
                arbiter.Cancel();

                if (QueueDrainHelper.Enter(ref wip))
                {
                    queue.Clear();
                }
            }

            public abstract void InnerNext(R v);

            public abstract void InnerError(Exception e);

            public void InnerComplete()
            {
                Volatile.Write(ref active, false);
                Drain();
            }

            public void InnerSubscribe(ISubscription s)
            {
                arbiter.Set(s);
            }

            public abstract void Drain();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class ConcatImmediateSubscriber : ConcatBaseSubscriber
        {
            /// <summary>
            /// Work-in-progress indicator for the half-serializer logic.
            /// </summary>
            int serializer;

            Pad112 p1;

            internal ConcatImmediateSubscriber(ISubscriber<R> actual, Func<T, IPublisher<R>> mapper, int prefetch)
                : base(actual, mapper, prefetch)
            {
            }

            public override void OnError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    arbiter.Cancel();

                    if (Interlocked.Increment(ref serializer) == 1)
                    {
                        e = ExceptionHelper.Terminate(ref error);
                        actual.OnError(e);
                    }
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public override void InnerNext(R v)
            {
                if (Interlocked.CompareExchange(ref serializer, 1, 0) == 0)
                {
                    actual.OnNext(v);

                    if (Interlocked.CompareExchange(ref serializer, 0, 1) != 1)
                    {
                        var e = ExceptionHelper.Terminate(ref error);
                        actual.OnError(e);
                    }
                }
            }

            public override void InnerError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    s.Cancel();

                    if (Interlocked.Increment(ref serializer) == 1)
                    {
                        e = ExceptionHelper.Terminate(ref error);
                        actual.OnError(e);
                    }
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }


            public override void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        queue.Clear();
                        return;
                    }
                    if (!Volatile.Read(ref active))
                    {

                        bool d = Volatile.Read(ref done);

                        T t;

                        bool empty;

                        try
                        {
                            empty = !queue.Poll(out t);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);
                            s.Cancel();
                            queue.Clear();
                            ExceptionHelper.AddError(ref error, ex);
                            ex = ExceptionHelper.Terminate(ref error);

                            actual.OnError(ex);
                            return;
                        }

                        if (d && empty)
                        {
                            actual.OnComplete();
                            return;
                        }

                        if (!empty)
                        {

                            if (fusionMode != FuseableHelper.SYNC)
                            {
                                int c = consumed + 1;
                                if (c == limit)
                                {
                                    consumed = 0;
                                    s.Request(c);
                                }
                                else
                                {
                                    consumed = c;
                                }
                            }


                            IPublisher<R> p;

                            try
                            {
                                p = mapper(t);
                            }
                            catch (Exception ex)
                            {
                                ExceptionHelper.ThrowIfFatal(ex);
                                s.Cancel();
                                queue.Clear();
                                ExceptionHelper.AddError(ref error, ex);
                                ex = ExceptionHelper.Terminate(ref error);

                                actual.OnError(ex);
                                return;
                            }

                            if (p == null)
                            {
                                s.Cancel();
                                queue.Clear();
                                ExceptionHelper.AddError(ref error, new NullReferenceException("The mapper returned a null IPublisher."));
                                var ex = ExceptionHelper.Terminate(ref error);

                                actual.OnError(ex);
                                return;
                            }

                            Volatile.Write(ref active, true);
                            p.Subscribe(inner);
                        }
                    }

                    if (Interlocked.Decrement(ref wip) == 0)
                    {
                        break;
                    }
                }
            }
        }

        sealed class ConcatBoundarySubscriber : ConcatBaseSubscriber
        {
            internal ConcatBoundarySubscriber(ISubscriber<R> actual, Func<T, IPublisher<R>> mapper, int prefetch)
                : base (actual, mapper, prefetch)
            {
                
            }

            public override void OnError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    Volatile.Write(ref done, true);
                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public override void InnerNext(R v)
            {
                actual.OnNext(v);
            }

            public override void InnerError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    Volatile.Write(ref active, false);
                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public override void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        queue.Clear();
                        return;
                    }
                    if (!Volatile.Read(ref active))
                    {

                        Exception exc = Volatile.Read(ref error);
                        if (exc != null)
                        {
                            exc = ExceptionHelper.Terminate(ref error);
                            s.Cancel();
                            queue.Clear();

                            actual.OnError(exc);
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        T t;

                        bool empty;

                        try
                        {
                            empty = !queue.Poll(out t);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);
                            s.Cancel();
                            queue.Clear();
                            ExceptionHelper.AddError(ref error, ex);
                            ex = ExceptionHelper.Terminate(ref error);

                            actual.OnError(ex);
                            return;
                        }

                        if (d && empty)
                        {
                            actual.OnComplete();
                            return;
                        }

                        if (!empty)
                        {

                            if (fusionMode != FuseableHelper.SYNC)
                            {
                                int c = consumed + 1;
                                if (c == limit)
                                {
                                    consumed = 0;
                                    s.Request(c);
                                }
                                else
                                {
                                    consumed = c;
                                }
                            }


                            IPublisher<R> p;

                            try
                            {
                                p = mapper(t);
                            }
                            catch (Exception ex)
                            {
                                ExceptionHelper.ThrowIfFatal(ex);
                                s.Cancel();
                                queue.Clear();
                                ExceptionHelper.AddError(ref error, ex);
                                ex = ExceptionHelper.Terminate(ref error);
                                actual.OnError(ex);
                                return;
                            }

                            if (p == null)
                            {
                                s.Cancel();
                                queue.Clear();
                                ExceptionHelper.AddError(ref error, new NullReferenceException("The mapper returned a null IPublisher."));
                                actual.OnError(ExceptionHelper.Terminate(ref error));
                                return;
                            }

                            Volatile.Write(ref active, true);
                            p.Subscribe(inner);
                        }
                    }

                    if (Interlocked.Decrement(ref wip) == 0)
                    {
                        break;
                    }
                }
            }
        }

        sealed class ConcatEndSubscriber : ConcatBaseSubscriber
        {
            internal ConcatEndSubscriber(ISubscriber<R> actual, Func<T, IPublisher<R>> mapper, int prefetch)
                : base(actual, mapper, prefetch)
            {
            }

            public override void OnError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    Volatile.Write(ref done, true);
                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public override void InnerNext(R v)
            {
                actual.OnNext(v);
            }

            public override void InnerError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    Volatile.Write(ref active, false);
                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public override void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        queue.Clear();
                        return;
                    }
                    if (!Volatile.Read(ref active))
                    {

                        bool d = Volatile.Read(ref done);

                        T t;

                        bool empty;

                        try
                        {
                            empty = !queue.Poll(out t);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);
                            s.Cancel();
                            queue.Clear();
                            ExceptionHelper.AddError(ref error, ex);
                            ex = ExceptionHelper.Terminate(ref error);

                            actual.OnError(ex);
                            return;
                        }

                        if (d && empty)
                        {
                            Exception exc = Volatile.Read(ref error);
                            if (exc != null)
                            {
                                exc = ExceptionHelper.Terminate(ref error);
                                s.Cancel();
                                queue.Clear();

                                actual.OnError(exc);
                            }
                            else
                            {
                                actual.OnComplete();
                            }
                            return;
                        }

                        if (!empty)
                        {

                            if (fusionMode != FuseableHelper.SYNC)
                            {
                                int c = consumed + 1;
                                if (c == limit)
                                {
                                    consumed = 0;
                                    s.Request(c);
                                }
                                else
                                {
                                    consumed = c;
                                }
                            }


                            IPublisher<R> p;

                            try
                            {
                                p = mapper(t);
                            }
                            catch (Exception ex)
                            {
                                ExceptionHelper.ThrowIfFatal(ex);
                                s.Cancel();
                                queue.Clear();
                                ExceptionHelper.AddError(ref error, ex);
                                ex = ExceptionHelper.Terminate(ref error);
                                actual.OnError(ex);
                                return;
                            }

                            if (p == null)
                            {
                                s.Cancel();
                                queue.Clear();
                                ExceptionHelper.AddError(ref error, new NullReferenceException("The mapper returned a null IPublisher."));
                                actual.OnError(ExceptionHelper.Terminate(ref error));
                                return;
                            }

                            Volatile.Write(ref active, true);
                            p.Subscribe(inner);
                        }
                    }

                    if (Interlocked.Decrement(ref wip) == 0)
                    {
                        break;
                    }
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        abstract class ConcatBaseConditionalSubscriber : ISubscriber<T>, ISubscription, ConcatConditionalParent
        {
            protected readonly IConditionalSubscriber<R> actual;

            protected readonly Func<T, IPublisher<R>> mapper;

            protected readonly int prefetch;

            protected readonly int limit;

            protected readonly ConcatInnerSubscriber inner;

            protected ISubscription s;

            protected IQueue<T> queue;

            protected int fusionMode;

            protected bool cancelled;

            protected bool done;

            protected Exception error;

            protected SubscriptionArbiterStruct arbiter;

            /// <summary>
            /// Work-in-progress indicator for inner subscriptions.
            /// </summary>
            protected int wip;

            protected bool active;

            protected int consumed;

            Pad112 p1;

            internal ConcatBaseConditionalSubscriber(IConditionalSubscriber<R> actual, Func<T, IPublisher<R>> mapper, int prefetch)
            {
                this.actual = actual;
                this.mapper = mapper;
                this.prefetch = prefetch;
                this.limit = prefetch - (prefetch >> 2);
                this.inner = new ConcatInnerSubscriber(this);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    var qs = s as IQueueSubscription<T>;
                    if (qs != null)
                    {
                        int m = qs.RequestFusion(FuseableHelper.ANY);
                        if (m == FuseableHelper.SYNC)
                        {
                            fusionMode = m;
                            queue = qs;
                            Volatile.Write(ref done, true);

                            actual.OnSubscribe(this);

                            Drain();
                            return;
                        }
                        else
                        if (m == FuseableHelper.ASYNC)
                        {
                            fusionMode = m;
                            queue = qs;

                            actual.OnSubscribe(this);

                            s.Request(prefetch < 0 ? long.MaxValue : prefetch);

                            return;
                        }
                    }

                    queue = QueueDrainHelper.CreateQueue<T>(prefetch);

                    actual.OnSubscribe(this);

                    s.Request(prefetch < 0 ? long.MaxValue : prefetch);
                }
            }

            public void OnNext(T t)
            {
                if (fusionMode != FuseableHelper.ASYNC)
                {
                    if (!queue.Offer(t))
                    {
                        OnError(new InvalidOperationException("ConcatMap-Immediate Queue is full?"));
                        return;
                    }
                }
                Drain();
            }

            public abstract void OnError(Exception e);

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                Drain();
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    arbiter.Request(n);
                }
            }

            public void Cancel()
            {
                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                Volatile.Write(ref cancelled, true);
                s.Cancel();
                arbiter.Cancel();

                if (QueueDrainHelper.Enter(ref wip))
                {
                    queue.Clear();
                }
            }

            public abstract void InnerNext(R v);

            public abstract bool TryInnerNext(R v);

            public abstract void InnerError(Exception e);

            public void InnerComplete()
            {
                Volatile.Write(ref active, false);
                Drain();
            }

            public void InnerSubscribe(ISubscription s)
            {
                arbiter.Set(s);
            }

            public abstract void Drain();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class ConcatImmediateConditionalSubscriber : ConcatBaseConditionalSubscriber
        {
            /// <summary>
            /// Work-in-progress indicator for the half-serializer logic.
            /// </summary>
            int serializer;

            Pad112 p1;

            internal ConcatImmediateConditionalSubscriber(IConditionalSubscriber<R> actual, Func<T, IPublisher<R>> mapper, int prefetch)
                : base(actual, mapper, prefetch)
            {
            }

            public override void OnError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    arbiter.Cancel();

                    if (Interlocked.Increment(ref serializer) == 1)
                    {
                        e = ExceptionHelper.Terminate(ref error);
                        actual.OnError(e);
                    }
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public override void InnerNext(R v)
            {
                if (Interlocked.CompareExchange(ref serializer, 1, 0) == 0)
                {
                    actual.OnNext(v);

                    if (Interlocked.CompareExchange(ref serializer, 0, 1) != 1)
                    {
                        var e = ExceptionHelper.Terminate(ref error);
                        actual.OnError(e);
                    }
                }
            }

            public override bool TryInnerNext(R v)
            {
                if (Interlocked.CompareExchange(ref serializer, 1, 0) == 0)
                {
                    bool b = actual.TryOnNext(v);

                    if (Interlocked.CompareExchange(ref serializer, 0, 1) != 1)
                    {
                        var e = ExceptionHelper.Terminate(ref error);
                        actual.OnError(e);
                        return false;
                    }

                    return b;
                }
                return false;
            }

            public override void InnerError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    s.Cancel();

                    if (Interlocked.Increment(ref serializer) == 1)
                    {
                        e = ExceptionHelper.Terminate(ref error);
                        actual.OnError(e);
                    }
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }


            public override void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        queue.Clear();
                        return;
                    }
                    if (!Volatile.Read(ref active))
                    {

                        bool d = Volatile.Read(ref done);

                        T t;

                        bool empty;

                        try
                        {
                            empty = !queue.Poll(out t);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);
                            s.Cancel();
                            queue.Clear();
                            ExceptionHelper.AddError(ref error, ex);
                            ex = ExceptionHelper.Terminate(ref error);

                            actual.OnError(ex);
                            return;
                        }

                        if (d && empty)
                        {
                            actual.OnComplete();
                            return;
                        }

                        if (!empty)
                        {

                            if (fusionMode != FuseableHelper.SYNC)
                            {
                                int c = consumed + 1;
                                if (c == limit)
                                {
                                    consumed = 0;
                                    s.Request(c);
                                }
                                else
                                {
                                    consumed = c;
                                }
                            }


                            IPublisher<R> p;

                            try
                            {
                                p = mapper(t);
                            }
                            catch (Exception ex)
                            {
                                ExceptionHelper.ThrowIfFatal(ex);
                                s.Cancel();
                                queue.Clear();
                                ExceptionHelper.AddError(ref error, ex);
                                ex = ExceptionHelper.Terminate(ref error);

                                actual.OnError(ex);
                                return;
                            }

                            if (p == null)
                            {
                                s.Cancel();
                                queue.Clear();
                                ExceptionHelper.AddError(ref error, new NullReferenceException("The mapper returned a null IPublisher."));
                                var ex = ExceptionHelper.Terminate(ref error);

                                actual.OnError(ex);
                                return;
                            }

                            Volatile.Write(ref active, true);
                            p.Subscribe(inner);
                        }
                    }

                    if (Interlocked.Decrement(ref wip) == 0)
                    {
                        break;
                    }
                }
            }
        }

        sealed class ConcatBoundaryConditionalSubscriber : ConcatBaseConditionalSubscriber
        {
            internal ConcatBoundaryConditionalSubscriber(IConditionalSubscriber<R> actual, Func<T, IPublisher<R>> mapper, int prefetch)
                : base(actual, mapper, prefetch)
            {

            }

            public override void OnError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    Volatile.Write(ref done, true);
                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public override void InnerNext(R v)
            {
                actual.OnNext(v);
            }

            public override bool TryInnerNext(R v)
            {
                return actual.TryOnNext(v);
            }

            public override void InnerError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    Volatile.Write(ref active, false);
                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public override void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        queue.Clear();
                        return;
                    }
                    if (!Volatile.Read(ref active))
                    {

                        Exception exc = Volatile.Read(ref error);
                        if (exc != null)
                        {
                            exc = ExceptionHelper.Terminate(ref error);
                            s.Cancel();
                            queue.Clear();

                            actual.OnError(exc);
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        T t;

                        bool empty;

                        try
                        {
                            empty = !queue.Poll(out t);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);
                            s.Cancel();
                            queue.Clear();
                            ExceptionHelper.AddError(ref error, ex);
                            ex = ExceptionHelper.Terminate(ref error);

                            actual.OnError(ex);
                            return;
                        }

                        if (d && empty)
                        {
                            actual.OnComplete();
                            return;
                        }

                        if (!empty)
                        {

                            if (fusionMode != FuseableHelper.SYNC)
                            {
                                int c = consumed + 1;
                                if (c == limit)
                                {
                                    consumed = 0;
                                    s.Request(c);
                                }
                                else
                                {
                                    consumed = c;
                                }
                            }


                            IPublisher<R> p;

                            try
                            {
                                p = mapper(t);
                            }
                            catch (Exception ex)
                            {
                                ExceptionHelper.ThrowIfFatal(ex);
                                s.Cancel();
                                queue.Clear();
                                ExceptionHelper.AddError(ref error, ex);
                                ex = ExceptionHelper.Terminate(ref error);
                                actual.OnError(ex);
                                return;
                            }

                            if (p == null)
                            {
                                s.Cancel();
                                queue.Clear();
                                ExceptionHelper.AddError(ref error, new NullReferenceException("The mapper returned a null IPublisher."));
                                actual.OnError(ExceptionHelper.Terminate(ref error));
                                return;
                            }

                            Volatile.Write(ref active, true);
                            p.Subscribe(inner);
                        }
                    }

                    if (Interlocked.Decrement(ref wip) == 0)
                    {
                        break;
                    }
                }
            }
        }

        sealed class ConcatEndConditionalSubscriber : ConcatBaseConditionalSubscriber
        {
            internal ConcatEndConditionalSubscriber(IConditionalSubscriber<R> actual, Func<T, IPublisher<R>> mapper, int prefetch)
                : base(actual, mapper, prefetch)
            {
            }

            public override void OnError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    Volatile.Write(ref done, true);
                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public override void InnerNext(R v)
            {
                actual.OnNext(v);
            }

            public override bool TryInnerNext(R v)
            {
                return actual.TryOnNext(v);
            }


            public override void InnerError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    Volatile.Write(ref active, false);
                    Drain();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public override void Drain()
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        queue.Clear();
                        return;
                    }
                    if (!Volatile.Read(ref active))
                    {

                        bool d = Volatile.Read(ref done);

                        T t;

                        bool empty;

                        try
                        {
                            empty = !queue.Poll(out t);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);
                            s.Cancel();
                            queue.Clear();
                            ExceptionHelper.AddError(ref error, ex);
                            ex = ExceptionHelper.Terminate(ref error);

                            actual.OnError(ex);
                            return;
                        }

                        if (d && empty)
                        {
                            Exception exc = Volatile.Read(ref error);
                            if (exc != null)
                            {
                                exc = ExceptionHelper.Terminate(ref error);
                                s.Cancel();
                                queue.Clear();

                                actual.OnError(exc);
                            }
                            else
                            {
                                actual.OnComplete();
                            }
                            return;
                        }

                        if (!empty)
                        {

                            if (fusionMode != FuseableHelper.SYNC)
                            {
                                int c = consumed + 1;
                                if (c == limit)
                                {
                                    consumed = 0;
                                    s.Request(c);
                                }
                                else
                                {
                                    consumed = c;
                                }
                            }


                            IPublisher<R> p;

                            try
                            {
                                p = mapper(t);
                            }
                            catch (Exception ex)
                            {
                                ExceptionHelper.ThrowIfFatal(ex);
                                s.Cancel();
                                queue.Clear();
                                ExceptionHelper.AddError(ref error, ex);
                                ex = ExceptionHelper.Terminate(ref error);
                                actual.OnError(ex);
                                return;
                            }

                            if (p == null)
                            {
                                s.Cancel();
                                queue.Clear();
                                ExceptionHelper.AddError(ref error, new NullReferenceException("The mapper returned a null IPublisher."));
                                actual.OnError(ExceptionHelper.Terminate(ref error));
                                return;
                            }

                            Volatile.Write(ref active, true);
                            p.Subscribe(inner);
                        }
                    }

                    if (Interlocked.Decrement(ref wip) == 0)
                    {
                        break;
                    }
                }
            }
        }
        interface ConcatParent
        {
            void InnerSubscribe(ISubscription s);

            void InnerNext(R t);

            void InnerError(Exception e);

            void InnerComplete();
        }

        interface ConcatConditionalParent : ConcatParent
        {
            bool TryInnerNext(R t);
        }

        sealed class ConcatInnerSubscriber : ISubscriber<R>
        {
            readonly ConcatParent parent;

            internal ConcatInnerSubscriber(ConcatParent parent)
            {
                this.parent = parent;
            }

            public void OnComplete()
            {
                parent.InnerComplete();
            }

            public void OnError(Exception e)
            {
                parent.InnerError(e);
            }

            public void OnNext(R t)
            {
                parent.InnerNext(t);
            }

            public void OnSubscribe(ISubscription s)
            {
                parent.InnerSubscribe(s);
            }
        }

        sealed class ConcatInnerConditionalSubscriber : IConditionalSubscriber<R>
        {
            readonly ConcatConditionalParent parent;

            internal ConcatInnerConditionalSubscriber(ConcatConditionalParent parent)
            {
                this.parent = parent;
            }

            public void OnComplete()
            {
                parent.InnerComplete();
            }

            public void OnError(Exception e)
            {
                parent.InnerError(e);
            }

            public void OnNext(R t)
            {
                parent.InnerNext(t);
            }

            public void OnSubscribe(ISubscription s)
            {
                parent.InnerSubscribe(s);
            }

            public bool TryOnNext(R t)
            {
                return parent.TryInnerNext(t);
            }
        }
    }
}
