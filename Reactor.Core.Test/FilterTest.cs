using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    public class FilterTest
    {
        [Test]
        public void Filter_Normal()
        {
            Flux.Range(1, 5).Filter(v => (v & 1) == 0).Test().AssertResult(2, 4);
        }
    }
}
