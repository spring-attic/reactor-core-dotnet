using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class OnBackpressureLatestTest
    {
        [Test]
        public void OnBackpressureLatest_Normal()
        {
            var ps = new DirectProcessor<int>();

            var ts = ps.OnBackpressureLatest().Test(0);

            ps.OnNext(1);
            ps.OnNext(2);

            ts.Request(1);

            ps.OnNext(3);

            ts.Request(2);

            ps.OnNext(4);
            ps.OnComplete();

            ts.AssertResult(2, 3, 4);
        }
    }
}
