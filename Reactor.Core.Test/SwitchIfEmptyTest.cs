using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class SwitchIfEmptyTest
    {
        [Test]
        public void SwitchIfEmpty_Empty()
        {
            Flux.Empty<int>().SwitchIfEmpty(Flux.Range(1, 10))
                .Test()
                .AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        }

        [Test]
        public void SwitchIfEmpty_NonEmpty()
        {
            Flux.Range(11, 10).SwitchIfEmpty(Flux.Range(1, 10))
                .Test()
                .AssertResult(11, 12, 13, 14, 15, 16, 17, 18, 19, 20);
        }

        [Test]
        public void SwitchIfEmpty_Conditional_Empty()
        {
            Flux.Empty<int>().SwitchIfEmpty(Flux.Range(1, 10))
                .Filter(v => true)
                .Test()
                .AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        }

        [Test]
        public void SwitchIfEmpty_Conditional_NonEmpty()
        {
            Flux.Range(11, 10).SwitchIfEmpty(Flux.Range(1, 10))
                .Filter(v => true)
                .Test()
                .AssertResult(11, 12, 13, 14, 15, 16, 17, 18, 19, 20);
        }
    }
}
