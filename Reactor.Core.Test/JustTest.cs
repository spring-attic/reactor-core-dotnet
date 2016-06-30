using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class JustTest
    {
        [Test]
        public void Just_Normal()
        {
            Assert.AreEqual(1, Flux.Just(1).BlockLast());
        }
    }
}
