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

namespace Reactor.Core.subscriber
{

    /// <summary>
    /// An ISubscriber with built-in assertions for testing synchronous
    /// and asynchronous IFlux and IMono sequences.
    /// </summary>
    /// <typeparam name="T">The consumed value type.</typeparam>
    public sealed class TestSubscriber<T> : ISubscriber<T>, ISubscription
    {
        readonly List<T> values = new List<T>();

        readonly List<Exception> errors = new List<Exception>();

        readonly CountdownEvent cde = new CountdownEvent(1);

        int completions;

        int subscriptions;

        bool subscriptionValidated;

        int requestFusionMode;

        int establishedFusionMode;

        ISubscription s;

        IQueueSubscription<T> qs;

        long requested;

        /// <summary>
        /// Number of items received, use Volatile reads on this.
        /// </summary>
        int valueCount;

        /// <summary>
        /// When the last item was received, UTC milliseconds, use Volatile reads on this.
        /// </summary>
        long lastTimestamp;

        /// <summary>
        /// Constructs a new TestSubscriber instance with an optional
        /// initial request amount.
        /// </summary>
        /// <param name="initialRequest">The initial request amount.</param>
        /// <param name="fusionMode">The fusion mode to establish.</param>
        public TestSubscriber(long initialRequest = long.MaxValue, int fusionMode = 0)
        {
            this.requested = initialRequest;
            this.requestFusionMode = fusionMode;
        }

        /// <inheritdoc/>
        public void OnSubscribe(ISubscription s)
        {
            subscriptions++;
            if (SubscriptionHelper.SetOnce(ref this.s, s))
            {
                var qs = s as IQueueSubscription<T>;
                this.qs = qs;
                if (qs != null)
                {
                    if (requestFusionMode != FuseableHelper.NONE)
                    {
                        int m = qs.RequestFusion(requestFusionMode);
                        establishedFusionMode = m;
                        if (m == FuseableHelper.SYNC)
                        {
                            try
                            {
                                try
                                {
                                    T v;

                                    while (qs.Poll(out v))
                                    {
                                        values.Add(v);
                                    }
                                    completions++;
                                    Volatile.Write(ref valueCount, values.Count);
                                    Volatile.Write(ref lastTimestamp, DateTimeOffset.UtcNow.UtcMillis());
                                }
                                catch (Exception ex)
                                {
                                    errors.Add(ex);
                                }
                            }
                            finally
                            {
                                cde.Signal();
                            }

                            return;
                        }
                    }
                }

                long r = Interlocked.Exchange(ref requested, 0L);
                if (r != 0L)
                {
                    s.Request(r);
                }
            }
        }

        /// <inheritdoc/>
        public void OnNext(T t)
        {
            if (establishedFusionMode == FuseableHelper.ASYNC)
            {
                try
                {
                    T v;
                    while (qs.Poll(out v))
                    {
                        values.Add(v);
                    }
                    Volatile.Write(ref valueCount, values.Count);
                    Volatile.Write(ref lastTimestamp, DateTimeOffset.UtcNow.UtcMillis());
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
                return;
            }

            if (!subscriptionValidated)
            {
                subscriptionValidated = true;
                if (s == null)
                {
                    errors.Add(new InvalidOperationException("OnSubscribe was not called before OnNext(" + t + ")"));
                }
            }

            values.Add(t);
            Volatile.Write(ref valueCount, valueCount + 1);
            Volatile.Write(ref lastTimestamp, DateTimeOffset.UtcNow.UtcMillis());
        }

        /// <inheritdoc/>
        public void OnError(Exception e)
        {
            try
            {
                if (!subscriptionValidated)
                {
                    subscriptionValidated = true;
                    if (s == null)
                    {
                        errors.Add(new InvalidOperationException("OnSubscribe() was not called before OnError(" + e + ")"));
                    }
                }

                errors.Add(e);
            }
            finally
            {
                cde.Signal();
            }
        }

        /// <inheritdoc/>
        public void OnComplete()
        {
            try
            {
                if (!subscriptionValidated)
                {
                    subscriptionValidated = true;
                    if (s == null)
                    {
                        errors.Add(new InvalidOperationException("OnSubscribe() was not called before OnComplete()"));
                    }
                }
                completions++;
            }
            finally
            {
                cde.Signal();
            }
        }

        /// <inheritdoc/>
        public void Request(long n)
        {
            if (establishedFusionMode != FuseableHelper.SYNC)
            {
                BackpressureHelper.DeferredRequest(ref s, ref requested, n);
            }
        }

        /// <inheritdoc/>
        public void Cancel()
        {
            SubscriptionHelper.Cancel(ref s);
        }

        void AssertionError(string message)
        {
            long now = DateTimeOffset.UtcNow.UtcMillis();

            string msg = string.Format("{0} (latch = {1}, values = {2}, last = {3}, errors = {4}, completions = {5})", message,
                cde.CurrentCount, Volatile.Read(ref valueCount), Volatile.Read(ref lastTimestamp) - now, errors.Count, completions);

            int c = errors.Count;
            if (c == 0)
            {
                throw new InvalidOperationException(msg);
            }
            else
            if (c == 1)
            {
                throw new InvalidOperationException(msg, errors[0]);
            }
            throw new InvalidOperationException(msg, new AggregateException(errors));
        }

        public TestSubscriber<T> AssertFuseable()
        {
            if (qs == null)
            {
                AssertionError("Upstream not fuseable.");
            }
            return this;
        }

        public TestSubscriber<T> AssertNoValues()
        {
            if (values.Count != 0)
            {
                AssertionError(string.Format("Values received: {0}\n", values));
            }
            return this;
        }

        public TestSubscriber<T> AssertValueCount(int n)
        {
            if (values.Count != n)
            {
                AssertionError(string.Format("Different number of values received: Expected = {0}, Actual = {1}\n", n, values.Count));
            }
            return this;
        }

        string ToString(T[] ts)
        {
            return string.Join(",",
                          ts.Select(x => x.ToString()).ToArray());
        }

        string ToString(IEnumerable<T> ts)
        {
            return string.Join(",",
                          ts.Select(x => x.ToString()).ToArray());
        }

        public TestSubscriber<T> AssertValues(params T[] expected)
        {
            if (values.Count != expected.Length)
            {
                AssertionError(string.Format("Different number of values received: Expected = [{0}] {1}, Actual = {2}\n", 
                    expected.Length, ToString(expected), ToString(values)));
            }

            var ec = EqualityComparer<T>.Default;
            int n = expected.Length;
            for (int i = 0; i < n; i++)
            {
                var a = values[i];
                var b = expected[i];
                if (!ec.Equals(a, b))
                {
                    AssertionError(string.Format("Values at {0} differ: Expected = {1}, Actual = {2}", i, a, b));
                }
            }

            return this;
        }

        public TestSubscriber<T> AssertNoError()
        {
            if (errors.Count != 0)
            {
                AssertionError("No errors expected");
            }
            return this;
        }

        public TestSubscriber<T> AssertError(Exception ex)
        {
            int c = errors.Count;
            if (c == 0)
            {
                AssertionError(string.Format("Error expected ({0}) but no errors received", ex));
            }
            if (c != 1)
            {
                AssertionError(string.Format("Error expected ({0}) but multiple errors received", ex));
            }
            if (errors[0] != ex)
            {
                AssertionError(string.Format("Error expected ({0}) but different error received", ex));
            }
            return this;
        }

        public TestSubscriber<T> AssertError<E>() where E : Exception
        {
            int c = errors.Count;
            if (c == 0)
            {
                AssertionError(string.Format("Error expected ({0}) but no errors received", typeof(E)));
            }
            if (c != 1)
            {
                AssertionError(string.Format("Error expected ({0}) but multiple errors received", typeof(E)));
            }
            if (!(errors[0] is E))
            {
                AssertionError(string.Format("Error expected ({0}) but different error received", typeof(E)));
            }
            return this;
        }

        public TestSubscriber<T> AssertError(Func<Exception, bool> predicate)
        {
            int c = errors.Count;
            if (c == 0)
            {
                AssertionError(string.Format("No errors received"));
            }
            if (c != 1)
            {
                AssertionError(string.Format("Multiple errors received"));
            }
            if (!predicate(errors[0]))
            {
                AssertionError(string.Format("Received error did not match the predicate"));
            }
            return this;
        }

        public TestSubscriber<T> AssertComplete()
        {
            int c = completions;
            if (c == 0)
            {
                AssertionError("Not completed");
            }
            if (c != 1)
            {
                AssertionError("Multiple completions");
            }
            return this;
        }

        public TestSubscriber<T> AssertNotComplete()
        {
            int c = completions;
            if (c == 1)
            {
                AssertionError("Completed");
            }
            if (c > 1)
            {
                AssertionError("Multiple completions");
            }
            return this;
        }

        public TestSubscriber<T> AssertFusionMode(int mode)
        {
            if (establishedFusionMode != mode)
            {
                AssertionError(string.Format("Wrong fusion mode. Expected = {0}, Actual = {1}", FuseableHelper.ToString(mode), FuseableHelper.ToString(establishedFusionMode)));
            }
            return this;
        }

        public TestSubscriber<T> AwaitTerminalEvent()
        {
            try
            {
                cde.Wait();
            }
            catch (Exception ex)
            {
                ExceptionHelper.OnErrorDropped(ex);
                Cancel();
            }
            return this;
        }

        public TestSubscriber<T> AwaitTerminalEvent(TimeSpan timeout)
        {
            try
            {
                if (!cde.Wait(timeout))
                {
                    Cancel();
                }
            }
            catch (Exception ex)
            {
                ExceptionHelper.OnErrorDropped(ex);
                Cancel();
            }
            return this;
        }

        public TestSubscriber<T> AssertNoEvents()
        {
            return AssertNoValues()
                   .AssertNoError()
                   .AssertNotComplete();
        }

        public TestSubscriber<T> AssertResult(params T[] expected)
        {
            return AssertValues(expected)
                   .AssertNoError()
                   .AssertComplete();
        }

        public TestSubscriber<T> AssertSubscribed()
        {
            if (s == null)
            {
                AssertionError("Not subscribed");
            }
            return this;
        }

        public TestSubscriber<T> AssertNotSubscribed()
        {
            if (s != null)
            {
                AssertionError("Subscribed");
            }
            return this;
        }

    }
}
