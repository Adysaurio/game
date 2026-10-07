using NUnit.Framework;
using TrashPandas.Core.Perception;
using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class PerceptionTests
    {
        static readonly Vector3 Eye = new Vector3(0f, 1.6f, 0f);

        [Test]
        public void Sees_TargetAheadInRange()
        {
            Assert.IsTrue(VisionCone.CanSee(Eye, Vector3.forward, new Vector3(1f, 1f, 5f), 8f, 110f, occluded: false));
        }

        [Test]
        public void DoesNotSee_OutOfRange_Behind_OutsideFov_OrOccluded()
        {
            Assert.IsFalse(VisionCone.CanSee(Eye, Vector3.forward, new Vector3(0f, 1f, 9f), 8f, 110f, false), "too far");
            Assert.IsFalse(VisionCone.CanSee(Eye, Vector3.forward, new Vector3(0f, 1f, -3f), 8f, 110f, false), "behind");
            Assert.IsFalse(VisionCone.CanSee(Eye, Vector3.forward, new Vector3(5f, 1.6f, 1f), 8f, 110f, false), "outside a 110° cone");
            Assert.IsFalse(VisionCone.CanSee(Eye, Vector3.forward, new Vector3(0f, 1f, 4f), 8f, 110f, occluded: true), "behind a table");
        }

        [Test]
        public void Fov_IsMeasuredOnTheGround_NotPenalizingHeight()
        {
            Assert.IsTrue(VisionCone.CanSee(Eye, Vector3.forward, new Vector3(0f, 0.2f, 1.5f), 8f, 110f, false),
                "a raccoon at your feet in front of you is visible");
        }

        [Test]
        public void Weirdness_NormalCoat_IsZero()
        {
            Assert.AreEqual(0f, Weirdness.Of(new BodyIntent()), 1e-4f);
        }

        [Test]
        public void Weirdness_Collapsed_IsMax()
        {
            Assert.AreEqual(1f, Weirdness.Of(new BodyIntent { Collapsed = true }), 1e-4f);
        }

        [Test]
        public void Weirdness_AddsUpVisibleProblems_Capped()
        {
            Assert.AreEqual(0.3f, Weirdness.Of(new BodyIntent { LeftArmLimp = true }), 1e-4f);
            Assert.AreEqual(0.6f, Weirdness.Of(new BodyIntent { LeftArmLimp = true, HeadSlumped = true }), 1e-4f);
            Assert.AreEqual(0.45f, Weirdness.Of(new BodyIntent { RightLegLimp = true, Discord = 0.3f }), 1e-4f);
            Assert.AreEqual(1f, Weirdness.Of(new BodyIntent { LeftArmLimp = true, RightArmLimp = true, HeadSlumped = true, LeftLegLimp = true }), 1e-4f);
        }
    }
}
