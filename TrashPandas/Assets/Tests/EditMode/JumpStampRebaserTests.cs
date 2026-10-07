using NUnit.Framework;
using TrashPandas.Core.Session;

namespace TrashPandas.Tests
{
    public class JumpStampRebaserTests
    {
        [Test]
        public void NewPress_IsStampedWithHostArrivalTime()
        {
            var r = new JumpStampRebaser();
            Assert.AreEqual(50.2f, r.Rebase(0, clientStamp: 49.9f, hostNow: 50.2f));
        }

        [Test]
        public void SamePressResent_KeepsFirstArrivalTime()
        {
            var r = new JumpStampRebaser();
            r.Rebase(0, 49.9f, 50.2f);
            Assert.AreEqual(50.2f, r.Rebase(0, 49.9f, 50.6f), "the press is repeated in every packet; it must not look newer");
        }

        [Test]
        public void TwoRemoteLegsWithLatency_StillLandInsideTheWindow()
        {
            // Both players pressed together; their packets arrive 0.18 s after their (lagging) client clocks.
            var r = new JumpStampRebaser();
            float left = r.Rebase(0, clientStamp: 10.00f, hostNow: 10.30f);
            float right = r.Rebase(1, clientStamp: 10.02f, hostNow: 10.33f);
            float hostNow = 10.34f;
            Assert.LessOrEqual(System.Math.Abs(left - right), 0.25f);
            Assert.LessOrEqual(hostNow - System.Math.Max(left, right), 0.25f, "fresh in host time despite the lag");
        }

        [Test]
        public void NeverPressed_StaysNeverPressed()
        {
            var r = new JumpStampRebaser();
            Assert.IsTrue(float.IsNegativeInfinity(r.Rebase(0, float.NegativeInfinity, 5f)));
        }

        [Test]
        public void Reset_ForgetsPlayer()
        {
            var r = new JumpStampRebaser();
            r.Rebase(0, 3f, 4f);
            r.Reset(0);
            Assert.AreEqual(9f, r.Rebase(0, 3f, 9f));
        }
    }
}
