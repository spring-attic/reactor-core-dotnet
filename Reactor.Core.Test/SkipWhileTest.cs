using NUnit.Framework;
using Reactor.Core.flow;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class SkipWhileTest
    {
        [Test]
        public void SkipWhile_Normal()
        {
            Flux.Range(1, 10).SkipWhile(v => v < 6)
                .Test().AssertResult(6, 7, 8, 9, 10);
        }

        [Test]
        public void SkipWhile_Conditional()
        {
            Flux.Range(1, 10).SkipWhile(v => v < 6)
                .Filter(v => true)
                .Test().AssertResult(6, 7, 8, 9, 10);
        }

        [Test]
        public void SkipWhile_Normal_Fused()
        {
            Flux.Range(1, 10).SkipWhile(v => v < 6)
                .Test(fusionMode: FuseableHelper.ANY)
                .AssertFusionMode(FuseableHelper.SYNC)
                .AssertResult(6, 7, 8, 9, 10);
        }

        [Test]
        public void SkipWhile_Conditional_Fused()
        {
            Flux.Range(1, 10).SkipWhile(v => v < 6)
                .Filter(v => true)
                .Test(fusionMode: FuseableHelper.ANY)
                .AssertFusionMode(FuseableHelper.SYNC)
                .AssertResult(6, 7, 8, 9, 10);
        }

    }
}
