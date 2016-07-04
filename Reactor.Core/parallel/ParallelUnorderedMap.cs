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
    sealed class ParallelUnorderedMap<T, R> : ParallelUnorderedFlux<R>
    {
        readonly IParallelFlux<T> source;

        readonly Func<T, R> mapper;

        public override int Parallelism
        {
            get
            {
                return source.Parallelism;
            }
        }

        internal ParallelUnorderedMap(IParallelFlux<T> source, Func<T, R> mapper)
        {
            this.source = source;
            this.mapper = mapper;
        }

        public override void Subscribe(ISubscriber<R>[] subscribers)
        {
            if (!this.Validate(subscribers))
            {
                return;
            }
            int n = subscribers.Length;

            var parents = new ISubscriber<T>[n];

            for (int i = 0; i < n; i++)
            {
                var s = subscribers[i];
                if (s is IConditionalSubscriber<T>)
                {
                    parents[i] = new ParallelMapConditionalSubscriber(
                        (IConditionalSubscriber<R>)s, mapper);
                }
                else
                {
                    parents[i] = new ParallelMapSubscriber(s, mapper);
                }
            }
            source.Subscribe(parents);
        }

        sealed class ParallelMapSubscriber : BasicSubscriber<T, R>
        {
            readonly Func<T, R> mapper;

            public ParallelMapSubscriber(ISubscriber<R> actual, Func<T, R> mapper) : base(actual)
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

            public override void OnNext(T t)
            {
                if (done)
                {
                    return;
                }

                R v;

                try
                {
                    v = mapper(t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return;
                }
                    

                actual.OnNext(v);
            }
        }

        sealed class ParallelMapConditionalSubscriber : BasicConditionalSubscriber<T, R>
        {
            readonly Func<T, R> mapper;

            public ParallelMapConditionalSubscriber(IConditionalSubscriber<R> actual, Func<T, R> mapper) : base(actual)
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

            public override void OnNext(T t)
            {
                if (done)
                {
                    return;
                }

                R v;

                try
                {
                    v = mapper(t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return;
                }


                actual.OnNext(v);
            }

            public override bool TryOnNext(T t)
            {
                if (done)
                {
                    return false;
                }

                R v;

                try
                {
                    v = mapper(t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return false;
                }


                return actual.TryOnNext(v);
            }
        }
    }
}
