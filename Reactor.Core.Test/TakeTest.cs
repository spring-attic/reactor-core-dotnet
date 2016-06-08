using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Reactor.Core.flow;
using Reactor.Core.scheduler;

namespace Reactor.Core.Test
{
    [TestClass]
    public class TakeTest
    {
        [TestMethod]
        public void Take_Fused_Exact_Boundary_Backpressure()
        {

            var ts = Flux.Range(1, 2)
                .Take(1)
                .PublishOn(ImmediateScheduler.Instance)
                .Test(1, FuseableHelper.ANY);

            ts.AssertResult(1);
        }

        [TestMethod]
        public void Take_Normal()
        {
            Flux.Range(1, 10).Take(5).Test().AssertResult(1, 2, 3, 4, 5);
        }

        [TestMethod]
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
    }
}
