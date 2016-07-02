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

namespace Reactor.Core.util
{
    /// <summary>
    /// A copy-on-write array container with a terminal state.
    /// Call Init() to setup the initial array to be empty.
    /// </summary>
    /// <typeparam name="T">The tracked value type.</typeparam>
    internal struct TrackingArray<T> where T : class
    {
        static readonly T[] EMPTY = new T[0];
        static readonly T[] TERMINATED = new T[0];

        T[] array;

        internal void Init()
        {
            array = EMPTY;
        }

        /// <summary>
        /// Atomically reads the current array of items.
        /// </summary>
        /// <returns>The current array of items</returns>
        internal T[] Array()
        {
            return Volatile.Read(ref array);
        }

        /// <summary>
        /// Atomically adds the item to this container or returns
        /// false if the container has been terminated.
        /// </summary>
        /// <param name="item">The item to add</param>
        /// <returns>True if successful, false if the container has been termianted.</returns>
        internal bool Add(T item)
        {
            var a = Volatile.Read(ref array);
            for (;;)
            {
                if (a == TERMINATED)
                {
                    return false;
                }
                int n = a.Length;
                var b = new T[n + 1];
                System.Array.Copy(a, 0, b, 0, n);
                b[n] = item;
                var c = Interlocked.CompareExchange(ref array, b, a);
                if (c == a)
                {
                    return true;
                }
                a = c;
            }
        }

        /// <summary>
        /// Removes an item from this container.
        /// </summary>
        /// <param name="item">The item to remove</param>
        internal void Remove(T item)
        {
            var a = Volatile.Read(ref array);
            for (;;)
            {
                if (a == TERMINATED || a == EMPTY)
                {
                    return;
                }
                int n = a.Length;

                int j = -1;
                for (int i = 0; i < n; i++)
                {
                    if (a[i] == item)
                    {
                        j = i;
                        break;
                    }
                } 
                if (j < 0)
                {
                    return;
                }

                T[] b;

                if (n == 1)
                {
                    b = EMPTY;
                }
                else
                {
                    b = new T[n - 1];
                    System.Array.Copy(a, 0, b, 0, j);
                    System.Array.Copy(a, j + 1, b, j, n - j - 1);
                }

                var c = Interlocked.CompareExchange(ref array, b, a);
                if (c == a)
                {
                    return;
                }
                a = c;
            }
        }

        /// <summary>
        /// Atomically terminates this container and returns the last
        /// array of items.
        /// </summary>
        /// <returns>The last array of items</returns>
        internal T[] Terminate()
        {
            var a = Volatile.Read(ref array);
            if (a != TERMINATED)
            {
                a = Interlocked.Exchange(ref array, TERMINATED);
            }
            return a;
        }

        /// <summary>
        /// Checks if this container has been terminated.
        /// </summary>
        /// <returns>True if this container has been terminated.</returns>
        internal bool IsTerminated()
        {
            return Volatile.Read(ref array) == TERMINATED;
        }
    }
}
