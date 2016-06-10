using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core.flow;
using Reactor.Core.subscriber;
using Reactor.Core.subscription;
using Reactor.Core.util;
using System.Threading;

namespace Reactor.Core.util
{
    /// <summary>
    /// A single-adder, single-remover tracker of values plus
    /// a separate free-slot queue.
    /// </summary>
    /// <typeparam name="T">The value type tracked.</typeparam>
    internal struct SpscFreelistTracker<T> where T : class
    {
        T[] values;

        int[] freelist;

        long producerIndex;

        long consumerIndex;

        object guard;

        int size;

        static readonly T[] TERMINATED = new T[0];

        internal void Init()
        {
            guard = new object();
            values = new T[0];
            freelist = new int[0];
        }

        internal T[] Cancel()
        {
            var a = Volatile.Read(ref values);
            if (a == TERMINATED)
            {
                return a;
            }
            a = Interlocked.Exchange(ref values, TERMINATED);
            Volatile.Write(ref size, 0);
            freelist = null;
            return a;
        }

        internal bool Add(T value)
        {
            var a = Volatile.Read(ref values);
            if (a == TERMINATED)
            {
                return false;
            }
            lock (guard)
            {
                a = Volatile.Read(ref values);
                if (a == TERMINATED)
                {
                    return false;
                }

                int idx = pollFree();
                if (idx < 0)
                {
                    int n = a.Length;
                    var b = n == 0 ? new T[4] : new T[n << 1];
                    Array.Copy(a, 0, b, 0, n);

                    int m = b.Length;
                    int[] u = new int[m];
                    for (int i = n + 1; i < m; i++)
                    {
                        u[i] = i;
                    }

                    freelist = u;
                    consumerIndex = n + 1;
                    producerIndex = m;

                    idx = n;
                    if (Interlocked.CompareExchange(ref values, b, a) != a)
                    {
                        freelist = null;
                        return false;
                    }
                    a = b;
                }

                Volatile.Write(ref a[idx], value);
                Volatile.Write(ref size, size + 1);
                return true;
            }
        }

        internal T[] Values()
        {
            return Volatile.Read(ref values);
        }

        internal int Size()
        {
            return Volatile.Read(ref size);
        }

        int pollFree()
        {
            var a = freelist;
            int m = a.Length - 1;
            long ci = consumerIndex;
            long pi = producerIndex;

            if (ci == pi)
            {
                return -1;
            }

            int idx = a[(int)ci & m];
            consumerIndex = ci + 1;
            return idx;
        }

        void offerFree(int index)
        {
            var a = freelist;
            int m = a.Length - 1;
            long pi = producerIndex;

            a[(int)pi & m] = index;
            producerIndex = pi + 1;

        }

        internal void Remove(int index)
        {
            var a = Volatile.Read(ref values);
            if (a == TERMINATED)
            {
                return;
            }

            lock (guard)
            {
                a = Volatile.Read(ref values);
                if (a == TERMINATED)
                {
                    return;
                }

                values[index] = default(T);
                offerFree(index);
                Volatile.Write(ref size, size - 1);
            }
        }
    }
}
