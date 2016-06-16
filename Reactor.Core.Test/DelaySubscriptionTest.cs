using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    public class DelaySubscriptionTest
    {
        [Test]
        public void DelaySubscription_Just()
        {
            Flux.Range(1, 5).DelaySubscription(Flux.Just(1)).Test().AssertResult(1, 2, 3, 4, 5);
        }


        [Test]
        public void DelaySubscription_Range()
        {
            Flux.Range(1, 5).DelaySubscription(Flux.Range(1, 2)).Test().AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void DelaySubscription_Empty()
        {
            Flux.Range(1, 5).DelaySubscription(Flux.Empty<int>()).Test().AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void DelaySubscription_Just_Conditional()
        {
            Flux.Range(1, 5).DelaySubscription(Flux.Just(1)).Filter(v => true).Test().AssertResult(1, 2, 3, 4, 5);
        }


        [Test]
        public void DelaySubscription_Range_Conditional()
        {
            Flux.Range(1, 5).DelaySubscription(Flux.Range(1, 2)).Filter(v => true).Test().AssertResult(1, 2, 3, 4, 5);
        }

        [Test]
        public void DelaySubscription_Empty_Conditional()
        {
            Flux.Range(1, 5).DelaySubscription(Flux.Empty<int>()).Filter(v => true).Test().AssertResult(1, 2, 3, 4, 5);
        }
    }
}
