using System;
using System.Linq;
using NUnit.Framework;
using TrashPandas.Core.Trenchcoat;

namespace TrashPandas.Tests
{
    public class SlotLayoutTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Layout_HasOneSlotPerPlayer_CoversWholeBody_WithoutOverlap(int players)
        {
            var slots = SlotLayout.ForPlayerCount(players);

            Assert.AreEqual(players, slots.Count);
            var union = slots.Aggregate(BodyPart.None, (acc, p) => acc | p);
            Assert.AreEqual(BodyPart.All, union);
            for (int i = 0; i < slots.Count; i++)
                for (int j = i + 1; j < slots.Count; j++)
                    Assert.AreEqual(BodyPart.None, slots[i] & slots[j], $"slots {i} and {j} overlap");
        }

        [Test]
        public void Layout_MatchesSpec()
        {
            CollectionAssert.AreEqual(
                new[] { BodyPart.Legs, BodyPart.Arms | BodyPart.Head },
                SlotLayout.ForPlayerCount(2));
            CollectionAssert.AreEqual(
                new[] { BodyPart.Legs, BodyPart.Arms, BodyPart.Head },
                SlotLayout.ForPlayerCount(3));
            CollectionAssert.AreEqual(
                new[] { BodyPart.LegLeft, BodyPart.LegRight, BodyPart.Arms, BodyPart.Head },
                SlotLayout.ForPlayerCount(4));
            CollectionAssert.AreEqual(
                new[] { BodyPart.LegLeft, BodyPart.LegRight, BodyPart.ArmLeft, BodyPart.ArmRight, BodyPart.Head },
                SlotLayout.ForPlayerCount(5));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(6)]
        public void Layout_RejectsUnsupportedPlayerCounts(int players)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SlotLayout.ForPlayerCount(players));
        }
    }
}
