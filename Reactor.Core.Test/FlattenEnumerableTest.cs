using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class FlattenEnumerableTest
    {
        [Test]
        public void FlattenEnumerable_Normal()
        {
            Flux.From(
                new List<int>(new[] { 1, 2, 3 }),
                new List<int>(),
                new List<int>(new[] { 5 }),
                new List<int>(new[] { 6, 7 })
            ).ConcatMap(v => v)
            .Test()
            .AssertResult(1, 2, 3, 5, 6, 7);
        }

        [Test]
        public void FlattenEnumerable_Normal_Backpressure()
        {
            var ts = Flux.From(
                new List<int>(new[] { 1, 2, 3 }),
                new List<int>(),
                new List<int>(new[] { 5 }),
                new List<int>(new[] { 6, 7 })
            ).ConcatMap(v => v)
            .Test(0);

            ts.AssertNoValues();

            ts.Request(2);

            ts.AssertValues(1, 2);

            ts.Request(2);

            ts.AssertValues(1, 2, 3, 5);

            ts.Request(2);

            ts.AssertResult(1, 2, 3, 5, 6, 7);
        }
    }
}
