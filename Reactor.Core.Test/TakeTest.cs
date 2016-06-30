using System;
using Reactor.Core.flow;
using Reactor.Core.scheduler;
using NUnit.Framework;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class TakeTest
    {
        [Test]
        public void Take_Fused_Exact_Boundary_Backpressure()
        {

            var ts = Flux.Range(1, 2)
                .Take(1)
                .PublishOn(ImmediateScheduler.Instance)
                .Test(1, FuseableHelper.ANY);

            ts.AssertResult(1);
        }

        [Test]
        public void Take_Normal()
        {
            Flux.Range(1, 10).Take(5).Test().AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void Take_Normal_Backpressured()
        {
            var ts = Flux.Range(1, 10).Take(5).Test(0L);

            ts.AssertNoEvents();

            ts.Request(1);

            ts.AssertValues(1);

            ts.Request(2);

            ts.AssertValues(1, 2, 3);

            ts.Request(2);

            ts.AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void Take_Exact_Number_Sync_Fused()
        {
            Flux.Range(1, 5).Take(5).Test(fusionMode: FuseableHelper.SYNC)
                .AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void Take_Exact_Number_Async_Fused()
        {
            var up = new UnicastProcessor<int>();
            up.OnNext(1, 2, 3, 4, 5);

            up.Take(5).Test(fusionMode: FuseableHelper.ASYNC)
                .AssertResult(1, 2, 3, 4, 5);
        }
    }
}
