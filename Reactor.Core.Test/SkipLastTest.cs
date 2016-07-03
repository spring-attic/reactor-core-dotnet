using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class SkipLastTest
    {
        [Test]
        public void SkipLast_Longer()
        {
            Flux.Range(1, 10).SkipLast(5)
                .Test().AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void SkipLast_Shorter()
        {
            Flux.Range(1, 10).SkipLast(15)
                .Test().AssertResult();
        }

        [Test]
        public void SkipLast_Longer_Conditional()
        {
            Flux.Range(1, 10).SkipLast(5)
                .Filter(v => true)
                .Test().AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void SkipLast_Shorter_Conditional()
        {
            Flux.Range(1, 10).SkipLast(15)
                .Filter(v => true)
                .Test().AssertResult();
        }
    }
}
