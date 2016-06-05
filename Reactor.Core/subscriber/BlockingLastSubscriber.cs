using System;

using Reactive.Streams;
using System.Threading;
using Reactor.Core.subscription;

namespace Reactor.Core.subscriber
{
    /// <summary>
    /// Blocks until the last value has been emitted or the upstream is empty.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    sealed class BlockingLastSubscriber<T> : ISubscriber<T>, IDisposable
    {
        readonly CountdownEvent cde = new CountdownEvent(1);

        bool hasValue;

        T value;

        Exception error;

        ISubscription s;


        public void OnComplete()
        {
            cde.Signal();
        }

        public void OnError(Exception e)
        {
            error = e;
            cde.Signal();
        }

        public void OnNext(T t)
        {
            hasValue = true;
            value = t;
        }

        public void OnSubscribe(ISubscription s)
        {
            if (SubscriptionHelper.SetOnce(ref this.s, s))
            {
                s.Request(long.MaxValue);
            }
        }

        public void Dispose()
        {
            SubscriptionHelper.Cancel(ref s);
        }

        internal T Get(bool cancelOnInterrupt = false)
        {
            if (cde.CurrentCount != 0)
            {
                cde.Wait();
            }
            var ex = error;
            if (ex != null)
            {
                throw ex;
            }
            if (hasValue)
            {
                return value;
            }
            throw new IndexOutOfRangeException("The upstream did not produce any value");
        }

        internal bool TryGet(out T value, bool cancelOnInterrupt = false)
        {
            if (cde.CurrentCount != 0)
            {
                try
                {
                    cde.Wait();
                }
                catch (Exception exc)
                {
                    if (cancelOnInterrupt)
                    {
                        Dispose();
                    }
                    throw exc;
                }
            }
            var ex = error;
            if (ex != null)
            {
                throw ex;
            }
            if (hasValue)
            {
                value = this.value;
                return true;
            }
            value = default(T);
            return false;
        }

        internal T Get(TimeSpan timeout, bool cancelOnInterrupt = false, bool cancelOnTimeout = false)
        {
            if (cde.CurrentCount != 0)
            {
                bool b;
                try
                {
                    b = cde.Wait(timeout);
                }
                catch (Exception exc)
                {
                    if (cancelOnInterrupt)
                    {
                        Dispose();
                    }
                    throw exc;
                }
                if (!b)
                {
                    if (cancelOnTimeout)
                    {
                        Dispose();
                    }
                    throw new TimeoutException("The upstream did not produce any value in time");
                }
            }
            var ex = error;
            if (ex != null)
            {
                throw ex;
            }
            if (hasValue)
            {
                return value;
            }
            throw new IndexOutOfRangeException("The upstream did not produce any value");
        }

        internal bool TryGet(out T value, TimeSpan timeout, bool cancelOnInterrupt = false, bool cancelOnTimeout = false)
        {
            if (cde.CurrentCount != 0)
            {
                if (!cde.Wait(timeout))
                {
                    if (cancelOnTimeout)
                    {
                        Dispose();
                    }
                    value = default(T);
                    return false;
                }
            }
            var ex = error;
            if (ex != null)
            {
                throw ex;
            }
            if (hasValue)
            {
                value = this.value;
                return true;
            }
            value = default(T);
            return false;
        }
    }
}
