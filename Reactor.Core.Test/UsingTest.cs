using NUnit.Framework;
using Reactor.Core.flow;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class UsingTest
    {
        [Test]
        public void Using_Normal()
        {
            Flux.Using(() => 1, s => Flux.Range(1, 5), s => { })
                .Test().AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void Using_Normal_Fused()
        {
            Flux.Using(() => 1, s => Flux.Range(1, 5), s => { })
                .Test(fusionMode: FuseableHelper.ANY)
                .AssertFusionMode(FuseableHelper.SYNC)
                .AssertResult(1, 2, 3, 4, 5);
        }
    }
}
