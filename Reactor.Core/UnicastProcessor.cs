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

namespace Reactor.Core
{
    /// <summary>
    /// An IFluxProcessor (IProcessor) that allows a single ISubscriber at most, buffers
    /// incoming values in an unbounded manner and replays it to the single subscriber.
    /// </summary>
    /// <remarks>
    /// The class publicly implements IQueueSubscription for performance reasons
    /// but one should not call its methods methods.
    /// </remarks>
    /// <typeparam name="T">The input and output type.</typeparam>
    public sealed class UnicastProcessor<T> : IFluxProcessor<T>, IQueueSubscription<T>
    {

        readonly IQueue<T> queue;

        bool cancelled;

        bool done;

        Exception error;

        int wip;

        long requested;

        ISubscriber<T> actual;

        int once;

        Action onTerminated;

        bool outputFused;

        public UnicastProcessor(Action onTerminated = null)
        {
            this.queue = new SpscLinkedArrayQueue<T>(Flux.BufferSize);
            this.onTerminated = onTerminated;
        }

        public UnicastProcessor(int capacityHint, Action onTerminated = null)
        {
            this.queue = new SpscLinkedArrayQueue<T>(capacityHint);
            this.onTerminated = onTerminated;
        }

        bool lvDone()
        {
            return Volatile.Read(ref done);
        }

        bool lvCancelled()
        {
            return Volatile.Read(ref cancelled);
        }

        void SignalTerminated()
        {
            var a = Volatile.Read(ref onTerminated);
            if (a != null)
            {
                a = Interlocked.Exchange(ref onTerminated, null);
                a?.Invoke();
            }
        }

        public void OnSubscribe(ISubscription s)
        {
            if (lvDone() || lvCancelled())
            {
                s.Cancel();
            }
            else
            {
                s.Request(long.MaxValue);
            }
        }

        public void OnNext(T t)
        {
            if (lvDone() || lvCancelled())
            {
                return;
            }

            queue.Offer(t);
            Drain();
        }

        public void OnError(Exception e)
        {
            if (lvDone() || lvCancelled())
            {
                ExceptionHelper.OnErrorDropped(e);
                return;
            }
            error = e;
            Volatile.Write(ref done, true);
            SignalTerminated();
            Drain();
        }

        public void OnComplete()
        {
            if (lvDone() || lvCancelled())
            {
                return;
            }
            Volatile.Write(ref done, true);
            SignalTerminated();
            Drain();
        }

        public void Subscribe(ISubscriber<T> s)
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                s.OnSubscribe(this);

                Volatile.Write(ref actual, s);
                if (lvCancelled())
                {
                    actual = null;
                }
                else
                {
                    Drain();
                }
            }
            else
            {
                EmptySubscription<T>.Error(s, new InvalidOperationException("Only a one ISubscriber allowed. Use Publish() or Replay() to share an UnicastProcessor."));
            }
        }

        void DrainFused(ISubscriber<T> a)
        {
            var q = queue;
            int missed = 1;

            for (;;)
            {
                if (lvCancelled())
                {
                    actual = null;
                    q.Clear();
                    return;
                }

                bool d = lvDone();

                a.OnNext(default(T));

                if (d)
                {
                    actual = null;
                    Exception ex = error;
                    if (ex != null)
                    {
                        a.OnError(ex);
                    }
                    else
                    {
                        a.OnComplete();
                    }

                    return;
                }

                missed = QueueDrainHelper.Leave(ref wip, missed);
                if (missed == 0)
                {
                    break;
                }
            }
        }

        void DrainRegular(ISubscriber<T> a)
        {
            var q = queue;
            int missed = 1;

            for (;;)
            {
                long r = Volatile.Read(ref requested);
                long e = 0L;

                while (e != r)
                {
                    if (lvCancelled())
                    {
                        actual = null;
                        q.Clear();
                        return;
                    }

                    bool d = lvDone();

                    T v;

                    bool empty = !q.Poll(out v);

                    if (d && empty)
                    {
                        actual = null;
                        Exception ex = error;
                        if (ex != null)
                        {
                            a.OnError(ex);
                        }
                        else
                        {
                            a.OnComplete();
                        }

                        return;
                    }

                    if (empty)
                    {
                        break;
                    }

                    a.OnNext(v);

                    e++;
                }

                if (e == r)
                {
                    if (lvCancelled())
                    {
                        actual = null;
                        q.Clear();
                        return;
                    }

                    if (lvDone() && q.IsEmpty())
                    {
                        actual = null;
                        Exception ex = error;
                        if (ex != null)
                        {
                            a.OnError(ex);
                        }
                        else
                        {
                            a.OnComplete();
                        }

                        return;
                    }
                }

                if (e != 0 && r != long.MaxValue)
                {
                    Interlocked.Add(ref requested, -e);
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
            var a = Volatile.Read(ref actual);

            if (a != null)
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                if (outputFused)
                {
                    DrainFused(a);
                }
                else
                {
                    DrainRegular(a);
                }
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

        public void Cancel()
        {
            if (lvCancelled())
            {
                return;
            }
            Volatile.Write(ref cancelled, true);
            if (QueueDrainHelper.Enter(ref wip))
            {
                queue.Clear();
                actual = null;
                return;
            }
        }

        public int RequestFusion(int mode)
        {
            int m = mode & FuseableHelper.ASYNC;
            outputFused = m != 0;
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
            actual = null;
            queue.Clear();
        }
    }
}
