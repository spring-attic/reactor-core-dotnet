using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactor.Core.flow
{
    /// <summary>
    /// Constants for <see cref="IQueueSubscription{T}.RequestFusion(int)"/> parameter
    /// and return types.
    /// </summary>
    public static class FuseableHelper
    {
        /// <summary>
        /// Returned by the <see cref="IQueueSubscription{T}.RequestFusion(int)"/> method to indicate no fusion will take place.
        /// </summary>
        public static readonly int NONE = 0;

        /// <summary>
        /// Requested and returned by the <see cref="IQueueSubscription{T}.RequestFusion(int)"/> to indicate synchronous fusion.
        /// </summary>
        public static readonly int SYNC = 1;

        /// <summary>
        /// Requested and returned by the <see cref="IQueueSubscription{T}.RequestFusion(int)"/> method to indicate asynchronous fusion.
        /// </summary>
        public static readonly int ASYNC = 2;

        /// <summary>
        /// Combination of <see cref="SYNC"/> and <see cref="ASYNC"/> constants.
        /// </summary>
        public static readonly int ANY = SYNC | ASYNC;

        /// <summary>
        /// Requested and returned by the <see cref="IQueueSubscription{T}.RequestFusion(int)"/> method 
        /// to indicate that the requestor is a thread-boundary.
        /// </summary>
        public static readonly int BOUNDARY = 4;

        /// <summary>
        /// Handle the case when the <see cref="IQueue{T}.Offer(T)"/> is called on a
        /// <see cref="IQueueSubscription{T}"/>.
        /// </summary>
        /// <returns>Never completes normally.</returns>
        public static bool DontCallOffer()
        {
            throw new InvalidOperationException("IQueueSubscription.Offer mustn't be called.");
        }

        /// <summary>
        /// Convert the mode flags into a string.
        /// </summary>
        /// <param name="mode">The mode flags.</param>
        /// <returns>The string representing the mode flags.</returns>
        public static string ToString(int mode)
        {
            string result = "";
            if (mode == 0)
            {
                return "NONE";
            }
            if ((mode & SYNC) != 0)
            {
                result += "SYNC | ";
            }
            if ((mode & ASYNC) != 0)
            {
                result += "ASYNC | ";
            }
            if ((mode & BOUNDARY) != 0)
            {
                result += "ASYNC | ";
            }
            return result.Substring(0, result.Length - 3);
        }
    }
}
