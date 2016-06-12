using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    public class AllTest
    {
        [Test]
        public void All_Normal()
        {
            Flux.Range(1, 5).All(v => v < 6).Test().AssertResult(true);
        }

        [Test]
        public void All_Empty()
        {
            Flux.Empty<int>().All(v => v == 3).Test().AssertResult(true);
        }

        [Test]
        public void All_Normal_2()
        {
            Flux.Range(1, 5).All(v => v < 5).Test().AssertResult(false);
        }
    }
}
