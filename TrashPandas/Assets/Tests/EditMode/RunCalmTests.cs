using NUnit.Framework;
using TrashPandas.Core.Panic;

namespace TrashPandas.Tests
{
    public class RunCalmTests
    {
        static bool Run(RunCalm c, float seconds, float runTime, bool seen, bool allHidden)
        {
            bool calmed = false;
            for (float t = 0f; t < seconds; t += 0.1f) calmed |= c.Update(0.1f, runTime + t, seen, allHidden);
            return calmed;
        }

        [Test]
        public void StartsFull()
        {
            Assert.AreEqual(1f, new RunCalm().Alert01);
        }

        [Test]
        public void HidingLongEnough_CalmsTheRun()
        {
            var c = new RunCalm();
            Assert.IsFalse(Run(c, RunCalm.HideSeconds - 1f, 20f, seen: false, allHidden: true), "a bit more hiding needed");
            Assert.Less(c.Alert01, 1f);
            Assert.IsTrue(Run(c, 1.5f, 40f, false, true));
            Assert.AreEqual(0f, c.Alert01);
        }

        [Test]
        public void JustOutOfSight_DoesNotCalmIt()
        {
            var c = new RunCalm();
            Assert.IsFalse(Run(c, 60f, 20f, seen: false, allHidden: false));
            Assert.AreEqual(1f, c.Alert01);
        }

        [Test]
        public void BeingSeen_FillsItBackUp()
        {
            var c = new RunCalm();
            Run(c, 5f, 20f, false, true);
            c.Update(0.1f, 30f, seen: true, allHidden: true);
            Assert.AreEqual(1f, c.Alert01);
        }

        [Test]
        public void NotRightAtTheStart()
        {
            var c = new RunCalm();
            Run(c, RunCalm.MinRunSeconds - 1f, 0f, false, true);
            Assert.AreEqual(1f, c.Alert01, "hiding during the first seconds of the RUN doesn't count yet");
        }
    }
}
