using System;
using Reactor.Core.flow;
using Reactor.Core.scheduler;
using NUnit.Framework;
using System.Collections.Generic;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class ConcatMapTest
    {
        [Test]
        public void ConcatMap_Normal()
        {
            Flux.Range(1, 3).Hide().ConcatMap(v => Flux.Range(1, 3))
                .Test().AssertResult(1, 2, 3, 1, 2, 3, 1, 2, 3);
        }

        [Test]
        public void ConcatMap_Long()
        {
            Flux.Range(1, Flux.BufferSize * 2).Hide().ConcatMap(v => Flux.Range(1, 3))
                .Test().AssertValueCount(6 * Flux.BufferSize).AssertComplete();
        }

        [Test]
        public void ConcatMap_Normal_Fused()
        {
            Flux.Range(1, 3).ConcatMap(v => Flux.Range(1, 3))
                .Test().AssertResult(1, 2, 3, 1, 2, 3, 1, 2, 3);
        }

        [Test]
        public void ConcatMap_Long_Fused()
        {
            Flux.Range(1, Flux.BufferSize * 2).ConcatMap(v => Flux.Range(1, 3))
                .Test().AssertValueCount(6 * Flux.BufferSize).AssertComplete();
        }

        [Test]
        public void ConcatMap_Boundary_Error()
        {
            var ts = Flux.Range(1, 3).Hide()
                .ConcatMap(v => v != 2 ? Flux.Range(1, 3) : Flux.Error<int>(new Exception("Forced failure")), ConcatErrorMode.Boundary)
                .Test();

            ts.AssertValues(1, 2, 3)
            .AssertError(e => e.Message.Equals("Forced failure"))
            .AssertNotComplete();
        }

        [Test]
        public void ConcatMap_Boundary_Error_Fused()
        {
            var ts = Flux.Range(1, 3)
                .ConcatMap(v => v != 2 ? Flux.Range(1, 3) : Flux.Error<int>(new Exception("Forced failure")), ConcatErrorMode.Boundary)
                .Test();

            ts.AssertValues(1, 2, 3)
            .AssertError(e => e.Message.Equals("Forced failure"))
            .AssertNotComplete();
        }

        [Test]
        public void ConcatMap_End_Error()
        {
            var ts = Flux.Range(1, 3).Hide()
                .ConcatMap(v => v != 2 ? Flux.Range(1, 3) : Flux.Error<int>(new Exception("Forced failure")), ConcatErrorMode.End)
                .Test();

            ts.AssertValues(1, 2, 3, 1, 2, 3)
            .AssertError(e => e.Message.Equals("Forced failure"))
            .AssertNotComplete();
        }

        [Test]
        public void ConcatMap_End_Error_Fused()
        {
            var ts = Flux.Range(1, 3)
                .ConcatMap(v => v != 2 ? Flux.Range(1, 3) : Flux.Error<int>(new Exception("Forced failure")), ConcatErrorMode.End)
                .Test();

            ts.AssertValues(1, 2, 3, 1, 2, 3)
            .AssertError(e => e.Message.Equals("Forced failure"))
            .AssertNotComplete();
        }

        [Test]
        public void ConcatMap_Conditional_Normal()
        {
            Flux.Range(1, 3).Hide().ConcatMap(v => Flux.Range(1, 3)).Filter(v => true)
                .Test().AssertResult(1, 2, 3, 1, 2, 3, 1, 2, 3);
        }

        [Test]
        public void ConcatMap_Conditional_Long()
        {
            Flux.Range(1, Flux.BufferSize * 2).Hide().ConcatMap(v => Flux.Range(1, 3)).Filter(v => true)
                .Test().AssertValueCount(6 * Flux.BufferSize).AssertComplete();
        }

        [Test]
        public void ConcatMap_Conditional_Normal_Fused()
        {
            Flux.Range(1, 3).ConcatMap(v => Flux.Range(1, 3)).Filter(v => true)
                .Test().AssertResult(1, 2, 3, 1, 2, 3, 1, 2, 3);
        }

        [Test]
        public void ConcatMap_Conditional_Long_Fused()
        {
            Flux.Range(1, Flux.BufferSize * 2).ConcatMap(v => Flux.Range(1, 3)).Filter(v => true)
                .Test().AssertValueCount(6 * Flux.BufferSize).AssertComplete();
        }

        [Test]
        public void ConcatMap_Conditional_Boundary_Error()
        {
            var ts = Flux.Range(1, 3).Hide()
                .ConcatMap(v => v != 2 ? Flux.Range(1, 3) : Flux.Error<int>(new Exception("Forced failure")), ConcatErrorMode.Boundary)
                .Filter(v => true).Test();

            ts.AssertValues(1, 2, 3)
            .AssertError(e => e.Message.Equals("Forced failure"))
            .AssertNotComplete();
        }

        [Test]
        public void ConcatMap_Conditional_Boundary_Error_Fused()
        {
            var ts = Flux.Range(1, 3)
                .ConcatMap(v => v != 2 ? Flux.Range(1, 3) : Flux.Error<int>(new Exception("Forced failure")), ConcatErrorMode.Boundary)
                .Filter(v => true).Test();

            ts.AssertValues(1, 2, 3)
            .AssertError(e => e.Message.Equals("Forced failure"))
            .AssertNotComplete();
        }

        [Test]
        public void ConcatMap_Conditional_End_Error()
        {
            var ts = Flux.Range(1, 3).Hide()
                .ConcatMap(v => v != 2 ? Flux.Range(1, 3) : Flux.Error<int>(new Exception("Forced failure")), ConcatErrorMode.End)
                .Filter(v => true).Test();

            ts.AssertValues(1, 2, 3, 1, 2, 3)
            .AssertError(e => e.Message.Equals("Forced failure"))
            .AssertNotComplete();
        }

        [Test]
        public void ConcatMap_Conditional_End_Error_Fused()
        {
            var ts = Flux.Range(1, 3)
                .ConcatMap(v => v != 2 ? Flux.Range(1, 3) : Flux.Error<int>(new Exception("Forced failure")), ConcatErrorMode.End)
                .Filter(v => true).Test();

            ts.AssertValues(1, 2, 3, 1, 2, 3)
            .AssertError(e => e.Message.Equals("Forced failure"))
            .AssertNotComplete();
        }

        [Test]
        public void ConcatMap_Take()
        {
            Flux.Range(1, 1000 * 1000 * 1000).ConcatMap(v => Flux.Just(v))
                .Take(1000)
                .Test().AssertValueCount(1000).AssertComplete();
        }

        static IEnumerable<int> Infinite()
        {
            int count = 0;
            while (true)
            {
                yield return count++;
            }
        }

        [Test]
        public void Concat_Infinite()
        {
            Flux.From(Infinite()).ConcatWith(Flux.Empty<int>())
                .Take(10)
                .Test().AssertValueCount(10);
        }

    }
}
