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

namespace Reactor.Core.parallel
{
    sealed class ParallelOrderedMap<T, R> : ParallelOrderedFlux<R>
    {
        readonly ParallelOrderedFlux<T> source;

        readonly Func<T, R> mapper;

        public override int Parallelism
        {
            get
            {
                return source.Parallelism;
            }
        }

        internal ParallelOrderedMap(ParallelOrderedFlux<T> source, Func<T, R> mapper)
        {
            this.source = source;
            this.mapper = mapper;
        }

        public override void SubscribeMany(ISubscriber<IOrderedItem<R>>[] subscribers)
        {
            if (!this.Validate(subscribers))
            {
                return;
            }
            int n = subscribers.Length;

            var parents = new ISubscriber<IOrderedItem<T>>[n];

            for (int i = 0; i < n; i++)
            {
                var s = subscribers[i];
                if (s is IConditionalSubscriber<IOrderedItem<R>>)
                {
                    parents[i] = new ParallelMapConditionalSubscriber(
                        (IConditionalSubscriber<IOrderedItem<R>>)s, mapper);
                }
                else
                {
                    parents[i] = new ParallelMapSubscriber(s, mapper);
                }
            }
            source.SubscribeMany(parents);
        }

        sealed class ParallelMapSubscriber : BasicSubscriber<IOrderedItem<T>, IOrderedItem<R>>
        {
            readonly Func<T, R> mapper;

            public ParallelMapSubscriber(ISubscriber<IOrderedItem<R>> actual, Func<T, R> mapper) : base(actual)
            {
                this.mapper = mapper;
            }

            public override void OnComplete()
            {
                Complete();
            }

            public override void OnError(Exception e)
            {
                Error(e);
            }

            public override void OnNext(IOrderedItem<T> t)
            {
                if (done)
                {
                    return;
                }

                R v;

                try
                {
                    v = mapper(t.Value);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return;
                }
                    

                actual.OnNext(t.Replace(v));
            }
        }

        sealed class ParallelMapConditionalSubscriber : BasicConditionalSubscriber<IOrderedItem<T>, IOrderedItem<R>>
        {
            readonly Func<T, R> mapper;

            public ParallelMapConditionalSubscriber(IConditionalSubscriber<IOrderedItem<R>> actual, Func<T, R> mapper) : base(actual)
            {
                this.mapper = mapper;
            }

            public override void OnComplete()
            {
                Complete();
            }

            public override void OnError(Exception e)
            {
                Error(e);
            }

            public override void OnNext(IOrderedItem<T> t)
            {
                if (done)
                {
                    return;
                }

                R v;

                try
                {
                    v = mapper(t.Value);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return;
                }


                actual.OnNext(t.Replace(v));
            }

            public override bool TryOnNext(IOrderedItem<T> t)
            {
                if (done)
                {
                    return false;
                }

                R v;

                try
                {
                    v = mapper(t.Value);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return false;
                }


                return actual.TryOnNext(t.Replace(v));
            }
        }
    }
}
