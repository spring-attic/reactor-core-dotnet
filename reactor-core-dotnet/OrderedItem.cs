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
    }
}
