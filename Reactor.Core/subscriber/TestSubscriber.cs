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
using System.Collections;

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
        /// The number of received OnNext values.
        /// </summary>
        public int ValueCount { get { return Volatile.Read(ref valueCount); } }

        /// <summary>
        /// The received OnNext values list.
        /// </summary>
        public IList<T> Values { get { return values; } }

        /// <summary>
        /// The received OnError exception list.
        /// </summary>
        public IList<Exception> Errors { get { return errors; } }

        /// <summary>
        /// The received OnComplete count.
        /// </summary>
        public int Completions { get { return completions; } }

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
            this.lastTimestamp = DateTimeOffset.UtcNow.UtcMillis();
        }

        /// <summary>
        /// Subscribe with the Empty Subscription instance.
        /// </summary>
        public void OnSubscribe()
        {
            OnSubscribe(EmptySubscription<T>.Instance);
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
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
                Volatile.Write(ref valueCount, values.Count);
                Volatile.Write(ref lastTimestamp, DateTimeOffset.UtcNow.UtcMillis());
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

        /// <summary>
        /// Assert that the upstream called OnSubscribe with an IQueueSubscription instance.
        /// </summary>
        /// <returns>This</returns>
        public TestSubscriber<T> AssertFuseable()
        {
            if (qs == null)
            {
                AssertionError("Upstream not fuseable.");
            }
            return this;
        }

        /// <summary>
        /// Assert that no OnNext has been called.
        /// </summary>
        /// <returns>This</returns>
        public TestSubscriber<T> AssertNoValues()
        {
            if (values.Count != 0)
            {
                AssertionError(string.Format("Values received: {0}\n", values));
            }
            return this;
        }

        /// <summary>
        /// Assert that OnNext has been called the specified number of times.
        /// </summary>
        /// <param name="n">The expected number of times OnNext has been called.</param>
        /// <returns></returns>
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
                          ts.Select(x => {
                              if (x is IEnumerable)
                              {
                                  return "[" + ToString(x as IEnumerable) + "]";
                              }
                              else
                              {
                                  return x.ToString();
                              }
                          }).ToArray());
        }

        string ToString(IEnumerable<T> ts)
        {
            return string.Join(",",
                          ts.Select(x => {
                              if (x is IEnumerable) {
                                  return "[" + ToString(x as IEnumerable) + "]";
                              } else
                              {
                                  return x.ToString();
                              }
                          }).ToArray());
        }

        string ToString(IEnumerable ts)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var o in ts)
            {
                if (sb.Length != 0)
                {
                    sb.Append(",");
                }
                if (o is IEnumerable)
                {
                    sb.Append("[");
                    sb.Append(ToString(o as IEnumerable));
                    sb.Append("]");
                }
                else
                {
                    sb.Append(o.ToString());
                }
            }
            return sb.ToString();
        }

        bool SequenceEqual(IEnumerable a, IEnumerable b)
        {
            var i1 = a.GetEnumerator();
            var i2 = b.GetEnumerator();

            for (;;)
            {
                bool b1 = i1.MoveNext();
                bool b2 = i2.MoveNext();

                if (b1 == b2)
                {
                    if (!b1)
                    {
                        return true;
                    }
                    else
                    {
                        var o1 = i1.Current;
                        var o2 = i2.Current;

                        if (!((o1 == o2) || (o1 != null && o2 != null && o1.Equals(o2))))
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Assert that OnNext has been called with values in the order and in the numbers
        /// the expected array contains them.
        /// </summary>
        /// <param name="expected">The array of expected values</param>
        /// <returns>This</returns>
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

                if (a is IEnumerable && b is IEnumerable)
                {
                    if (!SequenceEqual(a as IEnumerable, b as IEnumerable))
                    {
                        AssertionError(string.Format("Values at {0} differ: Expected = {1}, Actual = {2}", i, ToString(b as IEnumerable), ToString(a as IEnumerable)));
                    }
                }
                else
                if (!(((object)a == (object)b) || (a != null && b != null && a.Equals(b))))
                {
                    AssertionError(string.Format("Values at {0} differ: Expected = {1}, Actual = {2}", i, b, a));
                }
            }

            return this;
        }

        /// <summary>
        /// Assert that OnError has not been called.
        /// </summary>
        /// <returns>This</returns>
        public TestSubscriber<T> AssertNoError()
        {
            if (errors.Count != 0)
            {
                AssertionError("No errors expected");
            }
            return this;
        }

        /// <summary>
        /// Assert that OnError has been called once with the exact Exception instance.
        /// </summary>
        /// <param name="ex">The expected exception instance</param>
        /// <returns>This</returns>
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
            if (object.Equals(errors[0], ex))
            {
                AssertionError(string.Format("Error expected ({0}) but different error received", ex));
            }
            return this;
        }

        /// <summary>
        /// Assert that there is exactly one Exception which is a of type E.
        /// </summary>
        /// <typeparam name="E">The expected exception type</typeparam>
        /// <returns>This</returns>
        public TestSubscriber<T> AssertError<E>() where E : Exception
        {
            return AssertError(e => e is E);
        }

        /// <summary>
        /// Assert that there is exactly on Exception and the given predicate returns
        /// true of it.
        /// </summary>
        /// <param name="predicate">The predicate to run.</param>
        /// <returns>This</returns>
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

        /// <summary>
        /// Assert that there is exactly one Exception which has the specified error message.
        /// </summary>
        /// <param name="expectedMessage">The expected error message</param>
        /// <returns>This</returns>
        public TestSubscriber<T> AssertErrorMessage(string expectedMessage)
        {
            return AssertError(e => e.Message != null && e.Message.Equals(expectedMessage));
        }

        /// <summary>
        /// Assert that there has been exactly one completion signal received.
        /// </summary>
        /// <returns>This</returns>
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

        /// <summary>
        /// Asser tthat there has been no completion signal received.
        /// </summary>
        /// <returns>This</returns>
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

        /// <summary>
        /// Assert that the given fusion mode has been established.
        /// </summary>
        /// <param name="mode">The expected fusion mode, <see cref="FuseableHelper"/> constants.</param>
        /// <returns>This</returns>
        public TestSubscriber<T> AssertFusionMode(int mode)
        {
            if (establishedFusionMode != mode)
            {
                AssertionError(string.Format("Wrong fusion mode. Expected = {0}, Actual = {1}", FuseableHelper.ToString(mode), FuseableHelper.ToString(establishedFusionMode)));
            }
            return this;
        }

        /// <summary>
        /// Waits for an OnError or OnComplete signal.
        /// </summary>
        /// <returns>This</returns>
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

        /// <summary>
        /// Waits for an OnError or OnComplete signal for the given
        /// </summary>
        /// <param name="timeout">The time to wait</param>
        /// <returns>This</returns>
        public TestSubscriber<T> AwaitTerminalEvent(TimeSpan timeout)
        {
            try
            {
                if (!cde.Wait(timeout))
                {
                    Cancel();
                    errors.Add(new TimeoutException("TestScheduler timed out"));
                }
            }
            catch (Exception ex)
            {
                ExceptionHelper.ThrowIfFatal(ex);
                Cancel();
                errors.Add(ex);
            }
            return this;
        }

        /// <summary>
        /// Assert that no OnNext, OnError and OnComplete was called.
        /// </summary>
        /// <returns>This</returns>
        public TestSubscriber<T> AssertNoEvents()
        {
            return AssertNoValues()
                   .AssertNoError()
                   .AssertNotComplete();
        }

        /// <summary>
        /// Assert that this TestSubscriber received the given OnNext values
        /// followed by an OnComplete.
        /// </summary>
        /// <param name="expected">The array of expected values</param>
        /// <returns>This</returns>
        public TestSubscriber<T> AssertResult(params T[] expected)
        {
            return AssertValues(expected)
                   .AssertNoError()
                   .AssertComplete();
        }

        /// <summary>
        /// Assert that the upstream has called OnSubscribe with a proper ISubscription.
        /// </summary>
        /// <returns>This</returns>
        public TestSubscriber<T> AssertSubscribed()
        {
            if (s == null)
            {
                AssertionError("Not subscribed");
            }
            return this;
        }

        /// <summary>
        /// Assert that the upstream has not called OnSubscribe yet.
        /// </summary>
        /// <returns>This</returns>
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
