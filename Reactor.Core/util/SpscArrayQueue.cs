using System.Threading;
using Reactor.Core.flow;
using System.Runtime.InteropServices;


/* 
 * The algorithm was inspired by the Fast-Flow implementation in the JCTools library at
 * https://github.com/JCTools/JCTools/blob/master/jctools-core/src/main/java/org/jctools/queues/SpscUnboundedArrayQueue.java
 * 
 * The difference, as of now, is there is no item padding and no lookahead.
 */

namespace Reactor.Core.util
{
    /// <summary>
    /// A single-producer, single-consumer, bounded capacity concurrent queue.
    /// </summary>
    /// <typeparam name="T">The stored value type</typeparam>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public sealed class SpscArrayQueue<T> : IQueue<T>
    {
        readonly Entry[] array;

        readonly int mask;

        Pad112 p0;

        long producerIndex;

        Pad120 p1;

        long consumerIndex;

        Pad120 p2;

        /// <summary>
        /// Constructs an instance with the given capacity rounded up to
        /// the next power-of-2 value.
        /// </summary>
        /// <param name="capacity">The target capacity.</param>
        public SpscArrayQueue(int capacity)
        {
            int c = QueueHelper.Round(capacity);
            mask = c - 1;
            array = new Entry[c];
            Volatile.Write(ref consumerIndex, 0L); // FIXME not sure if C# constructor with readonly field does release or not
        }

        /// <inheritdoc/>
        public bool Offer(T value)
        {
            var a = array;
            int m = mask;
            long pi = producerIndex;

            int offset = (int)pi & m;

            if (a[offset].Flag != 0)
            {
                return false;
            }

            a[offset].value = value;
            a[offset].Flag = 1;
            Volatile.Write(ref producerIndex, pi + 1);

            return true;
        }

        /// <inheritdoc/>
        public bool Poll(out T value)
        {
            var a = array;
            int m = mask;
            long ci = consumerIndex;

            int offset = (int)ci & m;

            if (a[offset].Flag == 0)
            {
                value = default(T);
                return false;
            }

            value = a[offset].value;
            a[offset].value = default(T);
            a[offset].Flag = 0;
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
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        internal struct EntryPadded
        {
            long p8, p9, pA, pB, pC, pD, pE;

            /// <summary>
            /// Indicates the occupancy of the entry.
            /// </summary>
            int flag;

            /// <summary>
            /// The entry value.
            /// </summary>
            internal T value;

            long p11, p12, p13, p14, p15, p16, p17;

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
        }
    }
}
