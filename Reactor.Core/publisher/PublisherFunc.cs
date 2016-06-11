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
using Reactor.Core.util;

namespace Reactor.Core.publisher
{
    sealed class PublisherFunc<T> : IFlux<T>, IMono<T>, ICallable<T>
    {
        readonly Func<T> supplier;

        readonly bool nullMeansEmpty;

        public T Value
        {
            get
            {
                return supplier();
            }
        }

        public PublisherFunc(Func<T> supplier, bool nullMeansEmpty)
        {
            this.supplier = supplier;
            this.nullMeansEmpty = nullMeansEmpty;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            var parent = new FuncSubscription(s);
            s.OnSubscribe(parent);

            T v;
            try
            {
                v = supplier();
            }
            catch (Exception ex)
            {
                ExceptionHelper.ThrowIfFatal(ex);
                parent.Error(ex);
                return;
            }

            if (nullMeansEmpty && v == null)
            {
                s.OnComplete();
                return;
            }

            parent.Complete(v);
        }

        sealed class FuncSubscription : DeferredScalarSubscription<T>
        {
            public FuncSubscription(ISubscriber<T> actual) : base(actual)
            {
            }
        }
    }
}
