using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Reactor.Core.Test
{
    [TestClass]
    public class DirectProcessorTest
    {
        [TestMethod]
        public void Normal()
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
