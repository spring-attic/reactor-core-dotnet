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
    sealed class ArrayQueue<T> : IQueue<T>
    {
        internal T[] array;

        internal int mask;

        internal long consumerIndex;

        internal long producerIndex;

        public void Clear()
        {
            QueueHelper.Clear(this);
        }

        public bool IsEmpty()
        {
            return producerIndex == consumerIndex;
        }

        public bool Offer(T value)
        {
            T[] a = array;
            int m = mask;
            long pi = producerIndex;

            if (a == null)
            {
                a = new T[8];
                m = 7;
                mask = m;
                array = a;
                a[0] = value;
                producerIndex = 1;
            }
            else
            if (consumerIndex + m + 1 == pi)
            {
                int oldLen = a.Length;
                int offset = (int)pi & m;

                int newLen = oldLen << 1;
                m = newLen - 1;

                T[] b = new T[newLen];

                int n = oldLen - offset;
                Array.Copy(a, offset, b, offset, n);
                Array.Copy(a, 0, b, oldLen, offset);

                mask = m;
                a = b;
                array = b;
                b[(int)pi & m] = value;
                producerIndex = pi + 1;
            }
            else
            {
                int offset = (int)pi & m;
                a[offset] = value;
                producerIndex = pi + 1;
            }
            return true;
        }

        public bool Poll(out T value)
        {
            long ci = consumerIndex;
            if (ci != producerIndex)
            {
                int offset = (int)ci & mask;
                value = array[offset];

                array[offset] = default(T);
                consumerIndex = ci + 1;
                return true;
            }
            value = default(T);
            return false;
        }

        public void Drop()
        {
            long ci = consumerIndex;
            if (ci != producerIndex)
            {
                int offset = (int)ci & mask;

                array[offset] = default(T);
                consumerIndex = ci + 1;
            }
        }
    }
}
