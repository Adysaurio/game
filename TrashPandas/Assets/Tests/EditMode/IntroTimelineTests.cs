using NUnit.Framework;
using TrashPandas.Core.Raccoons;

namespace TrashPandas.Tests
{
    public class IntroTimelineTests
    {
        [Test]
        public void RaccoonsPopOutOneByOne_AfterTheEstablishingShot()
        {
            var t = new IntroTimeline(players: 3);
            Assert.AreEqual(IntroTimeline.Establish, t.PopTime(0), 1e-4f);
            Assert.AreEqual(IntroTimeline.Establish + IntroTimeline.PopGap, t.PopTime(1), 1e-4f);
            Assert.AreEqual(IntroTimeline.Establish + 2 * IntroTimeline.PopGap, t.PopTime(2), 1e-4f);
        }

        [Test]
        public void Phases_InOrder()
        {
            var t = new IntroTimeline(2);
            Assert.AreEqual(IntroPhase.Establishing, t.PhaseAt(0.1f));
            Assert.AreEqual(IntroPhase.PoppingOut, t.PhaseAt(t.PopTime(0) + 0.01f));
            Assert.AreEqual(IntroPhase.LineUp, t.PhaseAt(t.PopTime(1) + IntroTimeline.PopFlight + 0.01f));
            Assert.AreEqual(IntroPhase.Go, t.PhaseAt(t.Duration - IntroTimeline.GoTime + 0.01f));
            Assert.AreEqual(IntroPhase.Done, t.PhaseAt(t.Duration + 0.01f));
        }

        [Test]
        public void Flight_GoesFromUndergroundToTheSpot()
        {
            var t = new IntroTimeline(1);
            Assert.AreEqual(0f, t.FlightProgress(0, t.PopTime(0) - 0.1f), 1e-4f, "still in the sewer");
            Assert.AreEqual(1f, t.FlightProgress(0, t.PopTime(0) + IntroTimeline.PopFlight + 0.1f), 1e-4f);
            Assert.AreEqual(0.5f, t.FlightProgress(0, t.PopTime(0) + IntroTimeline.PopFlight / 2f), 1e-3f);
        }

        [Test]
        public void MorePlayers_LongerIntro_ButNeverLong()
        {
            Assert.Greater(new IntroTimeline(5).Duration, new IntroTimeline(2).Duration);
            Assert.LessOrEqual(new IntroTimeline(5).Duration, 7f, "it's a gag, not a cutscene");
        }
    }
}
