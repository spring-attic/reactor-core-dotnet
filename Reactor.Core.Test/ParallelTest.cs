using NUnit.Framework;
using Reactor.Core.scheduler;
using System;
using System.Collections.Generic;

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
        public void Parallel_Normal_Long()
        {
            Flux.Range(1, 10000).Hide().Parallel(2)
                .Sequential().Test()
                .AssertValueCount(10000)
                .AssertNoError()
                .AssertComplete();
        }

        [Test]
        public void Parallel_Normal_Fused_Long()
        {
            Flux.Range(1, 10000).Parallel(2)
                .Sequential().Test()
                .AssertValueCount(10000)
                .AssertNoError()
                .AssertComplete();
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

        [Test]
        public void Parallel_RunOn_Solo_Loop()
        {
            for (int i = 0; i < 20; i++)
            {
                Parallel_RunOn_Solo();
            }
        }

        [Test]
        public void Parallel_RunOn_Solo()
        {
            var ts = Flux.Range(1, 10000)
                .Parallel(1)
                .RunOn(DefaultScheduler.Instance)
                .Sequential()
                .Test()
                ;

            ts.AwaitTerminalEvent(TimeSpan.FromSeconds(5))
                .AssertValueCount(10000)
                .AssertNoError()
                .AssertComplete();

            HashSet<int> set = new HashSet<int>(ts.Values);

            Assert.AreEqual(10000, set.Count);
        }

        [Test]
        public void Parallel_RunOn_Loop()
        {
            for (int i = 0; i < 20; i++)
            {
                Parallel_RunOn();
            }
        }

        [Test]
        public void Parallel_RunOn()
        {
            var ts = Flux.Range(1, 10000)
                .Parallel(2)
                .RunOn(DefaultScheduler.Instance)
                .Sequential()
                .Test()
                ;

            ts.AwaitTerminalEvent(TimeSpan.FromSeconds(5))
                .AssertValueCount(10000)
                .AssertNoError()
                .AssertComplete();

            HashSet<int> set = new HashSet<int>(ts.Values);

            Assert.AreEqual(10000, set.Count);
        }

        [Test]
        public void Parallel_ReduceAll_Sync()
        {
            for (int j = 1; j <= 32; j++)
            {
                for (int i = 1; i <= 100000; i *= 10)
                {
                    var ts = Flux.Range(1, i)
                        .Map(v => (long)v)
                        .Parallel(j)
                        .Reduce((a, b) => a + b)
                        .Test()
                        ;

                    long result = ((long)i) * ((long)i + 1) / 2;

                    ts.AssertResult(result);
                }
            }
        }

        [Test]
        public void Parallel_ReduceAll_Async()
        {
            for (int j = 1; j <= Environment.ProcessorCount; j++)
            {
                for (int i = 1; i <= 100000; i *= 10)
                {
                    var ts = Flux.Range(1, i)
                        .Map(v => (long)v)
                        .Parallel(j)
                        .RunOn(DefaultScheduler.Instance)
                        .Reduce((a, b) => a + b)
                        .Test()
                        ;

                    long result = ((long)i) * ((long)i + 1) / 2;

                    ts
                        .AwaitTerminalEvent(TimeSpan.FromSeconds(20))
                        .AssertResult(result);
                }
            }
        }


        [Test]
        public void Parallel_Reduce_Sync()
        {
            for (int j = 1; j <= 32; j++)
            {
                for (int i = 1; i <= 100000; i *= 10)
                {
                    var ts = Flux.Range(1, i)
                        .Map(v => (long)v)
                        .Parallel(j)
                        .Reduce(() => 0L, (a, b) => a + b)
                        .Reduce((a, b) => a + b)
                        .Test()
                        ;

                    long result = ((long)i) * ((long)i + 1) / 2;

                    ts.AssertResult(result);
                }
            }
        }

        [Test]
        public void Parallel_Reduce_Async()
        {
            for (int j = 1; j <= Environment.ProcessorCount; j++)
            {
                for (int i = 1; i <= 100000; i *= 10)
                {
                    var ts = Flux.Range(1, i)
                        .Map(v => (long)v)
                        .Parallel(j)
                        .RunOn(DefaultScheduler.Instance)
                        .Reduce(() => 0L, (a, b) => a + b)
                        .Reduce((a, b) => a + b)
                        .Test()
                        ;

                    long result = ((long)i) * ((long)i + 1) / 2;

                    ts
                        .AwaitTerminalEvent(TimeSpan.FromSeconds(20))
                        .AssertResult(result);
                }
            }
        }

        [Test]
        public void Parallel_Ordered_Join_Just()
        {
            for (int j = 1; j <= 32; j++)
            {
                Flux.Just(1)
                .Parallel(j)
                .Sequential()
                .Test()
                .AssertResult(1);
            }
        }

        [Test]
        public void Parallel_Ordered_Join_Empty()
        {
            for (int j = 1; j <= 32; j++)
            {
                Flux.Empty<int>()
                .Parallel(j)
                .Sequential()
                .Test()
                .AssertResult();
            }
        }

        [Test]
        public void Parallel_Ordered_Join()
        {
            for (int j = 1; j <= 32; j++)
            {
                Flux.Range(1, 10)
                    .Parallel(j, true)
                    .Sequential()
                    .Test()
                    .AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
            }
        }

        [Test]
        public void Parallel_Ordered_Join_Async()
        {
            for (int j = 1; j <= Environment.ProcessorCount; j++)
            {
                for (int i = 1; i <= 100000; i *= 10)
                {
                    var ts = Flux.Range(1, i)
                        .Parallel(j, true)
                        .RunOn(DefaultScheduler.Instance)
                        .Sequential()
                        .Test();

                    ts.AwaitTerminalEvent(TimeSpan.FromSeconds(20));

                    ts.AssertValueCount(i)
                        .AssertNoError()
                        .AssertComplete();

                    for (int k = 1; k <= i; k++)
                    {
                        Assert.AreEqual(k, ts.Values[k - 1]);
                    }
                }
            }
        }

        [Test]
        public void Parallel_Sorted()
        {
            for (int i = 1; i <= 32; i++)
            {
                Flux.From(10, 9, 8, 7, 6, 5, 4, 3, 2, 1)
                    .Parallel(i)
                    .Sorted()
                    .Test()
                    .AssertResult(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
            }
        }

        [Test]
        public void Parallel_Sorted_Async()
        {
            for (int j = 1; j <= Environment.ProcessorCount; j++)
            {
                for (int i = 1; i <= 100000; i *= 10)
                {
                    var ts = Flux.Range(1, i)
                        .Map(v => i - v + 1)
                        .Parallel(j, true)
                        .RunOn(DefaultScheduler.Instance)
                        .Sorted()
                        .Test();

                    ts.AwaitTerminalEvent(TimeSpan.FromSeconds(20));

                    ts.AssertValueCount(i)
                        .AssertNoError()
                        .AssertComplete();

                    for (int k = 1; k <= i; k++)
                    {
                        Assert.AreEqual(k, ts.Values[k - 1]);
                    }
                }
            }
        }


        [Test]
        public void Parallel_Groups()
        {
            for (int i = 1; i <= 32; i++) {
                for (int j = 1; j <= 100000; j *= 10)
                {
                    Flux.Range(1, j)
                        .Parallel(i)
                        .Groups()
                        .FlatMap(v => v)
                        .Test()
                        .AssertValueCount(j)
                        .AssertNoError()
                        .AssertComplete();
                }
            }
        }
    }
}
