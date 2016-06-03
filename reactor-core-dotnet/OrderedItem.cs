using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reactor.Core
{
    public interface IOrderedItem<T> : IComparable<IOrderedItem<T>>
    {
        long Index { get; }

        T Value { get; }
    }
}
