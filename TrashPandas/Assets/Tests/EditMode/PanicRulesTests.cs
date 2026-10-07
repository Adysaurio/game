using NUnit.Framework;
using TrashPandas.Core.Panic;

namespace TrashPandas.Tests
{
    public class HitTrackerTests
    {
        [Test]
        public void ThreeHits_Catch()
        {
            var h = new HitTracker();
            Assert.AreEqual(HitResult.Stunned, h.TryHit(0, 1f));
            Assert.AreEqual(HitResult.Stunned, h.TryHit(0, 3f));
            Assert.AreEqual(HitResult.Caught, h.TryHit(0, 5f));
            Assert.AreEqual(3, h.Hits(0));
        }

        [Test]
        public void HitDuringInvulnerability_DoesNotCountDouble()
        {
            var h = new HitTracker();
            h.TryHit(0, 1f);
            Assert.AreEqual(HitResult.Ignored, h.TryHit(0, 1.5f), "same swing / pile-on within 0.8 s");
            Assert.AreEqual(1, h.Hits(0));
            Assert.AreEqual(HitResult.Stunned, h.TryHit(0, 1.9f));
        }

        [Test]
        public void Stun_LastsOneSecond()
        {
            var h = new HitTracker();
            h.TryHit(0, 10f);
            Assert.IsTrue(h.IsStunned(0, 10.9f));
            Assert.IsFalse(h.IsStunned(0, 11.1f));
            Assert.IsFalse(h.IsStunned(1, 10f), "other players are unaffected");
        }

        [Test]
        public void AfterCaught_FurtherHitsIgnored()
        {
            var h = new HitTracker();
            h.TryHit(0, 1f); h.TryHit(0, 2f); h.TryHit(0, 3f);
            Assert.AreEqual(HitResult.Ignored, h.TryHit(0, 5f));
            Assert.AreEqual(3, h.Hits(0));
        }

        [Test]
        public void Reset_Clears()
        {
            var h = new HitTracker();
            h.TryHit(0, 1f);
            h.Reset();
            Assert.AreEqual(0, h.Hits(0));
            Assert.IsFalse(h.IsStunned(0, 1.1f));
        }
    }

    public class RoundOutcomeTests
    {
        [Test]
        public void EndsWhenEveryoneIsResolved()
        {
            var r = new RoundOutcome();
            r.Begin(new[] { 0, 1 }, now: 0f);
            Assert.IsFalse(r.IsOver);
            Assert.IsTrue(r.MarkEscaped(0));
            Assert.IsFalse(r.IsOver);
            Assert.IsTrue(r.MarkCaught(1));
            Assert.IsTrue(r.IsOver);
            Assert.AreEqual(1, r.Escaped);
            Assert.AreEqual(1, r.Caught);
        }

        [Test]
        public void Resolution_IsFinal()
        {
            var r = new RoundOutcome();
            r.Begin(new[] { 0 }, 0f);
            r.MarkEscaped(0);
            Assert.IsFalse(r.MarkCaught(0), "a late hit can't catch someone who already escaped");
            Assert.IsFalse(r.MarkEscaped(0), "escaping twice is a no-op");
            Assert.AreEqual(PlayerOutcome.Escaped, r.StatusOf(0));
        }

        [Test]
        public void TimeLimit_CatchesWhoeverIsStillRunning()
        {
            var r = new RoundOutcome();
            r.Begin(new[] { 0, 1 }, now: 0f, timeLimit: 90f);
            r.MarkEscaped(0);
            r.Tick(89f);
            Assert.IsFalse(r.IsOver);
            r.Tick(90.1f);
            Assert.IsTrue(r.IsOver);
            Assert.AreEqual(PlayerOutcome.Caught, r.StatusOf(1));
            Assert.AreEqual(90f, r.SecondsLeft(0f), 1e-3f);
        }

        [Test]
        public void UnknownPlayer_IsNotRunning()
        {
            var r = new RoundOutcome();
            r.Begin(new[] { 0 }, 0f);
            Assert.AreEqual(PlayerOutcome.None, r.StatusOf(7));
            Assert.IsFalse(r.MarkCaught(7));
        }
    }
}
