using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class MaterializeTest
    {
        [Test]
        public void Materialize_Normal()
        {
            Flux.Range(1, 5).Materialize()
                .Test().AssertValueCount(6).AssertComplete();
        }

        [Test]
        public void Materialize_Dematerialize()
        {
            Flux.Range(1, 5).Materialize().Dematerialize()
                .Test().AssertResult(1, 2, 3, 4, 5);
        }
    }
}
