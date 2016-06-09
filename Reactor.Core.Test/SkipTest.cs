using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    public class SkipTest
    {
        [Test]
        public void Skip_Normal()
        {
            Flux.Range(1, 5).Skip(3).Test().AssertResult(4, 5);
        }
    }
}
