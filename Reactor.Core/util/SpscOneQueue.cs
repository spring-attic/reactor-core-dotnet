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
    /// A queue with capacity of one.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    public sealed class SpscOneQueue<T> : IQueue<T>
    {
        bool hasValue;
        T value;

        /// <inheritdoc/>
        public void Clear()
        {
            value = default(T);
            Volatile.Write(ref hasValue, false);
        }

        /// <inheritdoc/>
        public bool IsEmpty()
        {
            return !Volatile.Read(ref hasValue);
        }

        /// <inheritdoc/>
        public bool Offer(T value)
        {
            if (Volatile.Read(ref hasValue))
            {
                return false;
            }
            this.value = value;
            Volatile.Write(ref hasValue, true);
            return true;
        }

        /// <inheritdoc/>
        public bool Poll(out T value)
        {
            if (Volatile.Read(ref hasValue))
            {
                value = this.value;
                this.value = default(T);
                Volatile.Write(ref hasValue, false);
                return true;
            }
            value = default(T);
            return false;
        }
    }
}
