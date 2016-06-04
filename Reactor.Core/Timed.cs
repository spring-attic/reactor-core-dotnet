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

namespace Reactor.Core
{
    /// <summary>
    /// Structure holding a value and an Utc timestamp or time interval.
    /// </summary>
    /// <typeparam name="T">The value type</typeparam>
    public struct Timed<T>
    {
        /// <summary>
        /// The held value.
        /// </summary>
        public T Value { get; private set; }

        /// <summary>
        /// The held timestamp
        /// </summary>
        public long TimeMillis { get; private set; }

        /// <summary>
        /// Initializes the Timed instance.
        /// </summary>
        /// <param name="value">The value</param>
        /// <param name="timeMillis">The time in milliseconds</param>
        public Timed(T value, long timeMillis)
        {
            Value = value;
            TimeMillis = timeMillis;
        }
    }
}
