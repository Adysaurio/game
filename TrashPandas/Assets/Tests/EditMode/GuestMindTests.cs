using NUnit.Framework;
using TrashPandas.Core.Npc;

namespace TrashPandas.Tests
{
    public class GuestMindTests
    {
        static GuestState Run(GuestMind m, float seconds, float weird, bool raccoon)
        {
            GuestState s = m.State;
            for (float t = 0; t < seconds - 1e-4f; t += 0.1f) s = m.Update(0.1f, weird, raccoon);
            return s;
        }

        [Test]
        public void NothingOdd_StaysCalm()
        {
            Assert.AreEqual(GuestState.Calm, Run(new GuestMind(), 5f, 0f, false));
        }

        [Test]
        public void SomethingOdd_MakesCurious()
        {
            Assert.AreEqual(GuestState.Curious, Run(new GuestMind(), 0.2f, 0.3f, false));
        }

        [Test]
        public void BarelyOdd_IsIgnored()
        {
            Assert.AreEqual(GuestState.Calm, Run(new GuestMind(), 3f, 0.1f, false));
        }

        [Test]
        public void Curious_ForgetsAfterAWhile()
        {
            var m = new GuestMind();
            Run(m, 0.2f, 0.3f, false);
            Assert.AreEqual(GuestState.Curious, Run(m, 2f, 0f, false), "still wondering");
            Assert.AreEqual(GuestState.Calm, Run(m, 1f, 0f, false));
        }

        [Test]
        public void VeryOdd_Sustained_Alarms()
        {
            var m = new GuestMind();
            Assert.AreEqual(GuestState.Curious, Run(m, 1f, 0.8f, false), "not instantly");
            Assert.AreEqual(GuestState.Alarmed, Run(m, 0.6f, 0.8f, false));
        }

        [Test]
        public void LooseRaccoon_AlarmsAfterNoticing_ThenCalmsDownInSteps()
        {
            var m = new GuestMind();
            Assert.AreNotEqual(GuestState.Alarmed, Run(m, 0.4f, 0f, true), "a quick glimpse isn't enough");
            Assert.AreEqual(GuestState.Alarmed, Run(m, 0.3f, 0f, true), "seen for ~0.6 s: alarmed");
            Assert.AreEqual(GuestState.Alarmed, Run(m, 3.5f, 0f, false), "stays alarmed a few seconds");
            Assert.AreEqual(GuestState.Curious, Run(m, 1f, 0f, false));
            Assert.AreEqual(GuestState.Calm, Run(m, 3f, 0f, false));
        }

        [Test]
        public void RaccoonDashingThroughView_GoesUnregistered()
        {
            var m = new GuestMind();
            Run(m, 0.3f, 0f, true);
            Run(m, 0.5f, 0f, false); // out of sight again
            Run(m, 0.3f, 0f, true);  // glimpsed again, but the clock restarted
            Assert.AreNotEqual(GuestState.Alarmed, m.State);
        }

        [Test]
        public void JustAlarmed_FiresExactlyOnce_WhileWatching()
        {
            var m = new GuestMind();
            int fired = 0;
            for (int i = 0; i < 20; i++) { m.Update(0.1f, 0f, true); if (m.JustAlarmed) fired++; }
            Assert.AreEqual(1, fired);
        }
    }
}
