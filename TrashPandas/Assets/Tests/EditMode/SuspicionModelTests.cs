using NUnit.Framework;
using TrashPandas.Core.Suspicion;
using TrashPandas.Core.Trenchcoat;

namespace TrashPandas.Tests
{
    public class SuspicionModelTests
    {
        static SuspicionSettings S() => new SuspicionSettings
        {
            WitnessedMissingPartRate = 10f, UnwitnessedFactor = 0.2f, WeirdMovementRate = 20f,
            RaccoonSightingBurst = 20f, SightingCooldown = 2f, CatHissRate = 15f,
            CalmDecayRate = 5f, CalmDelay = 2f,
        };

        static void Run(SuspicionModel m, float seconds, SuspicionFrame f, float step = 0.1f)
        {
            long steps = (long)System.Math.Round(seconds / (double)step);
            for (long i = 0; i < steps; i++) m.Tick(step, f);
        }

        [Test]
        public void MissingPart_Witnessed_RaisesFullRate()
        {
            var m = new SuspicionModel(S());
            Run(m, 1f, new SuspicionFrame { MissingParts = BodyPart.ArmLeft, CoatWitnessed = true });
            Assert.AreEqual(10f, m.Value, 0.01f);
        }

        [Test]
        public void MissingPart_Unwitnessed_RaisesSlowly()
        {
            var m = new SuspicionModel(S());
            Run(m, 1f, new SuspicionFrame { MissingParts = BodyPart.ArmLeft, CoatWitnessed = false });
            Assert.AreEqual(2f, m.Value, 0.01f);
        }

        [Test]
        public void MultiPartSlots_CountEachMissingPart()
        {
            var m = new SuspicionModel(S());
            Run(m, 1f, new SuspicionFrame { MissingParts = BodyPart.Arms, CoatWitnessed = true });
            Assert.AreEqual(20f, m.Value, 0.01f, "two arms missing = twice the rate");
        }

        [Test]
        public void WeirdMovement_ScalesWithSeenWeirdness()
        {
            var m = new SuspicionModel(S());
            Run(m, 1f, new SuspicionFrame { CoatWitnessed = true, SeenWeirdness = 0.5f });
            Assert.AreEqual(10f, m.Value, 0.01f);
        }

        [Test]
        public void RaccoonSighting_Burst_RespectsCooldownPerWitness()
        {
            var m = new SuspicionModel(S());
            Assert.IsTrue(m.ReportRaccoonSighting(witnessId: 1, now: 10f));
            Assert.IsFalse(m.ReportRaccoonSighting(1, 10.5f), "same guest, still in cooldown");
            Assert.IsTrue(m.ReportRaccoonSighting(2, 10.5f), "a different guest counts");
            Assert.IsTrue(m.ReportRaccoonSighting(1, 12.1f), "cooldown over");
            Assert.AreEqual(60f, m.Value, 0.01f);
        }

        [Test]
        public void CatHiss_Raises()
        {
            var m = new SuspicionModel(S());
            Run(m, 2f, new SuspicionFrame { CatHissing = true });
            Assert.AreEqual(30f, m.Value, 0.01f);
        }

        [Test]
        public void Calm_DecaysOnlyAfterDelay()
        {
            var m = new SuspicionModel(S());
            m.ReportRaccoonSighting(1, 0f);                  // 20
            Run(m, 1.5f, default);                          // still within the calm delay
            Assert.AreEqual(20f, m.Value, 0.01f);
            Run(m, 2.5f, default);                          // 0.5 s of delay left, then 2 s of decay
            Assert.AreEqual(10f, m.Value, 0.2f);
        }

        [Test]
        public void ClampedToRange()
        {
            var m = new SuspicionModel(S());
            for (int i = 0; i < 10; i++) m.ReportRaccoonSighting(i, 0f);
            Assert.AreEqual(100f, m.Value);
            m.Reset();
            Run(m, 10f, default);
            Assert.AreEqual(0f, m.Value);
        }

        [Test]
        public void Caught_FiresExactlyOnce()
        {
            var m = new SuspicionModel(S());
            int fired = 0;
            m.CaughtChanged += caught => { if (caught) fired++; };
            for (int i = 0; i < 8; i++) m.ReportRaccoonSighting(i, 0f);
            Run(m, 1f, new SuspicionFrame { CatHissing = true });
            Assert.IsTrue(m.Caught);
            Assert.AreEqual(1, fired);
        }

        [Test]
        public void Caught_FreezesValue_UntilReset()
        {
            var m = new SuspicionModel(S());
            for (int i = 0; i < 5; i++) m.ReportRaccoonSighting(i, 0f);
            Run(m, 10f, default);
            Assert.AreEqual(100f, m.Value, "no calming down once caught");
            m.Reset();
            Assert.IsFalse(m.Caught);
            Assert.AreEqual(0f, m.Value);
        }
    
        [TestCase(0.016f)]
        [TestCase(0.0001f)]
        [TestCase(0.000001f)]
        public void Calm_DecaysAtAnyFrameRate(float step)
        {
            var m = new SuspicionModel(S());
            m.ReportRaccoonSighting(1, 0f); // 20
            Run(m, 4f, default, step);      // 2 s delay + 2 s of decay at 5/s
            Assert.AreEqual(10f, m.Value, 0.3f, $"dt={step}");
        }

        [TestCase(0.0001f)]
        [TestCase(0.000001f)]
        public void Rise_IsFrameRateIndependent(float step)
        {
            var m = new SuspicionModel(S());
            Run(m, 1f, new SuspicionFrame { CatHissing = true }, step);
            Assert.AreEqual(15f, m.Value, 0.3f, $"dt={step}");
        }
    
        [Test]
        public void NegligibleWeirdness_DoesNotBlockCalmDecay()
        {
            var m = new SuspicionModel(S());
            m.ReportRaccoonSighting(1, 0f); // 20
            Run(m, 4f, new SuspicionFrame { CoatWitnessed = true, SeenWeirdness = 1e-6f });
            Assert.AreEqual(10f, m.Value, 0.3f, "a rounding-level wobble while walking is not 'something odd'");
        }
    }
}
