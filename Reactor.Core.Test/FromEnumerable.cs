using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class FromEnumerableTest
    {
        [Test]
        public void FromEnumerable_Normal()
        {
            IEnumerable<int> en = new List<int>(new int[] { 1, 2, 3, 4, 5 });
            Flux.From(en)
                .Test().AssertResult(1, 2, 3, 4, 5);
        }
    }
}
