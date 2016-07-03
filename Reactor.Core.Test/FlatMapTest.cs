using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class FlatMapTest
    {
        [Test]
        public void FlatMap_Normal()
        {
            Flux.Range(1, 5)
                .FlatMap(v => Flux.Range(v, 2))
                .Test().AssertResult(1, 2, 2, 3, 3, 4, 4, 5, 5, 6);
        }

        [Test]
        public void FlatMap_Error()
        {
            Flux.Error<int>(new Exception("Forced failure"))
                .FlatMap(v => Flux.Just(1))
                .Test()
                .AssertNoValues().AssertErrorMessage("Forced failure").AssertNotComplete();
        }

        [Test]
        public void FlatMap_Mono_Enumerable()
        {
            Mono.Just(1).FlatMap(v => new List<int>(new[] { 1, 2, 3, 4, 5 }))
                .Test().AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void FlatMap_Mono_Mono()
        {
            Mono.Just(1).FlatMap(v => Mono.Just(v + 1))
                .Test().AssertResult(2);
        }

        [Test]
        public void FlatMap_Mono_Publisher()
        {
            Mono.Just(1).FlatMap(v => Flux.Range(v, 2))
                .Test().AssertResult(1, 2);
        }

    }
}
