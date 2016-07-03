using NUnit.Framework;
using Reactor.Core.flow;
using Reactor.Core.scheduler;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class SubscribeOnTest
    {
        [Test]
        public void SubscribeOn_Normal()
        {
            Flux.Range(1, 10).SubscribeOn(DefaultScheduler.Instance)
                .Test().AwaitTerminalEvent()
                .AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        }

        [Test]
        public void SubscribeOn_Scalar()
        {
            Flux.Just(1).SubscribeOn(DefaultScheduler.Instance)
                .Test().AwaitTerminalEvent()
                .AssertResult(1);
        }

        [Test]
        public void SubscribeOn_Callable()
        {
            Flux.From(() => 1).SubscribeOn(DefaultScheduler.Instance)
                .Test().AwaitTerminalEvent()
                .AssertResult(1);
        }

        [Test]
        public void SubscribeOn_ScalarFused()
        {
            Flux.Just(1).SubscribeOn(DefaultScheduler.Instance)
                .Test(fusionMode: FuseableHelper.ANY)
                .AwaitTerminalEvent()
                .AssertFusionMode(FuseableHelper.ASYNC)
                .AssertResult(1);
        }
    }
}
