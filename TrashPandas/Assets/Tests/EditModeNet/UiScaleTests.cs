using NUnit.Framework;
using TrashPandas.Runtime.Ui;

namespace TrashPandas.Tests.Net
{
    public class UiScaleTests
    {
        [TestCase(720, 1f)]
        [TestCase(1440, 2f)]
        [TestCase(1800, 2.5f)]
        public void ScalesWithWindowHeight(int height, float expected)
        {
            Assert.AreEqual(expected, UiScale.ScaleFor(height), 1e-4f);
        }

        [Test]
        public void NeverShrinksBelowReferenceSize()
        {
            Assert.AreEqual(1f, UiScale.ScaleFor(500), 1e-4f, "small windows keep 1:1 so text stays readable");
        }
    }
}
