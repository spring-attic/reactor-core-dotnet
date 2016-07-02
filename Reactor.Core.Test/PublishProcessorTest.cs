using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class PublishProcessorTest
    {
        [Test]
        public void PublishProcessor_Normal()
        {
            PublishProcessor<int> pp = new PublishProcessor<int>();

            var ts1 = pp.Test(0);
            var ts2 = pp.Test(0);

            ts1.AssertNoEvents();
            ts2.AssertNoValues();

            Flux.Range(1, 10).Subscribe(pp);

            ts1.AssertNoEvents();
            ts2.AssertNoValues();

            ts1.Request(1);

            ts1.AssertNoEvents();
            ts2.AssertNoValues();

            ts2.Request(10);

            ts1.AssertValues(1);
            ts2.AssertValues(1);

            ts1.Request(9);

            ts1.AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
            ts2.AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        }
    }
}
