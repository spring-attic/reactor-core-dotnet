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
    /// Utility methods for dealing with values in an array or IEnumerable and
    /// converting them into an array.
    /// </summary>
    internal static class MultiSourceHelper
    {
        public static T[] AppendFirst<T>(T[] array, T value)
        {
            int n = array.Length;
            var a = new T[n + 1];
            Array.Copy(array, 0, a, 1, n);
            a[0] = value;
            return a;
        }

        public static T[] AppendLast<T>(T[] array, T value)
        {
            int n = array.Length;
            var a = new T[n + 1];
            Array.Copy(array, 0, a, 0, n);
            a[n] = value;
            return a;
        }

        public static bool ToArray<T, U>(T[] values, IEnumerable<T> valuesEnumerable, ISubscriber<U> s, out int n, out T[] array)
        {
            if (values != null)
            {
                array = values;
                n = values.Length;
            }
            else
            {
                var i = 0;
                var a = new T[8];

                try
                {
                    foreach (var e in valuesEnumerable)
                    {
                        if (i == a.Length)
                        {
                            var b = new T[i + (i >> 1)];
                            Array.Copy(a, 0, b, 0, i);
                            a = b;
                        }
                        a[i] = e;

                        i++;
                    }
                }
                catch (Exception ex)
                {
                    ExceptionHelper.ThrowIfFatal(ex);
                    EmptySubscription<U>.Error(s, ex);
                    array = null;
                    n = 0;
                    return false;
                }

                array = a;
                n = i;
            }

            return true;
        }
    }
}
