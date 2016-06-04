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

namespace Reactor.Core.util
{
    /// <summary>
    /// A single-producer, single-consumer, unbounded concurrent queue
    /// with an island size to avoid frequent reallocation.
    /// </summary>
    /// <typeparam name="T">The stored value type</typeparam>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public sealed class SpscLinkedArrayQueue<T> : IQueue<T>
    {
        readonly int mask;

        long p1, p2, p3, p4, p5, p6, p7;
        long p8, p9, pA, pB, pC, pD, pE, pF;

        long producerIndex;
        Entry[] producerArray;

        long p11, p12, p13, p14, p15, p16;
        long p18, p19, p1A, p1B, p1C, p1D, p1E, p1F;

        long consumerIndex;
        Entry[] consumerArray;

        long p21, p22, p23, p24, p25, p26;
        long p28, p29, p2A, p2B, p2C, p2D, p2E, p2F;

        /// <summary>
        /// Constructs an instance with the given capacity rounded up to
        /// the next power-of-2 value.
        /// </summary>
        /// <param name="capacity">The target capacity.</param>
        public SpscLinkedArrayQueue(int capacity)
        {
            int c = QueueHelper.Round(capacity < 2 ? 2 : capacity);
            mask = c - 1;
            consumerArray = producerArray = new Entry[c];
            Volatile.Write(ref consumerIndex, 0L); // FIXME not sure if C# constructor with readonly field does release or not
        }

        /// <inheritdoc/>
        public bool Offer(T value)
        {
            var a = producerArray;
            int m = mask;
            long pi = producerIndex;

            int offset = (int)(pi + 1) & m;

            if (a[offset].Flag != 0)
            {
                offset = (int)pi & m;
                var b = new Entry[m + 1];
                b[offset].value = value;
                b[offset].FlagPlain = 1;
                a[offset].next = b;
                producerArray = b;
                a[offset].Flag = 2;
            }
            else
            {
                offset = (int)pi & m;
                a[offset].value = value;
                a[offset].Flag = 1;
            }

            Volatile.Write(ref producerIndex, pi + 1);
            return true;
        }

        /// <inheritdoc/>
        public bool Poll(out T value)
        {
            var a = consumerArray;
            int m = mask;
            long ci = consumerIndex;

            int offset = (int)ci & m;

            int f = a[offset].Flag;

            if (f == 0)
            {
                value = default(T);
                return false;
            }
            else
            if (f == 1)
            {
                value = a[offset].value;
                a[offset].value = default(T);
                a[offset].Flag = 0;
            } else
            {
                var b = a[offset].next;
                consumerArray = b;
                a[offset].next = null;
                value = b[offset].value;
                b[offset].value = default(T);
                b[offset].Flag = 0;
            }

            Volatile.Write(ref consumerIndex, ci + 1);
            return true;
        }

        /// <inheritdoc/>
        public bool IsEmpty()
        {
            return Volatile.Read(ref producerIndex) == Volatile.Read(ref consumerIndex);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            QueueHelper.Clear(this);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct Entry
        {
            /// <summary>
            /// Indicates the occupancy of the entry.
            /// </summary>
            int flag;

            /// <summary>
            /// The entry value.
            /// </summary>
            internal T value;

            /// <summary>
            /// Pointer to the next array.
            /// </summary>
            internal Entry[] next;

            /// <summary>
            /// Pad out to 32 bytes.
            /// </summary>
            long pad;

            /// <summary>
            /// Accesses the flag field with Volatile.
            /// </summary>
            internal int Flag
            {
                get
                {
                    return Volatile.Read(ref flag);
                }
                set
                {
                    Volatile.Write(ref flag, value);
                }
            }

            /// <summary>
            /// Write into the flag with plain access mode.
            /// </summary>
            internal int FlagPlain
            {
                set
                {
                    flag = value;
                }
            }
        }
    }
}
