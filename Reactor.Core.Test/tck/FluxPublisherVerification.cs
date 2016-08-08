using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Reactive.Streams;
using Reactive.Streams.TCK;
using Reactive.Streams.TCK.Support;

namespace Reactor.Core.Test.tck
{
    [TestFixture]
    abstract class FluxPublisherVerification<T> : PublisherVerification<T>
    {
        protected FluxPublisherVerification() : base(new TestEnvironment())
        {
        }

        public override IPublisher<T> CreateFailedPublisher() => Flux.Error<T>(new TestException());

        protected IEnumerable<int> Enumerate(long elements) => Enumerate(elements > int.MaxValue, elements);

        protected IEnumerable<int> Enumerate(bool useInfinite, long elements)
            => useInfinite
                ? new InfiniteEnumerable()
                : Enumerable.Range(0, (int)elements);


        private sealed class InfiniteEnumerable : IEnumerable<int>
        {
            public IEnumerator<int> GetEnumerator() => new InfiniteEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            private sealed class InfiniteEnumerator : IEnumerator<int>
            {
                private int _current;

                public void Dispose()
                {

                }

                public bool MoveNext()
                {
                    _current++;
                    return true;
                }

                public void Reset() => _current = 0;

                public int Current => _current;

                object IEnumerator.Current => Current;
            }
        }
    }
}
