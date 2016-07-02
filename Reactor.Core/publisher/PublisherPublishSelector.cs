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
    sealed class PublisherPublishSelector<T, R> : IFlux<R>
    {
        readonly IPublisher<T> source;

        readonly Func<IFlux<T>, IPublisher<R>> transformer;

        readonly int prefetch;

        internal PublisherPublishSelector(IPublisher<T> source, 
            Func<IFlux<T>, IPublisher<R>> transformer, int prefetch)
        {
            this.source = source;
            this.transformer = transformer;
            this.prefetch = prefetch;
        }

        public void Subscribe(ISubscriber<R> s)
        {
            var pp = new PublishProcessor<T>(prefetch);

            IPublisher<R> o;

            try
            {
                o = transformer(pp);
            }
            catch (Exception ex)
            {
                ExceptionHelper.ThrowIfFatal(ex);
                EmptySubscription<R>.Error(s, ex);
                return;
            }
            
            if (s is IConditionalSubscriber<R>)
            {
                o.Subscribe(new PublishSelectorConditionalSubscriber(pp, (IConditionalSubscriber<R>)s));
            }
            else
            {
                o.Subscribe(new PublishSelectorSubscriber(pp, s));
            }

            source.Subscribe(pp);
        }

        sealed class PublishSelectorSubscriber : BasicFuseableSubscriber<R, R>
        {
            readonly PublishProcessor<T> processor;

            public PublishSelectorSubscriber(PublishProcessor<T> processor, ISubscriber<R> actual) : base(actual)
            {
                this.processor = processor;
            }

            public override void Cancel()
            {
                base.Cancel();
                processor.Dispose();
            }

            public override void OnComplete()
            {
                try { 
                    actual.OnComplete();
                }
                finally
                {
                    processor.Dispose();
                }
            }

            public override void OnError(Exception e)
            {
                try
                {
                    actual.OnError(e);
                }
                finally
                {
                    processor.Dispose();
                }
            }

            public override void OnNext(R t)
            {
                actual.OnNext(t);
            }

            public override bool Poll(out R value)
            {
                return qs.Poll(out value);
            }

            public override int RequestFusion(int mode)
            {
                return qs.RequestFusion(mode);
            }
        }

        sealed class PublishSelectorConditionalSubscriber : BasicFuseableConditionalSubscriber<R, R>
        {
            readonly PublishProcessor<T> processor;

            public PublishSelectorConditionalSubscriber(PublishProcessor<T> processor, IConditionalSubscriber<R> actual) : base(actual)
            {
                this.processor = processor;
            }

            public override void Cancel()
            {
                base.Cancel();
                processor.Dispose();
            }

            public override void OnComplete()
            {
                try
                {
                    actual.OnComplete();
                }
                finally
                {
                    processor.Dispose();
                }
            }

            public override void OnError(Exception e)
            {
                try
                {
                    actual.OnError(e);
                }
                finally
                {
                    processor.Dispose();
                }
            }

            public override void OnNext(R t)
            {
                actual.OnNext(t);
            }

            public override bool Poll(out R value)
            {
                return qs.Poll(out value);
            }

            public override int RequestFusion(int mode)
            {
                return qs.RequestFusion(mode);
            }

            public override bool TryOnNext(R t)
            {
                return actual.TryOnNext(t);
            }
        }
    }
}
