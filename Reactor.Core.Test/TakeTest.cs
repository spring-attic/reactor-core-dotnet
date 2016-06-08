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
    }
}
