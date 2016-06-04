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
    /// Tracks IDisposables with an associated index and replaces them
    /// only if an incoming index is bigger than the hosted index.
    /// </summary>
    internal sealed class IndexedMultipleDisposable : IDisposable
    {
        IndexedEntry entry;

        public bool Replace(IDisposable next, long nextIndex)
        {
            var e = Volatile.Read(ref entry);
            for (;;)
            {
                if (e == IndexedEntry.DISPOSED)
                {
                    next?.Dispose();
                    return false;
                }
                if (e != null)
                {
                    if (e.index > nextIndex)
                    {
                        return true;
                    }
                }
                IndexedEntry u = new IndexedEntry(nextIndex, next);

                IndexedEntry v = Interlocked.CompareExchange(ref entry, u, e);
                if (v == e)
                {
                    return true;
                }
                else
                {
                    e = v;
                }
            }
        }

        public void Dispose()
        {
            var a = Volatile.Read(ref entry);
            if (a != IndexedEntry.DISPOSED)
            {
                a = Interlocked.Exchange(ref entry, IndexedEntry.DISPOSED);
                if (a != IndexedEntry.DISPOSED)
                {
                    a?.d?.Dispose();
                }
            }
        }
    }

    sealed class IndexedEntry
    {

        internal static readonly IndexedEntry DISPOSED = new IndexedEntry(long.MaxValue, null);

        internal readonly long index;

        internal readonly IDisposable d;

        internal IndexedEntry(long index, IDisposable d)
        {
            this.index = index;
            this.d = d;
        }
    }
}
