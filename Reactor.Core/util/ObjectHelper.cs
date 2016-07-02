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

namespace Reactor.Core.util
{
    /// <summary>
    /// Object-helper methods.
    /// </summary>
    internal static class ObjectHelper
    {
        /// <summary>
        /// Throws an Exception if the value is null, returns it otherwise.
        /// </summary>
        /// <typeparam name="T">The value type.</typeparam>
        /// <param name="value">The value.</param>
        /// <param name="errorMessage">The error message.</param>
        /// <returns>The value.</returns>
        internal static T RequireNonNull<T>(T value, string errorMessage) where T : class
        {
            if (value == null)
            {
                throw new NullReferenceException(errorMessage);
            }
            return value;
        }
    }
}
