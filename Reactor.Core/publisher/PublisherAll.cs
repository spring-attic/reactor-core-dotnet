using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Reactive.Streams;
using Reactor.Core.flow;
using Reactor.Core.subscriber;
using Reactor.Core.subscription;
using Reactor.Core.util;
using System.Threading;

namespace Reactor.Core.publisher
{
    sealed class PublisherAll<T> : IMono<bool>
    {
        readonly IPublisher<T> source;

        readonly Func<T, bool> predicate;

        public PublisherAll(IPublisher<T> source, Func<T, bool> predicate)
        {
            this.source = source;
            this.predicate = predicate;
        }

        public void Subscribe(ISubscriber<bool> s)
        {
            source.Subscribe(new AllSubscriber(s, predicate));
        }

        sealed class AllSubscriber : DeferredScalarSubscriber<T, bool>
        {
            readonly Func<T, bool> predicate;

            bool done;

            public AllSubscriber(ISubscriber<bool> actual, Func<T, bool> predicate) : base(actual)
            {
                this.predicate = predicate;
            }

            public override void OnComplete()
            {
                if (done)
                {
                    return;
                }
                Complete(true);
            }

            public override void OnError(Exception e)
            {
                if (done)
                {
                    ExceptionHelper.OnErrorDropped(e);
                    return;
                }
                done = true;
                Error(e);
            }

            public override void OnNext(T t)
            {
                if (done)
                {
                    return;
                }

                bool b;

                try
                {
                    b = predicate(t);
                }
                catch (Exception ex)
                {
                    done = true;
                    Fail(ex);
                    return;
                }
                if (!b)
                {
                    s.Cancel();
                    done = true;
                    Complete(true);
                }
            }
        }
    }
}
