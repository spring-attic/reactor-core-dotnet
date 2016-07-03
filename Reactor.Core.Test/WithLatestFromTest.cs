using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class WithLatestFromTest
    {
        [Test]
        public void WithLatestFrom_Normal()
        {
            var dp1 = new DirectProcessor<int>();
            var dp2 = new DirectProcessor<int>();

            var ts = dp1.WithLatestFrom(dp2, (a, b) => a + b).Test();

            dp1.OnNext(1);

            dp2.OnNext(10);

            dp1.OnNext(2, 3, 4);

            dp2.OnNext(20);

            dp1.OnNext(5);
            dp1.OnComplete();

            ts.AssertResult(12, 13, 14, 25);

            Assert.IsFalse(dp1.HasSubscribers, "dp1 has subscribers?");
            Assert.IsFalse(dp2.HasSubscribers, "dp2 has subscribers?");
        }
    }
}
