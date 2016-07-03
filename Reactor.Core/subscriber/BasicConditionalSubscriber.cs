using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core;
using System.Threading;
using Reactor.Core.flow;
using Reactor.Core.subscription;

namespace Reactor.Core.subscriber
{
    /// <summary>
    /// Base class for conditional subscribers, referencing a downstream
    /// conditional subscriber and having a done and ISubscription field.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <typeparam name="U"></typeparam>
    internal abstract class BasicConditionalSubscriber<T, U> : IConditionalSubscriber<T>, ISubscription
    {
        /// <summary>
        /// The actual child ISubscriber.
        /// </summary>
        protected readonly IConditionalSubscriber<U> actual;

        protected bool done;

        protected ISubscription s;

        internal BasicConditionalSubscriber(IConditionalSubscriber<U> actual)
        {
            this.actual = actual;
        }

        public void Cancel()
        {
            s.Cancel();
        }

        public abstract void OnComplete();

        public abstract void OnError(Exception e);

        public abstract void OnNext(T t);

        public abstract bool TryOnNext(T t);

        public void OnSubscribe(ISubscription s)
        {
            if (SubscriptionHelper.Validate(ref this.s, s))
            {


                if (BeforeSubscribe())
                {
                    actual.OnSubscribe(this);

                    AfterSubscribe();
                }
            }
        }

        public virtual void Request(long n)
        {
            s.Request(n);
        }

        /// <summary>
        /// Called after a successful OnSubscribe call but
        /// before the downstream's OnSubscribe is called with this.
        /// </summary>
        protected virtual bool BeforeSubscribe()
        {
            return true;
        }

        /// <summary>
        /// Called once the OnSubscribe has been called the first time
        /// and this has been set on the child ISubscriber.
        /// </summary>
        protected virtual void AfterSubscribe()
        {

        }

        /// <summary>
        /// Complete the actual ISubscriber if the sequence is not already done.
        /// </summary>
        protected void Complete()
        {
            if (done)
            {
                return;
            }
            done = true;
            actual.OnComplete();
        }

        /// <summary>
        /// Signal an error to the actual ISubscriber if the sequence is not already done.
        /// </summary>
        protected void Error(Exception ex)
        {
            if (done)
            {
                ExceptionHelper.OnErrorDropped(ex);
                return;
            }
            done = true;
            actual.OnError(ex);
        }

        /// <summary>
        /// Rethrows a fatal exception, cancels the ISubscription and
        /// calls <see cref="Error(Exception)"/>
        /// </summary>
        /// <param name="ex">The exception to rethrow or signal</param>
        protected void Fail(Exception ex)
        {
            ExceptionHelper.ThrowIfFatal(ex);
            s.Cancel();
            OnError(ex);
        }
    }
}
