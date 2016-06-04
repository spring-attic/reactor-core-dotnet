using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core;
using System.Threading;
using Reactor.Core.flow;

namespace Reactor.Core
{
    /// <summary>
    /// Represents one of the reactive-streams signals.
    /// </summary>
    /// <typeparam name="T">The value type held</typeparam>
    public interface ISignal<T>
    {
        /// <summary>
        /// Is this signal an OnNext? Use <see cref="Next"/> property get the actual value.
        /// </summary>
        bool IsNext { get; }

        /// <summary>
        /// Is this signal an OnError? Use <see cref="Error"/> property get the actual error.
        /// </summary>
        bool IsError { get; }

        /// <summary>
        /// Is this signal an OnComplete?
        /// </summary>
        bool IsComplete { get; }

        /// <summary>
        /// Returns the OnNext value if <see cref="IsNext"/> returns true or the type-default value otherwise.
        /// </summary>
        T Next { get; }

        /// <summary>
        /// Returns the OnError value if <see cref="IsError"/> returns true, null otherwise.
        /// </summary>
        Exception Error { get; }
    }

    /// <summary>
    /// Utility methods to create and handle ISignal objects.
    /// </summary>
    public static class SignalHelper
    {
        /// <summary>
        /// Create an OnNext signal.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        public static ISignal<T> Next<T>(T value)
        {
            return new SignalNext<T>(value);
        }

        /// <summary>
        /// Create an OnError signal.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="e"></param>
        /// <returns></returns>
        public static ISignal<T> Error<T>(Exception e)
        {
            return new SignalError<T>(e);
        }

        /// <summary>
        /// Return the singleton OnComplete signal.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static ISignal<T> Complete<T>()
        {
            return SignalComplete<T>.Instance;
        }

        /// <summary>
        /// Based on the signal type, call the appropriate ISubscriber method.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="s"></param>
        /// <param name="signal"></param>
        public static void Dispatch<T>(ISubscriber<T> s, ISignal<T> signal)
        {
            if (signal.IsNext)
            {
                s.OnNext(signal.Next);
            }
            else
            if (signal.IsError)
            {
                s.OnError(signal.Error);
            }
            else
            if (signal.IsComplete)
            {
                s.OnComplete();
            }
        }
    }

    internal sealed class SignalNext<T> : ISignal<T>
    {
        readonly T value;

        internal SignalNext(T value)
        {
            this.value = value;
        }

        public Exception Error
        {
            get
            {
                return null;
            }
        }

        public bool IsComplete
        {
            get
            {
                return false;
            }
        }

        public bool IsError
        {
            get
            {
                return false;
            }
        }

        public bool IsNext
        {
            get
            {
                return true;
            }
        }

        public T Next
        {
            get
            {
                return value;
            }
        }
    }

    internal sealed class SignalError<T> : ISignal<T>
    {
        readonly Exception e;

        internal SignalError(Exception e)
        {
            this.e = e;
        }

        public Exception Error
        {
            get
            {
                return e;
            }
        }

        public bool IsComplete
        {
            get
            {
                return false;
            }
        }

        public bool IsError
        {
            get
            {
                return true;
            }
        }

        public bool IsNext
        {
            get
            {
                return false;
            }
        }

        public T Next
        {
            get
            {
                return default(T);
            }
        }
    }

    internal sealed class SignalComplete<T> : ISignal<T>
    {

        internal static readonly SignalComplete<T> Instance = new SignalComplete<T>();

        public Exception Error
        {
            get
            {
                return null;
            }
        }

        public bool IsComplete
        {
            get
            {
                return true;
            }
        }

        public bool IsError
        {
            get
            {
                return false;
            }
        }

        public bool IsNext
        {
            get
            {
                return false;
            }
        }

        public T Next
        {
            get
            {
                return default(T);
            }
        }
    }
}
