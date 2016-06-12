using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Reactor.Core.Test
{
    [TestFixture]
    public class BufferTest
    {
        [Test]
        public void Buffer_Exact()
        {
            Flux.Range(1, 5).Buffer(2).Test().AssertResult(
                new List<int>(new[] { 1, 2 }),
                new List<int>(new[] { 3, 4 }),
                new List<int>(new[] { 5 })
            );
        }

        [Test]
        public void Buffer_Skip()
        {
            Flux.Range(1, 5).Buffer(2, 3).Test().AssertResult(
                new List<int>(new[] { 1, 2 }),
                new List<int>(new[] { 4, 5 })
            );
        }

        [Test]
        public void Buffer_Overlap()
        {
            Flux.Range(1, 5).Buffer(2, 1).Test().AssertResult(
                new List<int>(new[] { 1, 2 }),
                new List<int>(new[] { 2, 3 }),
                new List<int>(new[] { 3, 4 }),
                new List<int>(new[] { 4, 5 }),
                new List<int>(new[] { 5 })
            );
        }

        [Test]
        public void Buffer_Overlap_Backpressured()
        {
            var ts = Flux.Range(1, 5).Buffer(2, 1).Test(2);

            ts.AssertValues(
                new List<int>(new[] { 1, 2 }),
                new List<int>(new[] { 2, 3 })
            );

            ts.Request(1);

            ts.AssertValues(
                new List<int>(new[] { 1, 2 }),
                new List<int>(new[] { 2, 3 }),
                new List<int>(new[] { 3, 4 })
            );

            ts.Request(1);

            ts.AssertValues(
                new List<int>(new[] { 1, 2 }),
                new List<int>(new[] { 2, 3 }),
                new List<int>(new[] { 3, 4 }),
                new List<int>(new[] { 4, 5 })
            );

            ts.Request(1);

            ts.AssertResult(
                new List<int>(new[] { 1, 2 }),
                new List<int>(new[] { 2, 3 }),
                new List<int>(new[] { 3, 4 }),
                new List<int>(new[] { 4, 5 }),
                new List<int>(new[] { 5 })
            );
        }

    }
}
