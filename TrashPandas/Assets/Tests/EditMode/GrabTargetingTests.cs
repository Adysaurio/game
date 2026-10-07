using System.Collections.Generic;
using NUnit.Framework;
using TrashPandas.Core.Grabbing;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class GrabTargetingTests
    {
        static readonly Vector3 Eye = new Vector3(0f, 2f, -3f);
        static readonly Vector3 Chest = new Vector3(0f, 1.3f, 0f);
        const float Reach = 1.3f;
        const float Cone = 12f;

        static int? Pick(Vector3 aim, params GrabCandidate[] candidates) =>
            GrabTargeting.Pick(Eye, aim, new List<GrabCandidate>(candidates), Chest, Reach, Cone);

        static Vector3 Toward(Vector3 p) => (p - Eye).normalized;

        [Test]
        public void Picks_ObjectUnderCrosshairWithinReach()
        {
            var glass = new GrabCandidate { Id = 1, Position = new Vector3(0.3f, 1f, 0.6f) };
            Assert.AreEqual(1, Pick(Toward(glass.Position), glass));
        }

        [Test]
        public void Magnetism_PicksSlightlyOffAim_ButNotFarOff()
        {
            var glass = new GrabCandidate { Id = 1, Position = new Vector3(0.3f, 1f, 0.6f) };
            Vector3 slightlyOff = Quaternion.Euler(0f, 6f, 0f) * Toward(glass.Position);
            Vector3 farOff = Quaternion.Euler(0f, 30f, 0f) * Toward(glass.Position);
            Assert.AreEqual(1, Pick(slightlyOff, glass));
            Assert.IsNull(Pick(farOff, glass));
        }

        [Test]
        public void Ignores_OutOfReach_AndBehindTheAimRay()
        {
            var farGlass = new GrabCandidate { Id = 1, Position = new Vector3(0f, 1f, 4f) };
            Assert.IsNull(Pick(Toward(farGlass.Position), farGlass));

            var nearChest = new GrabCandidate { Id = 2, Position = new Vector3(0f, 1.3f, 0.3f) };
            Assert.IsNull(Pick(-Toward(nearChest.Position), nearChest), "aiming away from it");
        }

        [Test]
        public void Prefers_SmallestAngleFromCrosshair()
        {
            var a = new GrabCandidate { Id = 1, Position = new Vector3(0.4f, 1f, 0.6f) };
            var b = new GrabCandidate { Id = 2, Position = new Vector3(0.2f, 1f, 0.6f) };
            Assert.AreEqual(2, Pick(Toward(b.Position), a, b));
        }

        [Test]
        public void ClosestHand_ChoosesBySide()
        {
            var left = new Vector3(-0.4f, 1.5f, 0f);
            var right = new Vector3(0.4f, 1.5f, 0f);
            Assert.IsTrue(GrabTargeting.LeftHandCloser(new Vector3(-0.5f, 1f, 0.5f), left, right));
            Assert.IsFalse(GrabTargeting.LeftHandCloser(new Vector3(0.5f, 1f, 0.5f), left, right));
        }
    }
}
