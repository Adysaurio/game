using NUnit.Framework;
using TrashPandas.Core.Events;
using TrashPandas.Core.Panic;

namespace TrashPandas.Tests
{
    public class ReadingTimeTests
    {
        [Test]
        public void ShortLine_GetsTheMinimum()
        {
            Assert.AreEqual(EventTiming.MinReading, EventTiming.ReadingTime("Champagne, sir?"), 1e-3f);
        }

        [Test]
        public void LongerLines_GetMoreTime_UpToTheCap()
        {
            float shortT = EventTiming.ReadingTime("Champagne, sir?");
            float longT = EventTiming.ReadingTime("A blessed day, my son. Shall we say a little prayer?");
            Assert.Greater(longT, shortT);
            Assert.LessOrEqual(EventTiming.ReadingTime(new string('a', 500)), EventTiming.MaxReading);
        }

        [Test]
        public void NullLine_IsSafe()
        {
            Assert.AreEqual(EventTiming.MinReading, EventTiming.ReadingTime(null), 1e-3f);
        }
    }

    public class PanicGraceTests
    {
        [Test]
        public void HumansFreezeInSurprise_ThenMove()
        {
            var g = new PanicGrace(surpriseSeconds: 2f, noHitSeconds: 3f);
            g.Begin(10f);
            Assert.IsFalse(g.ChasersMayMove(11.9f), "frozen in shock");
            Assert.IsTrue(g.ChasersMayMove(12.1f));
        }

        [Test]
        public void NoHits_DuringTheHeadStart()
        {
            var g = new PanicGrace(2f, 3f);
            g.Begin(10f);
            Assert.IsFalse(g.MayHit(12.9f));
            Assert.IsTrue(g.MayHit(13.1f));
        }

        [Test]
        public void BeforeBegin_EverythingAllowed()
        {
            var g = new PanicGrace(2f, 3f);
            Assert.IsTrue(g.ChasersMayMove(0f));
            Assert.IsTrue(g.MayHit(0f));
        }
    }
}
