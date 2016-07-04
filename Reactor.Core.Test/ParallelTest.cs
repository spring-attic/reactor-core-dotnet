using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    //[Timeout(30000)]
    public class ParallelTest
    {
        [Test]
        public void Parallel_Normal()
        {
            Flux.Range(1, 10).Hide().Parallel(2)
                .Sequential().Test().AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        }

        [Test]
        public void Parallel_Normal_Fused()
        {
            Flux.Range(1, 10).Parallel(2)
                .Sequential().Test().AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        }

        [Test]
        public void Parallel_Map()
        {
            Flux.Range(1, 10).Parallel(2)
                .Map(v => v * 2)
                .Sequential()
                .Test()
                .AssertResult(2, 4, 6, 8, 10, 12, 14, 16, 18, 20);
        }

        [Test]
        public void Parallel_Filter()
        {
            Flux.Range(1, 10).Parallel(2)
                .Filter(v => (v & 1) != 0)
                .Sequential()
                .Test()
                .AssertResult(1, 3, 5, 7, 9);
        }
    }
}
