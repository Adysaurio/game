using System.Collections.Generic;
using NUnit.Framework;
using TrashPandas.Core.Grabbing;
using TrashPandas.Core.Loot;
using TrashPandas.Core.Raccoons;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class ClickIntentTests
    {
        [Test]
        public void QuickClick_IsATap()
        {
            var c = new ClickIntent();
            c.Press(1f);
            var r = c.Release(1.1f);
            Assert.AreEqual(ClickResult.Tap, r.Kind);
        }

        [Test]
        public void Hold_IsAThrow_StrengthGrowsToOne()
        {
            var c = new ClickIntent();
            c.Press(1f);
            var half = c.Release(1.5f);
            Assert.AreEqual(ClickResult.Throw, half.Kind);
            Assert.AreEqual(0.5f, half.Strength, 0.05f);
            c.Press(2f);
            Assert.AreEqual(1f, c.Release(5f).Strength, 1e-3f, "capped");
        }

        [Test]
        public void ReleaseWithoutPress_IsNothing()
        {
            Assert.AreEqual(ClickResult.None, new ClickIntent().Release(1f).Kind);
        }

        [Test]
        public void Charge_ReportsWhileHolding()
        {
            var c = new ClickIntent();
            c.Press(0f);
            Assert.AreEqual(0f, c.Charge(0.1f), 1e-3f, "still a tap");
            Assert.Greater(c.Charge(0.6f), 0.4f);
        }
    }

    public class GrabPickTests
    {
        static readonly Vector3 Eye = new Vector3(0f, 2f, -3f);
        static readonly Vector3 Mouth = new Vector3(0f, 0.3f, 0f);

        [Test]
        public void PicksTheMostCentered_WithinReach()
        {
            var items = new List<GrabCandidate>
            {
                new GrabCandidate { Id = 1, Position = new Vector3(0.6f, 0.2f, 0.6f) },
                new GrabCandidate { Id = 2, Position = new Vector3(0f, 0.2f, 0.8f) },
            };
            Vector3 forward = (new Vector3(0f, 0.2f, 0.8f) - Eye).normalized;
            Assert.AreEqual(2, GrabPick.Pick(Eye, forward, Mouth, items));
        }

        [Test]
        public void OutOfReach_IsIgnored()
        {
            var items = new List<GrabCandidate> { new GrabCandidate { Id = 1, Position = new Vector3(0f, 0.2f, 3f) } };
            Vector3 forward = (items[0].Position - Eye).normalized;
            Assert.IsNull(GrabPick.Pick(Eye, forward, Mouth, items));
        }

        [Test]
        public void BehindTheView_IsIgnored()
        {
            var items = new List<GrabCandidate> { new GrabCandidate { Id = 1, Position = new Vector3(0f, 0.2f, -0.8f) } };
            Assert.IsNull(GrabPick.Pick(Eye, Vector3.forward, Mouth, items));
        }
    }

    public class DeliveryLedgerTests
    {
        [Test]
        public void Deliver_CreditsThePlayer_Once()
        {
            var l = new DeliveryLedger();
            Assert.IsTrue(l.Deliver(1, itemId: 5, value: 40, objective: null));
            Assert.IsFalse(l.Deliver(2, 5, 40, null), "same item twice");
            Assert.AreEqual(40, l.Of(1));
            Assert.AreEqual(0, l.Of(2));
            Assert.AreEqual(40, l.Total);
        }

        [Test]
        public void Objectives_AreMarked()
        {
            var l = new DeliveryLedger();
            l.Deliver(0, 9, 300, ObjectiveId.Ring);
            Assert.IsTrue(l.IsDone(ObjectiveId.Ring));
        }

        [Test]
        public void Reset_Clears()
        {
            var l = new DeliveryLedger();
            l.Deliver(0, 9, 300, ObjectiveId.Ring);
            l.Reset();
            Assert.AreEqual(0, l.Total);
            Assert.IsFalse(l.IsDone(ObjectiveId.Ring));
        }
    }
}
