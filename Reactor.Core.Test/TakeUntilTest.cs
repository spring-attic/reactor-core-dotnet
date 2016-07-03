using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class TakeUntilTest
    {
        [Test]
        public void TakeUntil_Normal()
        {
            var dp1 = new DirectProcessor<int>();
            var dp2 = new DirectProcessor<int>();

            var ts = dp1.TakeUntil(dp2).Test();

            dp1.OnNext(1, 2, 3);

            dp2.OnNext(1);

            ts.AssertResult(1, 2, 3);

            Assert.IsFalse(dp1.HasSubscribers, "dp1 has Subscribers?!");
            Assert.IsFalse(dp2.HasSubscribers, "dp2 has Subscribers?!");
        }
    }
}
