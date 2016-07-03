using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class SkipUntilTest
    {
        [Test]
        public void SkipUntil_Normal()
        {
            var dp1 = new DirectProcessor<int>();
            var dp2 = new DirectProcessor<int>();

            var ts = dp1.SkipUntil(dp2).Test();

            dp1.OnNext(1, 2, 3);

            dp2.OnNext(1);

            dp1.OnNext(4, 5, 6);
            dp1.OnComplete();

            Assert.IsFalse(dp2.HasSubscribers, "Has subscribers?!");

            ts.AssertResult(4, 5, 6);
        }
    }
}
