using NUnit.Framework;
using UnityEngine;
using TrashPandas.Core.Raccoons;

namespace TrashPandas.Tests
{
    public class GadgetTests
    {
        [Test]
        public void StartingKit()
        {
            var k = GadgetKit.Starting();
            Assert.AreEqual(3, k.Count(Gadget.Pebble));
            Assert.AreEqual(1, k.Count(Gadget.SmokeBomb));
            Assert.AreEqual(2, k.Count(Gadget.Banana));
        }

        [Test]
        public void LookingUp_ThrowsFarther()
        {
            var level = GadgetThrow.Velocity(Gadget.Pebble, new UnityEngine.Vector3(0f, 0f, 1f));
            var up = GadgetThrow.Velocity(Gadget.Pebble, new UnityEngine.Vector3(0f, 0.5f, 0.86f));
            var down = GadgetThrow.Velocity(Gadget.Pebble, new UnityEngine.Vector3(0f, -0.4f, 0.9f));
            Assert.Greater(GadgetThrow.FlatRange(up), GadgetThrow.FlatRange(level));
            Assert.Greater(GadgetThrow.FlatRange(level), GadgetThrow.FlatRange(down));
            Assert.Greater(level.z, 0f, "forward");
        }

        [Test]
        public void AimAtAPoint_LandsExactlyThere()
        {
            var o = new Vector3(1f, 0.6f, 2f);
            var target = new Vector3(7f, 0f, 9f);
            var v = GadgetThrow.VelocityTo(o, target, out float t);
            Vector3 at = o + v * t + 0.5f * Physics.gravity * t * t;
            Assert.Less(Vector3.Distance(at, target), 0.01f);
            Assert.Greater(t, 0.3f, "a visible arc, not a laser");
        }

        [Test]
        public void TooFar_IsClampedToTheToolsRange()
        {
            var o = Vector3.zero;
            var far = new Vector3(0f, 0f, 100f);
            var clamped = GadgetThrow.ClampToRange(Gadget.Banana, o, far, out bool tooFar);
            Assert.IsTrue(tooFar);
            Assert.AreEqual(GadgetThrow.MaxRange(Gadget.Banana), new Vector2(clamped.x, clamped.z).magnitude, 0.01f);
            GadgetThrow.ClampToRange(Gadget.Pebble, o, new Vector3(0f, 0f, 5f), out tooFar);
            Assert.IsFalse(tooFar);
        }

        [Test]
        public void EachTool_ShowsItsEffectRadius()
        {
            Assert.Greater(GadgetThrow.EffectRadius(Gadget.SmokeBomb), 2f);
            Assert.Greater(GadgetThrow.EffectRadius(Gadget.Pebble), GadgetThrow.EffectRadius(Gadget.Banana), "the clack carries");
        }

        [Test]
        public void BananasAreTossedShort_PebblesFly()
        {
            var aim = new UnityEngine.Vector3(0f, 0f, 1f);
            Assert.Greater(GadgetThrow.FlatRange(GadgetThrow.Velocity(Gadget.Pebble, aim)), GadgetThrow.FlatRange(GadgetThrow.Velocity(Gadget.Banana, aim)));
        }

        [Test]
        public void Peel_SlipsWhoeverStepsOnIt()
        {
            Assert.IsTrue(BananaPeel.Slips(new UnityEngine.Vector3(1f, 0f, 1f), new UnityEngine.Vector3(1.3f, 0f, 1.1f), moving: true));
            Assert.IsFalse(BananaPeel.Slips(new UnityEngine.Vector3(1f, 0f, 1f), new UnityEngine.Vector3(2f, 0f, 1f), true), "too far");
            Assert.IsFalse(BananaPeel.Slips(new UnityEngine.Vector3(1f, 0f, 1f), new UnityEngine.Vector3(1.1f, 0f, 1f), moving: false), "standing still on it is fine");
        }

        [Test]
        public void Using_SpendsOne_UntilEmpty()
        {
            var k = GadgetKit.Starting();
            Assert.IsTrue(k.TryUse(Gadget.SmokeBomb));
            Assert.IsFalse(k.TryUse(Gadget.SmokeBomb), "none left");
            Assert.AreEqual(0, k.Count(Gadget.SmokeBomb));
        }

        [Test]
        public void PickingUp_AddsUpToACap()
        {
            var k = new GadgetKit();
            for (int i = 0; i < 20; i++) k.Add(Gadget.Pebble, 1);
            Assert.AreEqual(GadgetKit.MaxPerKind, k.Count(Gadget.Pebble));
        }

        [Test]
        public void Smoke_HidesInside_Only()
        {
            var s = new SmokeCloud(UnityEngine.Vector3.zero, radius: 3f, until: 10f);
            Assert.IsTrue(s.Hides(new UnityEngine.Vector3(2f, 0f, 0f), now: 5f));
            Assert.IsFalse(s.Hides(new UnityEngine.Vector3(4f, 0f, 0f), 5f));
            Assert.IsFalse(s.Hides(UnityEngine.Vector3.zero, now: 11f), "it clears");
        }
    }
}
