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
    sealed class PublisherPublishOn<T> : IFlux<T>, IFuseable
    {
        readonly IPublisher<T> source;

        readonly Scheduler scheduler;

        readonly bool delayError;

        readonly int prefetch;

        internal PublisherPublishOn(IPublisher<T> source, Scheduler scheduler, bool delayError, int prefetch)
        {
            this.source = source;
            this.scheduler = scheduler;
            this.delayError = delayError;
            this.prefetch = prefetch;
        }

        public void Subscribe(ISubscriber<T> s)
        {

            Worker worker = scheduler.CreateWorker();

            if (s is IConditionalSubscriber<T>)
            {
                source.Subscribe(new PublishOnConditionalSubscriber((IConditionalSubscriber<T>)s, worker, delayError, prefetch));
            }
            else
            {
                source.Subscribe(new PublishOnSubscriber(s, worker, delayError, prefetch));
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class PublishOnSubscriber : ISubscriber<T>, IQueueSubscription<T>
        {
            // Cold fields
            readonly ISubscriber<T> actual;

            readonly Worker worker;

            readonly bool delayError;

            readonly int prefetch;

            readonly int limit;

            int sourceMode;

            int outputMode;

            ISubscription s;

            IQueue<T> queue;

            bool done;

            Exception error;

            bool cancelled;

            // Hot fields

            Pad128 p1;

            int wip;

            Pad120 p2;

            long requested;

            Pad120 p3;

            int produced;

            Pad120 p4;

            internal PublishOnSubscriber(ISubscriber<T> actual, Worker worker, bool delayError, int prefetch)
            {
                this.actual = actual;
                this.worker = worker;
                this.delayError = delayError;
                this.prefetch = prefetch;
                this.limit = prefetch - (prefetch >> 2);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    var qs = s as IQueueSubscription<T>;
                    if (qs != null)
                    {
                        int mode = qs.RequestFusion(FuseableHelper.ANY | FuseableHelper.BOUNDARY);

                        if (mode == FuseableHelper.SYNC)
                        {
                            this.sourceMode = mode;
                            this.queue = qs;
                            Volatile.Write(ref done, true);

                            actual.OnSubscribe(this);

                            Schedule();
                            return;
                        }
                        else
                        if (mode == FuseableHelper.ASYNC)
                        {
                            this.sourceMode = mode;
                            this.queue = qs;

                            actual.OnSubscribe(this);

                            s.Request(prefetch);

                            return;
                        }
                    }

                    queue = QueueDrainHelper.CreateQueue<T>(prefetch);

                    actual.OnSubscribe(this);

                    s.Request(prefetch);
                }
            }

            public void OnNext(T t)
            {
                if (sourceMode != FuseableHelper.ASYNC)
                {
                    if (!queue.Offer(t))
                    {
                        s.Cancel();
                        OnError(new InvalidOperationException("Queue is full?!"));
                        return;
                    }
                }
                Schedule();
            }

            public void OnError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    Volatile.Write(ref done, true);
                    Schedule();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                Schedule();
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                    Schedule();
                }
            }

            public void Cancel()
            {
                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                Volatile.Write(ref cancelled, true);
                worker.Dispose();
                s.Cancel();
                if (QueueDrainHelper.Enter(ref wip))
                {
                    queue.Clear();
                }
            }

            void Schedule()
            {
                if (QueueDrainHelper.Enter(ref wip))
                {
                    worker.Schedule(Drain);
                }
            }

            void DrainSync()
            {
                var a = actual;
                var q = queue;

                int missed = 1;


                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    long e = 0L;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        T v;
                        bool empty;

                        try
                        {
                            empty = !q.Poll(out v);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);

                            q.Clear();
                            s.Cancel();

                            a.OnError(ex);

                            worker.Dispose();
                            return;
                        }

                        if (empty)
                        {
                            a.OnComplete();

                            worker.Dispose();
                            return;
                        }

                        a.OnNext(v);

                        e++;
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool empty;

                        try
                        {
                            empty = q.IsEmpty();
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);

                            q.Clear();
                            s.Cancel();

                            a.OnError(ex);

                            worker.Dispose();
                            return;
                        }

                        if (empty)
                        {
                            a.OnComplete();

                            worker.Dispose();
                            return;
                        }
                    }

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            void DrainAsyncDelay()
            {
                var a = actual;
                var q = queue;

                int p = produced;
                int lim = limit;

                int missed = 1;

                for (;;)
                {

                    long r = Volatile.Read(ref requested);
                    long e = 0L;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        bool empty;

                        T v;

                        try
                        {
                            empty = !q.Poll(out v);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);

                            s.Cancel();
                            q.Clear();

                            ExceptionHelper.AddError(ref error, ex);
                            ex = ExceptionHelper.Terminate(ref error);

                            a.OnError(ex);

                            worker.Dispose();
                            return;
                        }

                       

                        if (d && empty)
                        {
                            Exception ex = error;
                            if (ex != null)
                            {
                                a.OnError(ex);
                            }
                            else
                            {
                                a.OnComplete();
                            }

                            worker.Dispose();
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        a.OnNext(v);

                        e++;

                        if (++p == lim)
                        {
                            p = 0;
                            s.Request(lim);
                        }
                    }

                    if (e == r && QueueDrainHelper.CheckTerminatedDelayed(ref cancelled, ref done, ref error, a, q, s, worker))
                    {
                        return;
                    }

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            void DrainAsyncNoDelay()
            {
                var a = actual;
                var q = queue;

                int p = produced;
                int lim = limit;

                int missed = 1;

                for (;;)
                {

                    long r = Volatile.Read(ref requested);
                    long e = 0L;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        if (d)
                        {
                            Exception ex = error;
                            if (ex != null)
                            {
                                q.Clear();
                                a.OnError(ex);

                                worker.Dispose();
                                return;
                            }
                        }

                        T v;

                        bool empty;

                        try
                        {
                            empty = !q.Poll(out v);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);

                            s.Cancel();
                            q.Clear();

                            ExceptionHelper.AddError(ref error, ex);
                            ex = ExceptionHelper.Terminate(ref error);

                            a.OnError(ex);

                            worker.Dispose();
                            return;
                        }

                        if (d && empty)
                        {
                            a.OnComplete();

                            worker.Dispose();
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        a.OnNext(v);

                        e++;
                        
                        if (++p == lim)
                        {
                            p = 0;
                            s.Request(lim);
                        }
                    }

                    if (e == r && QueueDrainHelper.CheckTerminated(ref cancelled, ref done, ref error, a, q, s, worker))
                    {
                        return;
                    }

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }

            }

            void DrainOutput()
            {
                var a = actual;

                int missed = 1;

                for (;;)
                {
                    if (Volatile.Read(ref cancelled))
                    {
                        return;
                    }

                    if (!delayError)
                    {
                        Exception ex = Volatile.Read(ref error);
                        if (ex != null)
                        {
                            ex = ExceptionHelper.Terminate(ref error);

                            a.OnError(ex);
                            return;
                        }
                    }

                    bool d = Volatile.Read(ref done);

                    a.OnNext(default(T));

                    if (d)
                    {
                        Exception ex = Volatile.Read(ref error);
                        if (ex != null)
                        {
                            a.OnError(ex);
                        }
                        else
                        {
                            a.OnComplete();
                        }

                        worker.Dispose();
                        return;
                    }

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            void Drain()
            {
                if (outputMode == FuseableHelper.ASYNC)
                {
                    DrainOutput();
                }
                else
                if (sourceMode == FuseableHelper.SYNC)
                {
                    DrainSync();
                }
                else
                if (delayError)
                {
                    DrainAsyncDelay();
                }
                else
                {
                    DrainAsyncNoDelay();
                }
            }

            public int RequestFusion(int mode)
            {
                int m = mode & FuseableHelper.ASYNC;
                outputMode = m;
                return m;
            }

            public bool Offer(T value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out T value)
            {
                return queue.Poll(out value);
            }

            public bool IsEmpty()
            {
                return queue.IsEmpty();
            }

            public void Clear()
            {
                queue.Clear();
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        sealed class PublishOnConditionalSubscriber : ISubscriber<T>, IQueueSubscription<T>
        {
            // Cold fields
            readonly IConditionalSubscriber<T> actual;

            readonly Worker worker;

            readonly bool delayError;

            readonly int prefetch;

            readonly int limit;

            int sourceMode;

            int outputMode;

            ISubscription s;

            IQueue<T> queue;

            bool done;

            Exception error;

            bool cancelled;

            // Hot fields

            long p00, p01, p02, p03, p04, p05, p06, p07;
            long p10, p11, p12, p13, p14, p15, p16, p17;

            int wip;

            long p20, p21, p22, p23, p24, p25, p26;
            long p30, p31, p32, p33, p34, p35, p36, p37;


            long requested;

            long p40, p41, p42, p43, p44, p45, p46;
            long p50, p51, p52, p53, p54, p55, p56, p57;

            int produced;

            long p60, p61, p62, p63, p64, p65, p66;
            long p70, p71, p72, p73, p74, p75, p76, p77;

            internal PublishOnConditionalSubscriber(IConditionalSubscriber<T> actual, Worker worker, bool delayError, int prefetch)
            {
                this.actual = actual;
                this.worker = worker;
                this.delayError = delayError;
                this.prefetch = prefetch;
                this.limit = prefetch - (prefetch >> 2);
            }

            public void OnSubscribe(ISubscription s)
            {
                if (SubscriptionHelper.Validate(ref this.s, s))
                {
                    var qs = s as IQueueSubscription<T>;
                    if (qs != null)
                    {
                        int mode = qs.RequestFusion(FuseableHelper.ANY | FuseableHelper.BOUNDARY);

                        if (mode == FuseableHelper.SYNC)
                        {
                            this.sourceMode = mode;
                            this.queue = qs;
                            Volatile.Write(ref done, true);

                            actual.OnSubscribe(this);

                            Schedule();
                            return;
                        }
                        else
                        if (mode == FuseableHelper.ASYNC)
                        {
                            this.sourceMode = mode;
                            this.queue = qs;

                            actual.OnSubscribe(this);

                            s.Request(prefetch);

                            return;
                        }
                    }

                    if (prefetch < 0)
                    {
                        queue = new SpscLinkedArrayQueue<T>(-prefetch);
                    }
                    else
                    {
                        queue = new SpscArrayQueue<T>(prefetch);
                    }

                    actual.OnSubscribe(this);

                    s.Request(prefetch);
                }
            }

            public void OnNext(T t)
            {
                if (sourceMode != FuseableHelper.ASYNC)
                {
                    if (!queue.Offer(t))
                    {
                        s.Cancel();
                        OnError(new InvalidOperationException("Queue is full?!"));
                        return;
                    }
                }
                Schedule();
            }

            public void OnError(Exception e)
            {
                if (ExceptionHelper.AddError(ref error, e))
                {
                    Volatile.Write(ref done, true);
                    Schedule();
                }
                else
                {
                    ExceptionHelper.OnErrorDropped(e);
                }
            }

            public void OnComplete()
            {
                Volatile.Write(ref done, true);
                Schedule();
            }

            public void Request(long n)
            {
                if (SubscriptionHelper.Validate(n))
                {
                    BackpressureHelper.GetAndAddCap(ref requested, n);
                    Schedule();
                }
            }

            public void Cancel()
            {
                if (Volatile.Read(ref cancelled))
                {
                    return;
                }
                Volatile.Write(ref cancelled, true);
                worker.Dispose();
                s.Cancel();
                if (QueueDrainHelper.Enter(ref wip))
                {
                    queue.Clear();
                }
            }

            void Schedule()
            {
                if (QueueDrainHelper.Enter(ref wip))
                {
                    worker.Schedule(Drain);
                }
            }

            void DrainSync()
            {
                var a = actual;
                var q = queue;

                int missed = 1;


                for (;;)
                {
                    long r = Volatile.Read(ref requested);
                    long e = 0L;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        T v;
                        bool empty;

                        try
                        {
                            empty = !q.Poll(out v);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);

                            q.Clear();
                            s.Cancel();

                            a.OnError(ex);

                            worker.Dispose();
                            return;
                        }

                        if (empty)
                        {
                            a.OnComplete();

                            worker.Dispose();
                            return;
                        }

                        if (a.TryOnNext(v))
                        {
                            e++;
                        }
                    }

                    if (e == r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool empty;

                        try
                        {
                            empty = q.IsEmpty();
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);

                            q.Clear();
                            s.Cancel();

                            a.OnError(ex);

                            worker.Dispose();
                            return;
                        }

                        if (empty)
                        {
                            a.OnComplete();

                            worker.Dispose();
                            return;
                        }
                    }

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            void DrainAsyncDelay()
            {
                var a = actual;
                var q = queue;

                int p = produced;
                int lim = limit;

                int missed = 1;

                for (;;)
                {

                    long r = Volatile.Read(ref requested);
                    long e = 0L;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        bool empty;

                        T v;

                        try
                        {
                            empty = !q.Poll(out v);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);

                            s.Cancel();
                            q.Clear();

                            ExceptionHelper.AddError(ref error, ex);
                            ex = ExceptionHelper.Terminate(ref error);

                            a.OnError(ex);

                            worker.Dispose();
                            return;
                        }



                        if (d && empty)
                        {
                            Exception ex = error;
                            if (ex != null)
                            {
                                a.OnError(ex);
                            }
                            else
                            {
                                a.OnComplete();
                            }

                            worker.Dispose();
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        if (a.TryOnNext(v))
                        {
                            e++;
                        }

                        if (++p == lim)
                        {
                            p = 0;
                            s.Request(lim);
                        }
                    }

                    if (e == r && QueueDrainHelper.CheckTerminatedDelayed(ref cancelled, ref done, ref error, a, q, s, worker))
                    {
                        return;
                    }

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            void DrainAsyncNoDelay()
            {
                var a = actual;
                var q = queue;

                int p = produced;
                int lim = limit;

                int missed = 1;

                for (;;)
                {

                    long r = Volatile.Read(ref requested);
                    long e = 0L;

                    while (e != r)
                    {
                        if (Volatile.Read(ref cancelled))
                        {
                            q.Clear();
                            return;
                        }

                        bool d = Volatile.Read(ref done);

                        if (d)
                        {
                            Exception ex = error;
                            if (ex != null)
                            {
                                q.Clear();
                                a.OnError(ex);

                                worker.Dispose();
                                return;
                            }
                        }

                        T v;

                        bool empty;

                        try
                        {
                            empty = !q.Poll(out v);
                        }
                        catch (Exception ex)
                        {
                            ExceptionHelper.ThrowIfFatal(ex);

                            s.Cancel();
                            q.Clear();

                            ExceptionHelper.AddError(ref error, ex);
                            ex = ExceptionHelper.Terminate(ref error);

                            a.OnError(ex);

                            worker.Dispose();
                            return;
                        }

                        if (d && empty)
                        {
                            a.OnComplete();

                            worker.Dispose();
                            return;
                        }

                        if (empty)
                        {
                            break;
                        }

                        if (a.TryOnNext(v))
                        {
                            e++;
                        }

                        if (++p == lim)
                        {
                            p = 0;
                            s.Request(lim);
                        }
                    }

                    if (e == r && QueueDrainHelper.CheckTerminated(ref cancelled, ref done, ref error, a, q, s, worker))
                    {
                        return;
                    }

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }

            }

            void DrainOutput()
            {
                var a = actual;

                int missed = 1;

                for (;;)
                {
                    bool d = Volatile.Read(ref done);

                    bool empty;

                    try
                    {
                        empty = queue.IsEmpty();
                    }
                    catch (Exception ex)
                    {
                        ExceptionHelper.ThrowIfFatal(ex);

                        s.Cancel();

                        ExceptionHelper.AddError(ref error, ex);
                        ex = ExceptionHelper.Terminate(ref error);

                        a.OnError(ex);

                        worker.Dispose();
                        return;
                    }

                    if (!empty)
                    {
                        a.TryOnNext(default(T));
                    }

                    if (d)
                    {
                        Exception ex = Volatile.Read(ref error);
                        if (ex != null)
                        {
                            a.OnError(ex);
                        }
                        else
                        {
                            a.OnComplete();
                        }

                        worker.Dispose();
                        return;
                    }

                    missed = QueueDrainHelper.Leave(ref wip, missed);
                    if (missed == 0)
                    {
                        break;
                    }
                }
            }

            void Drain()
            {
                if (outputMode == FuseableHelper.ASYNC)
                {
                    DrainOutput();
                }
                else
                if (sourceMode == FuseableHelper.SYNC)
                {
                    DrainSync();
                }
                else
                if (delayError)
                {
                    DrainAsyncDelay();
                }
                else
                {
                    DrainAsyncNoDelay();
                }
            }

            public int RequestFusion(int mode)
            {
                int m = mode & FuseableHelper.ASYNC;
                outputMode = m;
                return m;
            }

            public bool Offer(T value)
            {
                return FuseableHelper.DontCallOffer();
            }

            public bool Poll(out T value)
            {
                return queue.Poll(out value);
            }

            public bool IsEmpty()
            {
                return queue.IsEmpty();
            }

            public void Clear()
            {
                queue.Clear();
            }
        }
    }
}
