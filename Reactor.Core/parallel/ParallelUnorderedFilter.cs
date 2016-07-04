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
    sealed class ParallelUnorderedFilter<T> : ParallelUnorderedFlux<T>
    {
        readonly IParallelFlux<T> source;

        readonly Func<T, bool> predicate;

        public override int Parallelism
        {
            get
            {
                return source.Parallelism;
            }
        }

        internal ParallelUnorderedFilter(IParallelFlux<T> source, Func<T, bool> predicate)
        {
            this.source = source;
            this.predicate = predicate;
        }

        public override void Subscribe(ISubscriber<T>[] subscribers)
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
                    parents[i] = new ParallelFilterConditionalSubscriber(
                        (IConditionalSubscriber<T>)s, predicate);
                }
                else
                {
                    parents[i] = new ParallelFilterSubscriber(s, predicate);
                }
            }
            source.Subscribe(parents);
        }

        sealed class ParallelFilterSubscriber : BasicSubscriber<T, T>, IConditionalSubscriber<T>
        {
            readonly Func<T, bool> predicate;

            public ParallelFilterSubscriber(ISubscriber<T> actual, Func<T, bool> predicate) : base(actual)
            {
                this.predicate = predicate;
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
                if (!TryOnNext(t))
                {
                    s.Request(1);
                }
            }

            public bool TryOnNext(T t)
            {
                if (done)
                {
                    return false;
                }

                bool b;

                try
                {
                    b = predicate(t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return false;
                }

                if (b)
                {
                    actual.OnNext(t);
                    return true;
                }
                return false;
            }
        }

        sealed class ParallelFilterConditionalSubscriber : BasicConditionalSubscriber<T, T>
        {
            readonly Func<T, bool> predicate;

            public ParallelFilterConditionalSubscriber(IConditionalSubscriber<T> actual, Func<T, bool> predicate) : base(actual)
            {
                this.predicate = predicate;
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
                if (!TryOnNext(t))
                {
                    s.Request(1);
                }
            }

            public override bool TryOnNext(T t)
            {
                if (done)
                {
                    return false;
                }

                bool b;

                try
                {
                    b = predicate(t);
                }
                catch (Exception ex)
                {
                    Fail(ex);
                    return false;
                }

                if (b)
                {
                    return actual.TryOnNext(t);
                }
                return false;
            }
        }
    }
}
