using NUnit.Framework;
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
