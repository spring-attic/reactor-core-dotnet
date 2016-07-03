using NUnit.Framework;
using Reactor.Core.flow;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class ThenManyTest
    {
        [Test]
        public void ThenMany_Normal()
        {
            Flux.Range(1, 10)
                .ThenMany(Flux.Range(11, 10))
                .Test()
                .AssertResult(11, 12, 13, 14, 15, 16, 17, 18, 19, 20);
        }

        [Test]
        public void ThenMany_Conditional()
        {
            Flux.Range(1, 10)
                .ThenMany(Flux.Range(11, 10))
                .Filter(v => true)
                .Test()
                .AssertResult(11, 12, 13, 14, 15, 16, 17, 18, 19, 20);
        }
    }
}
