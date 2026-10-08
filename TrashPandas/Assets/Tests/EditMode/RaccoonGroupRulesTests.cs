using NUnit.Framework;
using TrashPandas.Core.Raccoons;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class HeavyCarryTests
    {
        [Test]
        public void NeedsTwo()
        {
            Assert.IsFalse(HeavyCarry.Lifted(1));
            Assert.IsTrue(HeavyCarry.Lifted(2));
            Assert.IsTrue(HeavyCarry.Lifted(4));
        }

        [Test]
        public void Anchor_IsTheMidpoint()
        {
            var a = HeavyCarry.Anchor(new[] { new Vector3(0, 0.3f, 0), new Vector3(2, 0.3f, 0) });
            Assert.AreEqual(new Vector3(1, 0.3f, 0), a);
        }

        [Test]
        public void PullingApart_Drops()
        {
            Assert.IsFalse(HeavyCarry.ShouldDrop(new[] { Vector3.zero, new Vector3(1.5f, 0, 0) }));
            Assert.IsTrue(HeavyCarry.ShouldDrop(new[] { Vector3.zero, new Vector3(1.7f, 0, 0) }));
        }
    }

    public class TowerRulesTests
    {
        [Test]
        public void EachRiderSlowsTheBottom()
        {
            Assert.AreEqual(1f, TowerRules.SpeedFactor(0), 1e-4f);
            Assert.AreEqual(0.6f, TowerRules.SpeedFactor(1), 1e-4f);
            Assert.AreEqual(0.36f, TowerRules.SpeedFactor(2), 1e-4f);
        }

        [Test]
        public void RunningOrGettingHit_Collapses()
        {
            Assert.IsTrue(TowerRules.Collapses(bottomRunning: true, bottomHit: false));
            Assert.IsTrue(TowerRules.Collapses(false, true));
            Assert.IsFalse(TowerRules.Collapses(false, false));
        }

        [Test]
        public void CantMount_HeavyCarrier_Frozen_OrFullTower()
        {
            Assert.IsTrue(TowerRules.CanMount(targetCarryingHeavy: false, targetFrozen: false, towerSize: 2));
            Assert.IsFalse(TowerRules.CanMount(true, false, 1));
            Assert.IsFalse(TowerRules.CanMount(false, true, 1));
            Assert.IsFalse(TowerRules.CanMount(false, false, TowerRules.MaxHeight));
        }
    }

    public class NoiseModelTests
    {
        [Test]
        public void Radii_MatchThePlan()
        {
            Assert.AreEqual(6f, NoiseModel.Radius(NoiseKind.Running));
            Assert.AreEqual(4f, NoiseModel.Radius(NoiseKind.HardLanding));
            Assert.AreEqual(7f, NoiseModel.Radius(NoiseKind.Crash));
            Assert.AreEqual(0f, NoiseModel.Radius(NoiseKind.Sneaking));
        }

        [Test]
        public void Hears_WithinRadius_LessThroughWalls()
        {
            Assert.IsTrue(NoiseModel.Hears(Vector3.zero, new Vector3(5, 0, 0), 6f, occluded: false));
            Assert.IsFalse(NoiseModel.Hears(Vector3.zero, new Vector3(5, 0, 0), 6f, occluded: true), "walls halve the radius");
            Assert.IsFalse(NoiseModel.Hears(Vector3.zero, new Vector3(8, 0, 0), 6f, false));
        }
    }
}
