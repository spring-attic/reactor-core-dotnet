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
    /// Utility methods to work with IQueues.
    /// </summary>
    internal static class QueueHelper
    {
        /// <summary>
        /// Rounds the value to a power-of-2 if not already power of 2
        /// </summary>
        /// <param name="v">The value to round</param>
        /// <returns>The rounded value.</returns>
        internal static int Round(int v)
        {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            return v + 1;
        }

        /// <summary>
        /// Clear the queue by polling until no more items left.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="q">The source queue.</param>
        internal static void Clear<T>(IQueue<T> q)
        {
            T dummy;

            while (q.Poll(out dummy) && !q.IsEmpty()) ;
        }
    }
}
