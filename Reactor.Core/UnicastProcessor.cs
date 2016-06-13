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
using System.Diagnostics;

namespace Reactor.Core
{
    /// <summary>
    /// An IFluxProcessor (IProcessor) that allows a single ISubscriber at most, buffers
    /// incoming values in an unbounded manner and replays it to the single subscriber.
    /// </summary>
    /// <remarks>
    /// The class publicly implements IQueueSubscription for performance reasons
    /// but one should not call its methods.
    /// </remarks>
    /// <typeparam name="T">The input and output type.</typeparam>
    public sealed class UnicastProcessor<T> : IFluxProcessor<T>, IQueueSubscription<T>
    {

        readonly IQueue<T> queue;

        bool cancelled;

        int done;

        Exception error;

        int wip;

        long requested;

        ISubscriber<T> regular;

        IConditionalSubscriber<T> conditional;

        int once;

        Action onTerminated;

        bool outputFused;

        /// <summary>
        /// Construct a default UnicastProcessor with an optional termination action
        /// and the default Flux.BufferSize queue.
        /// </summary>
        /// <param name="onTerminated">The optional termination action.</param>
        public UnicastProcessor(Action onTerminated = null)
        {
            this.queue = new SpscLinkedArrayQueue<T>(Flux.BufferSize);
            this.onTerminated = onTerminated;
        }

        /// <summary>
        /// Construct a default UnicastProcessor with an optional termination action
        /// and the given capacity-hinted queue.
        /// </summary>
        /// <param name="capacityHint">The expected number of items getting buffered</param>
        /// <param name="onTerminated">The optional termination action.</param>
        public UnicastProcessor(int capacityHint, Action onTerminated = null)
        {
            this.queue = new SpscLinkedArrayQueue<T>(capacityHint);
            this.onTerminated = onTerminated;
        }

        bool lvDone()
        {
            return Volatile.Read(ref done) != 0;
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

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public void OnNext(T t)
        {
            if (lvDone() || lvCancelled())
            {
                return;
            }

            queue.Offer(t);
            Drain();
        }

        /// <inheritdoc/>
        public void OnError(Exception e)
        {
            if (lvDone() || lvCancelled())
            {
                ExceptionHelper.OnErrorDropped(e);
                return;
            }
            error = e;
            Interlocked.Exchange(ref done, 1);
            SignalTerminated();
            Drain();
        }

        /// <inheritdoc/>
        public void OnComplete()
        {
            if (lvDone() || lvCancelled())
            {
                return;
            }
            Interlocked.Exchange(ref done, 1);
            SignalTerminated();
            Drain();
        }

        /// <inheritdoc/>
        public void Subscribe(ISubscriber<T> s)
        {
            if (Interlocked.CompareExchange(ref once, 1, 0) == 0)
            {
                s.OnSubscribe(this);

                if (s is IConditionalSubscriber<T>)
                {
                    Volatile.Write(ref conditional, (IConditionalSubscriber<T>)s);
                    if (lvCancelled())
                    {
                        conditional = null;
                    }
                    else
                    {
                        Drain();
                    }
                }
                else
                {
                    Volatile.Write(ref regular, s);
                    if (lvCancelled())
                    {
                        regular = null;
                    }
                    else
                    {
                        Drain();
                    }
                }
            }
            else
            {
                EmptySubscription<T>.Error(s, new InvalidOperationException("Only a one ISubscriber allowed. Use Publish() or Replay() to share an UnicastProcessor."));
            }
        }

        void DrainRegularFused(ISubscriber<T> a)
        {
            var q = queue;
            int missed = 1;

            for (;;)
            {
                if (lvCancelled())
                {
                    regular = null;
                    q.Clear();
                    return;
                }

                bool d = lvDone();

                a.OnNext(default(T));

                if (d)
                {
                    regular = null;
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
                        regular = null;
                        q.Clear();
                        return;
                    }

                    bool d = lvDone();

                    T v;

                    bool empty = !q.Poll(out v);

                    if (d && empty)
                    {
                        regular = null;
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
                        regular = null;
                        q.Clear();
                        return;
                    }

                    if (lvDone() && q.IsEmpty())
                    {
                        regular = null;
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

        void DrainConditionalFused(IConditionalSubscriber<T> a)
        {
            var q = queue;
            int missed = 1;

            for (;;)
            {
                if (lvCancelled())
                {
                    conditional = null;
                    q.Clear();
                    return;
                }

                bool d = lvDone();

                a.TryOnNext(default(T));

                if (d)
                {
                    conditional = null;
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

        void DrainConditional(IConditionalSubscriber<T> a)
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
                        conditional = null;
                        q.Clear();
                        return;
                    }

                    bool d = lvDone();

                    T v;

                    bool empty = !q.Poll(out v);

                    if (d && empty)
                    {
                        conditional = null;
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
                        conditional = null;
                        q.Clear();
                        return;
                    }

                    if (lvDone() && q.IsEmpty())
                    {
                        conditional = null;
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
            //Interlocked.MemoryBarrier();
            var a = Volatile.Read(ref regular);

            if (a != null)
            {
                if (!QueueDrainHelper.Enter(ref wip))
                {
                    return;
                }

                if (outputFused)
                {
                    DrainRegularFused(a);
                }
                else
                {
                    DrainRegular(a);
                }
            } else
            {
                var b = Volatile.Read(ref conditional);
                if (b != null)
                {
                    if (!QueueDrainHelper.Enter(ref wip))
                    {
                        return;
                    }

                    if (outputFused)
                    {
                        DrainConditionalFused(b);
                    }
                    else
                    {
                        DrainConditional(b);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public void Request(long n)
        {
            if (SubscriptionHelper.Validate(n))
            {
                BackpressureHelper.GetAndAddCap(ref requested, n);
                Drain();
            }
        }

        /// <inheritdoc/>
        public void Cancel()
        {
            if (lvCancelled())
            {
                return;
            }
            Volatile.Write(ref cancelled, true);
            if (QueueDrainHelper.Enter(ref wip))
            {
                Clear();
                return;
            }
        }

        /// <inheritdoc/>
        public int RequestFusion(int mode)
        {
            int m = mode & FuseableHelper.ASYNC;
            outputFused = m != 0;
            return m;
        }

        /// <inheritdoc/>
        public bool Offer(T value)
        {
            return FuseableHelper.DontCallOffer();
        }

        /// <inheritdoc/>
        public bool Poll(out T value)
        {
            return queue.Poll(out value);
        }

        /// <inheritdoc/>
        public bool IsEmpty()
        {
            return queue.IsEmpty();
        }

        /// <inheritdoc/>
        public void Clear()
        {
            regular = null;
            conditional = null;
            queue.Clear();
        }
    }
}
