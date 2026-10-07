using NUnit.Framework;

namespace TrashPandas.Tests
{
    public class SmokeTests
    {
        [Test]
        public void TestPipeline_Runs()
        {
            Assert.AreEqual(4, 2 + 2);
        }
    }
}
