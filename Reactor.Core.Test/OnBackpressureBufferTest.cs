using NUnit.Framework;
using Reactor.Core.flow;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class OnBackpressureBufferTest
    {
        [Test]
        public void OnBackpressureBuffer_Normal()
        {
            Flux.Range(1, 10).OnBackpressureBuffer()
                .Test().AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        }

        [Test]
        public void OnBackpressureBuffer_Fused()
        {
            Flux.Range(1, 10).OnBackpressureBuffer()
                .Test(fusionMode: FuseableHelper.ANY)
                .AssertFusionMode(FuseableHelper.ASYNC)
                .AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        }
        [Test]
        public void OnBackpressureBuffer_Conditional()
        {
            Flux.Range(1, 10).OnBackpressureBuffer()
                .Filter(v => true)
                .Test().AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        }

        [Test]
        public void OnBackpressureBuffer_Conditional_Fused()
        {
            Flux.Range(1, 10).OnBackpressureBuffer()
                .Filter(v => true)
                .Test(fusionMode: FuseableHelper.ANY)
                .AssertFusionMode(FuseableHelper.ASYNC)
                .AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        }

        [Test]
        public void OnBackpressureBuffer_Backpressured()
        {
            var ts = Flux.Range(1, 10).OnBackpressureBuffer()
                .Test(0);

            ts.AssertNoEvents();

            ts.Request(2);

            ts.AssertValues(1, 2);

            ts.Request(5);

            ts.AssertValues(1, 2, 3, 4, 5, 6, 7);

            ts.Request(3);

            ts.AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        }

    }
}
