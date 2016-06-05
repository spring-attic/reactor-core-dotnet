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
    /// A type that can't have instances.
    /// </summary>
    public sealed class Void
    {
        private Void()
        {

        }
    }
}
