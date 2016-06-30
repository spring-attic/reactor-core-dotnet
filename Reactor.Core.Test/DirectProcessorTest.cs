using System;
using NUnit.Framework;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class DirectProcessorTest
    {
        [Test]
        public void DirectProcessor_Normal()
        {
            DirectProcessor<int> dp = new DirectProcessor<int>();

            var ts = dp.Test();

            ts.AssertSubscribed()
                .AssertNoEvents();

            dp.OnNext(1);
            dp.OnNext(2);
            dp.OnComplete();

            Assert.IsFalse(dp.HasSubscribers);

            ts.AssertResult(1, 2);
        }
    }
}
