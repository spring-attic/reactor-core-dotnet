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
    sealed class PublisherFunc<T> : IFlux<T>, IMono<T>, IFuseable, ICallable<T>
    {
        readonly Func<T> supplier;

        public T Value
        {
            get
            {
                return supplier();
            }
        }

        public PublisherFunc(Func<T> supplier)
        {
            this.supplier = supplier;
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
