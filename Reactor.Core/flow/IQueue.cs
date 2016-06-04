using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactor.Core.flow
{
    /// <summary>
    /// The standard queue interface.
    /// </summary>
    /// <typeparam name="T">The value type</typeparam>
    public interface IQueue<T>
    {
        /// <summary>
        /// Offers the given value and returns true if the queue is not full.
        /// </summary>
        /// <param name="value">The value to enqueue.</param>
        /// <returns>True if successful, false if the queue is full.</returns>
        bool Offer(T value);

        /// <summary>
        /// Tries polling a value into the output value and returns true
        /// if successful
        /// </summary>
        /// <param name="value">The output to dequeue the value into</param>
        /// <returns>True if a value was polled, false if the queue is empty</returns>
        bool Poll(out T value);

        /// <summary>
        /// Returns true if the queue is empty.
        /// </summary>
        /// <returns>True if the queue is empty.</returns>
        bool IsEmpty();

        /// <summary>
        /// Clears the queue.
        /// </summary>
        void Clear();
    }
}
