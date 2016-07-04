using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactor.Core
{
    /// <summary>
    /// Base interface for indexed one element container.
    /// </summary>
    /// <typeparam name="T">The contained value type</typeparam>
    public interface IOrderedItem<T> : IComparable<IOrderedItem<T>>
    {
        /// <summary>
        /// The index of the contained element.
        /// </summary>
        long Index { get; }

        /// <summary>
        /// The contained element.
        /// </summary>
        T Value { get; }

        /// <summary>
        /// Returns an IOrderedItem with the same index but different value content.
        /// </summary>
        /// <typeparam name="R">The result value type.</typeparam>
        /// <param name="value">The replacement value.</param>
        /// <returns>The IOrderedItem with the same index as this and the given value as content.</returns>
        IOrderedItem<R> Replace<R>(R value);
    }
}
