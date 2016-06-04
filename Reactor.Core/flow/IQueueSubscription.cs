using Reactive.Streams;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactor.Core.flow
{
    /// <summary>
    /// A combination of an IQueue and an ISubscription to allow queue fusion.
    /// </summary>
    /// <typeparam name="T">The value type in the queue.</typeparam>
    public interface IQueueSubscription<T> : IQueue<T>, ISubscription
    {
        /// <summary>
        /// Indicate the intent to fuse two subsequent operators.
        /// </summary>
        /// <param name="mode">The wanted fusion mode. See the <see cref="FuseableHelper"/> constants.</param>
        /// <returns>The established fusion mode. See the <see cref="FuseableHelper"/> constants.</returns>
        int RequestFusion(int mode);
    }
}
