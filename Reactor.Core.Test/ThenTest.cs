using NUnit.Framework;
using Reactor.Core.flow;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class ThenTest
    {
        [Test]
        public void Then_EmptyVoid()
        {
            Flux.Range(1, 10).Then(Mono.Empty<Void>())
                .Test().AssertResult();
        }

        [Test]
        public void Then_EmptyVoid_Fused()
        {
            Flux.Range(1, 10).Then(Mono.Empty<Void>())
                .Test(fusionMode: FuseableHelper.ANY)
                .AssertFusionMode(FuseableHelper.ASYNC)
                .AssertResult();
        }
    }
}
