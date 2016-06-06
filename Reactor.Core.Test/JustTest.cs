using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Reactor.Core.Test
{
    [TestClass]
    public class JustTest
    {
        [TestMethod]
        public void Normal()
        {
            Assert.AreEqual(1, Flux.Just(1).BlockLast());
        }
    }
}
