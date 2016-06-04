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
    /// An IDisposable holding onto another IDisposable and allows
    /// replacing it atomically, optionally disposing the previous one.
    /// </summary>
    internal sealed class MultipleDisposable : IDisposable
    {
        IDisposable d;

        internal MultipleDisposable()
        {
        }

        internal MultipleDisposable(IDisposable d)
        {
            this.d = d;
        }

        public void Dispose()
        {
            DisposableHelper.Dispose(ref d);
        }

        public bool Replace(IDisposable next)
        {
            return DisposableHelper.Replace(ref d, next);
        }

        public bool Set(IDisposable next)
        {
            return DisposableHelper.Set(ref d, next);
        }
    }
}
