using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestLib;

namespace Test32
{
    [TestClass]
    public class UnitTest
    {
        [TestMethod]
        public void Test() => Assert.IsTrue(TestClass.Test());
    }
}
