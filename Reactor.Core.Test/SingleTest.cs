using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class SingleTest
    {
        [Test]
        public void Single_Single()
        {
            Flux.Just(1).Single().Test().AssertResult(1);
        }

        [Test]
        public void Single_Empty_Default()
        {
            Flux.Empty<int>().Single(2).Test().AssertResult(2);
        }

        [Test]
        public void Single_OrEmpty_Empty()
        {
            Flux.Empty<int>().SingleOrEmpty().Test().AssertResult();
        }

        [Test]
        public void Single_OrEmpty_Single()
        {
            Flux.Just(3).SingleOrEmpty().Test().AssertResult(3);
        }

        [Test]
        public void Single_Empty()
        {
            Flux.Empty<int>().Single().Test()
                .AssertNoValues()
                .AssertError(e => e is IndexOutOfRangeException)
                .AssertNotComplete();
        }

        [Test]
        public void Single_Many()
        {
            Flux.Range(1, 10).Single().Test()
                .AssertNoValues()
                .AssertError(e => e is IndexOutOfRangeException)
                .AssertNotComplete();
        }
    }
}
