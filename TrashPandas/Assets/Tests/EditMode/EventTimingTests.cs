using NUnit.Framework;
using TrashPandas.Core.Events;
using TrashPandas.Core.Suspicion;

namespace TrashPandas.Tests
{
    public class WarningTimeTests
    {
        [TestCase(0f, 8f)]     // everyone inside: minimum warning
        [TestCase(20f, 8f)]    // 5 s run + 3 = 8
        [TestCase(40f, 13f)]   // 10 s run + 3
        [TestCase(200f, 20f)]  // capped
        public void FromFarthestRaccoon(float distance, float expected)
        {
            Assert.AreEqual(expected, WarningTime.Compute(distance), 1e-3f);
        }
    }

    public class EventSchedulerTests
    {
        [Test]
        public void FirstEvent_Within35To50Seconds()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var s = new EventScheduler(seed);
                Assert.That(s.NextAt, Is.InRange(35f, 50f));
                Assert.IsFalse(s.IsDue(34.9f));
            }
        }

        [Test]
        public void AfterAnEvent_Next45To75SecondsLater()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var s = new EventScheduler(seed);
                s.EventFinished(100f);
                Assert.That(s.NextAt, Is.InRange(145f, 175f));
            }
        }

        [Test]
        public void NeverTheSameSpeakerTwiceInARow()
        {
            var s = new EventScheduler(7);
            int last = s.PickSpeaker(4);
            for (int i = 0; i < 200; i++)
            {
                int next = s.PickSpeaker(4);
                Assert.AreNotEqual(last, next);
                last = next;
            }
        }

        [Test]
        public void SingleSpeaker_StillWorks()
        {
            var s = new EventScheduler(1);
            Assert.AreEqual(0, s.PickSpeaker(1));
            Assert.AreEqual(0, s.PickSpeaker(1));
        }
    }

    public class SuspicionAdjustTests
    {
        [Test]
        public void Adjust_RaisesAndLowers_WithinRange()
        {
            var m = new SuspicionModel(new SuspicionSettings());
            m.Adjust(30f);
            Assert.AreEqual(30f, m.Value, 1e-3f);
            m.Adjust(-8f);
            Assert.AreEqual(22f, m.Value, 1e-3f);
            m.Adjust(-100f);
            Assert.AreEqual(0f, m.Value, 1e-3f);
        }

        [Test]
        public void Adjust_CanTriggerCaught()
        {
            var m = new SuspicionModel(new SuspicionSettings());
            m.Adjust(95f);
            m.Adjust(25f);
            Assert.IsTrue(m.Caught);
        }

        [Test]
        public void Adjust_Raise_RestartsCalmDelay()
        {
            var m = new SuspicionModel(new SuspicionSettings { CalmDelay = 2f, CalmDecayRate = 5f });
            m.Adjust(20f);
            m.Tick(1.5f, default);
            Assert.AreEqual(20f, m.Value, 1e-3f, "a failed event doesn't start fading right away");
        }
    }
}
