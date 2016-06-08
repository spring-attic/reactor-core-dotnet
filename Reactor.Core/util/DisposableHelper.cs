using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core;
using System.Threading;
using Reactor.Core.flow;
using Reactor.Core.subscription;
using Reactor.Core.util;

namespace Reactor.Core.util
{
    /// <summary>
    /// Utility methods to work with IDisposable fields.
    /// </summary>
    public static class DisposableHelper
    {
        private static readonly IDisposable DISPOSED = new Disposed();

        /// <summary>
        /// The Disposed instance.
        /// </summary>
        internal static IDisposable Disposed { get { return DISPOSED; } }

        /// <summary>
        /// Check if the given IDisposable is the disposed instance.
        /// </summary>
        /// <param name="d">The IDisposable to check.</param>
        /// <returns>True if the <paramref name="d"/> is the disposed instance</returns>
        public static bool IsDisposed(IDisposable d)
        {
            return d == DISPOSED;
        }

        /// <summary>
        /// Check if the given field contains the disposed instance.
        /// </summary>
        /// <param name="d">The IDisposable field to check.</param>
        /// <returns>True if the <paramref name="d"/> contains the disposed instance</returns>
        public static bool IsDisposed(ref IDisposable d)
        {
            return Volatile.Read(ref d) == DISPOSED;
        }

        /// <summary>
        /// Atomically dispose the contents of the field if not already disposed.
        /// </summary>
        /// <param name="d">The target field to dispose</param>
        /// <returns>True if the current thread successfully disposed the contents</returns>
        public static bool Dispose(ref IDisposable d)
        {
            var c = Volatile.Read(ref d);
            if (c != DISPOSED)
            {
                c = Interlocked.Exchange(ref d, DISPOSED);
                if (c != DISPOSED)
                {
                    c?.Dispose();
                    return true;
                }
            }
            return true;
        }

        /// <summary>
        /// Atomically replaces the contents of the target field or disposes the
        /// new IDisposable if the field contains the disposed instance.
        /// </summary>
        /// <param name="d">The target field.</param>
        /// <param name="a">The new IDisposable instance</param>
        /// <returns>True if successful, false if the target contains the disposed instance</returns>
        public static bool Replace(ref IDisposable d, IDisposable a)
        {
            var c = Volatile.Read(ref d);
            for (;;)
            {
                if (c == DISPOSED)
                {
                    a?.Dispose();
                    return false;
                }
                var b = Interlocked.CompareExchange(ref d, a, c);
                if (b == c)
                {
                    return true;
                }
                c = b;
            }
        }

        /// <summary>
        /// Atomically sets the contents of the target field, disposing the old
        /// valuue or disposes thenew IDisposable if the field contains the 
        /// disposed instance.
        /// </summary>
        /// <param name="d">The target field.</param>
        /// <param name="a">The new IDisposable instance</param>
        /// <returns>True if successful, false if the target contains the disposed instance</returns>
        public static bool Set(ref IDisposable d, IDisposable a)
        {
            var c = Volatile.Read(ref d);
            for (;;)
            {
                if (c == DISPOSED)
                {
                    a?.Dispose();
                    return false;
                }
                var b = Interlocked.CompareExchange(ref d, a, c);
                if (b == c)
                {
                    c?.Dispose();
                    return true;
                }
                c = b;
            }
        }
    }

    /// <summary>
    /// The class representing a disposed IDisposable
    /// </summary>
    sealed class Disposed : IDisposable
    {
        public void Dispose()
        {
            // deliberately ignored
        }
    }
}
