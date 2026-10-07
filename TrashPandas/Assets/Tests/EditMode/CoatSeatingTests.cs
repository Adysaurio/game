using NUnit.Framework;
using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class CoatSeatingTests
    {
        static SlotSystem Full3()
        {
            var slots = new SlotSystem(3);
            for (int i = 0; i < 3; i++) slots.TryEnter(i, i);
            return slots;
        }

        [Test]
        public void TryReturn_CloseEnoughAndFreeSeat_TakesFirstFreeSeat()
        {
            var slots = Full3();
            slots.Leave(1);
            Assert.IsTrue(CoatSeating.TryReturn(slots, 1, new Vector3(1f, 0f, 0f), Vector3.zero, 1.6f, out int seat));
            Assert.AreEqual(1, seat);
            Assert.AreEqual(1, slots.SlotOf(1));
        }

        [Test]
        public void TryReturn_TooFar_Refused()
        {
            var slots = Full3();
            slots.Leave(1);
            Assert.IsFalse(CoatSeating.TryReturn(slots, 1, new Vector3(5f, 0f, 0f), Vector3.zero, 1.6f, out _));
            Assert.IsNull(slots.SlotOf(1));
        }

        [Test]
        public void TryReturn_IgnoresHeightDifference()
        {
            var slots = Full3();
            slots.Leave(2);
            Assert.IsTrue(CoatSeating.TryReturn(slots, 2, new Vector3(0.5f, 1.2f, 0f), Vector3.zero, 1.6f, out _),
                "a raccoon on a table right next to the coat can hop back in");
        }

        [Test]
        public void TryReturn_AlreadyInside_Refused()
        {
            var slots = Full3();
            Assert.IsFalse(CoatSeating.TryReturn(slots, 0, Vector3.zero, Vector3.zero, 1.6f, out _));
        }

        [Test]
        public void SpawnBeside_PutsRaccoonToTheCoatsRight()
        {
            Vector3 p = CoatSeating.SpawnBeside(new Vector3(2f, 0f, 3f), Quaternion.identity);
            Assert.Greater(p.x, 2.5f);
            Assert.AreEqual(3f, p.z, 1e-4f);
        }
    }
}
