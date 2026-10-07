using NUnit.Framework;
using TrashPandas.Core.Input;

namespace TrashPandas.Tests
{
    public class LookSensitivityTests
    {
        [Test]
        public void Steps_MultiplyAndStayWithinLimits()
        {
            var s = new LookSensitivity(0.3f);
            s.Increase();
            Assert.AreEqual(0.3f * LookSensitivity.Step, s.Value, 1e-5f);
            s.Decrease();
            Assert.AreEqual(0.3f, s.Value, 1e-5f);

            for (int i = 0; i < 100; i++) s.Increase();
            Assert.AreEqual(LookSensitivity.Max, s.Value, 1e-5f);
            for (int i = 0; i < 100; i++) s.Decrease();
            Assert.AreEqual(LookSensitivity.Min, s.Value, 1e-5f);
        }

        [Test]
        public void Constructor_ClampsBadSavedValues()
        {
            Assert.AreEqual(LookSensitivity.Max, new LookSensitivity(999f).Value, 1e-5f);
            Assert.AreEqual(LookSensitivity.Default, new LookSensitivity(float.NaN).Value, 1e-5f);
            Assert.AreEqual(LookSensitivity.Default, new LookSensitivity(0f).Value, 1e-5f);
        }
    }
}
