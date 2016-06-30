using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class HasElementsTest
    {
        [Test]
        public void HasElements_Normal()
        {
            Flux.Range(1, 5).HasElements().Test().AssertResult(true);
        }

        [Test]
        public void HasElements_Empty()
        {
            Flux.Empty<int>().HasElements().Test().AssertResult(false);
        }
    }
}
