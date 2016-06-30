using System;
using Reactor.Core.flow;
using Reactor.Core.scheduler;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Threading;
using Reactor.Core.subscriber;

namespace Reactor.Core.Test
{
    [TestFixture]
    [Timeout(30000)]
    public class UnicastProcessorTest
    {
        [Test]
        public void UnicastProcessor_Online()
        {
            var up = new UnicastProcessor<int>();

            var ts = up.Test();

            up.OnNext(1, 2, 3, 4, 5, 6);
            up.OnComplete();

            ts.AssertResult(1, 2, 3, 4, 5, 6);
        }


        [Test]
        public void UnicastProcessor_Offline()
        {
            var up = new UnicastProcessor<int>();
            up.OnNext(1, 2, 3, 4, 5, 6);
            up.OnComplete();

            var ts = up.Test();

            ts.AssertResult(1, 2, 3, 4, 5, 6);

        }

        [Test]
        public void UnicastProcessor_Conditional_Offline()
        {
            var up = new UnicastProcessor<int>();
            up.OnNext(1, 2, 3, 4, 5, 6);
            up.OnComplete();

            var ts = up.Filter(v => true).Test();

            ts.AssertResult(1, 2, 3, 4, 5, 6);

        }


        [Test]
        public void UnicastProcessor_Online_Scheduled()
        {
            for (int i = 0; i < 100000; i++)
            {
                var up = new UnicastProcessor<int>();

                Task.Run(() =>
                {
                    up.OnNext(1, 2, 3, 4, 5, 6);
                    up.OnComplete();
                });

                var ts = up.Test();

                ts
                .AwaitTerminalEvent(TimeSpan.FromSeconds(5))
                .AssertResult(1, 2, 3, 4, 5, 6);
            }
        }

        [Test]
        public void UnicastProcessor_Online_Fused_Scheduled()
        {
            for (int i = 0; i < 100000; i++)
            {
                var up = new UnicastProcessor<int>();

                Task.Run(() =>
                {
                    up.OnNext(1, 2, 3, 4, 5, 6);
                    up.OnComplete();
                });

                var ts = up.Test(fusionMode: FuseableHelper.ANY);

                ts
                .AwaitTerminalEvent(TimeSpan.FromSeconds(5))
                .AssertResult(1, 2, 3, 4, 5, 6);
            }
        }

        [Test]
        public void UnicastProcessor_Online_Fused_Scheduled_2()
        {
            for (int i = 0; i < 100000; i++)
            {
                var up = new UnicastProcessor<int>();

                var ts = new TestSubscriber<int>(fusionMode: FuseableHelper.ANY);

                int[] wait = { 2 };

                Task.Run(() =>
                {
                    Interlocked.Decrement(ref wait[0]);
                    while (Volatile.Read(ref wait[0]) != 0) ;

                    up.OnNext(1, 2);
                    up.OnNext(3, 4);
                    up.OnNext(5, 6);
                    up.OnComplete();
                });

                Interlocked.Decrement(ref wait[0]);
                while (Volatile.Read(ref wait[0]) != 0) ;

                up.Subscribe(ts);

                ts
                .AwaitTerminalEvent(TimeSpan.FromSeconds(5))
                .AssertResult(1, 2, 3, 4, 5, 6);
            }
        }

        [Test]
        public void UnicastProcessor_Online_Fused_Scheduled_3()
        {
            for (int i = 0; i < 100000; i++)
            {
                var up = new UnicastProcessor<int>();

                var ts = new TestSubscriber<int>(fusionMode: FuseableHelper.ANY);

                int[] wait = { 2 };

                Task.Run(() =>
                {
                    up.OnNext(1, 2);

                    Interlocked.Decrement(ref wait[0]);
                    while (Volatile.Read(ref wait[0]) != 0) ;

                    up.OnNext(3, 4);
                    up.OnNext(5, 6);
                    up.OnComplete();
                });

                Interlocked.Decrement(ref wait[0]);
                while (Volatile.Read(ref wait[0]) != 0) ;

                up.Subscribe(ts);

                ts
                .AwaitTerminalEvent(TimeSpan.FromSeconds(5))
                .AssertResult(1, 2, 3, 4, 5, 6);
            }
        }

        [Test]
        public void UnicastProcessor_Online_Fused_Scheduled_4()
        {
            for (int i = 0; i < 100000; i++)
            {
                var up = new UnicastProcessor<int>();

                var ts = new TestSubscriber<int>(fusionMode: FuseableHelper.ANY);

                int[] wait = { 2 };

                Task.Run(() =>
                {
                    up.OnNext(1, 2);
                    up.OnNext(3, 4);

                    Interlocked.Decrement(ref wait[0]);
                    while (Volatile.Read(ref wait[0]) != 0) ;

                    up.OnNext(5, 6);
                    up.OnComplete();
                });

                Interlocked.Decrement(ref wait[0]);
                while (Volatile.Read(ref wait[0]) != 0) ;

                up.Subscribe(ts);

                ts
                .AwaitTerminalEvent(TimeSpan.FromSeconds(5))
                .AssertResult(1, 2, 3, 4, 5, 6);
            }
        }

        [Test]
        public void UnicastProcessor_Online_Fused_Scheduled_5()
        {
            for (int i = 0; i < 100000; i++)
            {
                var up = new UnicastProcessor<int>();

                var ts = new TestSubscriber<int>(fusionMode: FuseableHelper.ANY);

                int[] wait = { 2 };

                Task.Run(() =>
                {
                    up.OnNext(1, 2);
                    up.OnNext(3, 4);
                    up.OnNext(5, 6);

                    Interlocked.Decrement(ref wait[0]);
                    while (Volatile.Read(ref wait[0]) != 0) ;

                    up.OnComplete();
                });

                Interlocked.Decrement(ref wait[0]);
                while (Volatile.Read(ref wait[0]) != 0) ;

                up.Subscribe(ts);

                ts
                .AwaitTerminalEvent(TimeSpan.FromSeconds(5))
                .AssertResult(1, 2, 3, 4, 5, 6);
            }
        }

        [Test]
        public void UnicastProcessor_Online_Fused_Scheduled_6()
        {
            for (int i = 0; i < 100000; i++)
            {
                var up = new UnicastProcessor<int>();

                var ts = new TestSubscriber<int>(fusionMode: FuseableHelper.ANY);

                int[] wait = { 2 };

                Task.Run(() =>
                {
                    up.OnNext(1, 2);
                    up.OnNext(3, 4);
                    up.OnNext(5, 6);
                    up.OnComplete();

                    Interlocked.Decrement(ref wait[0]);
                    while (Volatile.Read(ref wait[0]) != 0) ;
                });

                Interlocked.Decrement(ref wait[0]);
                while (Volatile.Read(ref wait[0]) != 0) ;

                up.Subscribe(ts);

                ts
                .AwaitTerminalEvent(TimeSpan.FromSeconds(5))
                .AssertResult(1, 2, 3, 4, 5, 6);
            }
        }

        [Test]
        public void UnicastProcessor_Online_Hidden_Scheduled()
        {
            for (int i = 0; i < 100000; i++)
            {
                var up = new UnicastProcessor<int>();

                Task.Run(() =>
                {
                    up.OnNext(1, 2, 3, 4, 5, 6);
                    up.OnComplete();
                });

                var ts = up.Hide().Test();

                ts
                .AwaitTerminalEvent(TimeSpan.FromSeconds(5))
                .AssertResult(1, 2, 3, 4, 5, 6);
            }
        }
    }
}
