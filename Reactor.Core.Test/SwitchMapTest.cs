using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class SwitchMapTest
    {
        [Test]
        public void SwitchMap_Normal()
        {
            var dp = new DirectProcessor<int>();

            var ts = dp.SwitchMap(v => Flux.Range(v, 2).Hide())
                .Test();

            dp.OnNext(1, 2, 3);
            dp.OnComplete();

            ts.AssertResult(1, 2, 2, 3, 3, 4);
        }

        [Test]
        public void SwitchMap_Normal_Fused()
        {
            var dp = new DirectProcessor<int>();

            var ts = dp.SwitchMap(v => Flux.Range(v, 2))
                .Test();

            dp.OnNext(1, 2, 3);
            dp.OnComplete();

            ts.AssertResult(1, 2, 2, 3, 3, 4);
        }

        [Test]
        public void SwitchMap_Normal_Backpressured()
        {
            var dp = new DirectProcessor<int>();

            var ts = dp.SwitchMap(v => Flux.Range(v, 2))
                .Test(initialRequest: 1);

            dp.OnNext(1);

            dp.OnNext(2);

            ts.Request(1);

            dp.OnNext(3);

            ts.Request(1);

            dp.OnComplete();

            ts.Request(1);

            ts.AssertResult(1, 2, 3, 4);

        }

    }
}
