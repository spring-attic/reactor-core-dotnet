using NUnit.Framework;
using System;

namespace Reactor.Core.Test
{
    [TestFixture]
    public class FilterTest
    {
        [Test]
        public void Filter_Normal()
        {
            Flux.Range(1, 5).Filter(v => (v & 1) == 0).Test().AssertResult(2, 4);
        }

        [Test]
        public void Filter_Error()
        {
            Flux.Error<int>(new Exception("Forced failure"))
                .Filter(v => (v & 1) == 0).Test()
                .AssertNoValues().AssertErrorMessage("Forced failure").AssertNotComplete();
        }
    }
}
