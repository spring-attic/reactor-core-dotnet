using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactor.Core.flow
{
    /// <summary>
    /// Marker interface indicating that the <see cref="Reactive.Streams.IPublisher{T}"/>
    /// can be back-fused.
    /// </summary>
    /// <seealso cref="Reactor.Core.flow.FuseableHelper"/>
    public interface IFuseable
    {
    }

    /// <summary>
    /// Constants for <see cref="IQueueSubscription{T}.RequestFusion(int)"/> parameter
    /// and return types.
    /// </summary>
    public static class FuseableHelper
    {
        public static readonly int NONE = 0;

        public static readonly int SYNC = 1;

        public static readonly int ASYNC = 2;

        public static readonly int ANY = SYNC | ASYNC;

        public static readonly int BOUNDARY = 4;

    }
}
