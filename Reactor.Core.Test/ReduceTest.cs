using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class ReduceTest
    {
        [Test]
        public void Reduce_Normal()
        {
            Flux.Range(1, 10).Reduce((a, b) => a + b).Test().AssertResult(55);
        }

        [Test]
        public void Reduce_InitialValue()
        {
            Flux.Range(1, 10).Reduce(10, (a, b) => a + b).Test().AssertResult(65);
        }

    }
}
