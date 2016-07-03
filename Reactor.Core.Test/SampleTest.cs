using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class SampleTest
    {
        [Test]
        public void Sample_Normal()
        {
            var dp1 = new DirectProcessor<int>();
            var dp2 = new DirectProcessor<int>();

            var ts = dp1.Sample(dp2).Test();

            dp1.OnNext(1);
            dp1.OnNext(2);

            dp2.OnNext(1);
            dp2.OnNext(3);

            dp1.OnNext(3);

            dp2.OnComplete();

            ts.AssertResult(2, 3);

            Assert.IsFalse(dp1.HasSubscribers, "dp1 has subscribers?!");
            Assert.IsFalse(dp2.HasSubscribers, "dp2 has subscribers?!");
        }
    }
}
