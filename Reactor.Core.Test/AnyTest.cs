using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    public class AnyTest
    {
        [Test]
        public void Any_Normal()
        {
            Flux.Range(1, 5).Any(v => v == 3).Test().AssertResult(true);
        }

        [Test]
        public void Any_Empty()
        {
            Flux.Empty<int>().Any(v => v == 3).Test().AssertResult(false);
        }

        [Test]
        public void Any_Normal_2()
        {
            Flux.Range(1, 5).Any(v => v == 7).Test().AssertResult(false);
        }
    }
}
