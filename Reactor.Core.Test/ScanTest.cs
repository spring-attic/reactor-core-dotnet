using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class ScanTest
    {
        [Test]
        public void Scan_Normal()
        {
            Flux.Range(1, 10).Scan((a, b) => a + b).Test()
                .AssertResult(1, 3, 6, 10, 15, 21, 28, 36, 45, 55);
        }

        [Test]
        public void Scan_InitialValue()
        {
            Flux.Range(1, 10).Scan(10, (a, b) => a + b).Test()
                .AssertResult(10, 11, 13, 16, 20, 25, 31, 38, 46, 55, 65);
        }

        [Test]
        public void Scan_InitialValue_Backpressured()
        {
            var ts = Flux.Range(1, 10).Scan(10, (a, b) => a + b).Test(0);

            ts.AssertNoEvents();

            ts.Request(2);

            ts.AssertValues(10, 11);

            ts.Request(8);

            ts.AssertValues(10, 11, 13, 16, 20, 25, 31, 38, 46, 55);

            ts.Request(1);

            ts.AssertResult(10, 11, 13, 16, 20, 25, 31, 38, 46, 55, 65);
        }
    }
}
