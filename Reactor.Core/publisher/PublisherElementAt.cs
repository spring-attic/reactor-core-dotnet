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

namespace Reactor.Core.publisher
{
    sealed class PublisherElementAt<T> : IMono<T>
    {
        readonly IPublisher<T> source;

        readonly long index;

        readonly T defaultValue;

        readonly bool hasDefault;

        internal PublisherElementAt(IPublisher<T> source, long index, T defaultValue, bool hasDefault)
        {
            this.source = source;
            this.index = index;
            this.defaultValue = defaultValue;
            this.hasDefault = hasDefault;
        }

        public void Subscribe(ISubscriber<T> s)
        {
            source.Subscribe(new ElementAtSubscriber(s, index, defaultValue, hasDefault));
        }

        sealed class ElementAtSubscriber : DeferredScalarSubscriber<T, T>
        {
            readonly long index;

            readonly T defaultValue;

            readonly bool hasDefault;

            long i;

            bool done;

            public ElementAtSubscriber(ISubscriber<T> actual, long index, T defaultValue, bool hasDefault) : base(actual)
            {
                this.index = index;
                this.defaultValue = defaultValue;
                this.hasDefault = hasDefault;
            }

            public override void OnComplete()
            {
                if (done)
                {
                    return;
                }
                done = true;
                if (hasDefault)
                {
                    Complete(defaultValue);
                }
                else
                {
                    Complete();
                }
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
                long j = i;
                if (j == index)
                {
                    s.Cancel();
                    done = true;
                    Complete(t);
                }
                else
                {
                    i = j + 1;
                }
            }
        }
    }
}
