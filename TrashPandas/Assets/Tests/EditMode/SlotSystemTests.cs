using System.Collections.Generic;
using NUnit.Framework;
using TrashPandas.Core.Trenchcoat;

namespace TrashPandas.Tests
{
    public class SlotSystemTests
    {
        [Test]
        public void NewSystem_AllSlotsFree_EverythingMissing()
        {
            var slots = new SlotSystem(3);
            Assert.AreEqual(3, slots.SlotCount);
            Assert.AreEqual(0, slots.FirstFreeSlot());
            Assert.AreEqual(BodyPart.None, slots.ControlledParts);
            Assert.AreEqual(BodyPart.All, slots.MissingParts);
        }

        [Test]
        public void TryEnter_FreeSlot_TakesItsParts()
        {
            var slots = new SlotSystem(3);
            Assert.IsTrue(slots.TryEnter(playerId: 7, slotIndex: 1));
            Assert.AreEqual(7, slots.OccupantOf(1));
            Assert.AreEqual(1, slots.SlotOf(7));
            Assert.AreEqual(BodyPart.Arms, slots.ControlledParts);
            Assert.AreEqual(BodyPart.Legs | BodyPart.Head, slots.MissingParts);
        }

        [Test]
        public void TryEnter_OccupiedSlot_ReturnsFalse()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 0);
            Assert.IsFalse(slots.TryEnter(2, 0));
            Assert.AreEqual(1, slots.OccupantOf(0));
            Assert.IsNull(slots.SlotOf(2));
        }

        [Test]
        public void TryEnter_PlayerAlreadyInside_ReturnsFalse()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 0);
            Assert.IsFalse(slots.TryEnter(1, 2));
            Assert.AreEqual(0, slots.SlotOf(1));
            Assert.IsNull(slots.OccupantOf(2));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void TryEnter_InvalidSlot_ReturnsFalse(int slotIndex)
        {
            var slots = new SlotSystem(3);
            Assert.IsFalse(slots.TryEnter(1, slotIndex));
        }

        [Test]
        public void Leave_FreesSlot_AndPartGoesMissing()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 0);
            slots.TryEnter(2, 1);
            slots.TryEnter(3, 2);
            Assert.AreEqual(BodyPart.None, slots.MissingParts);

            Assert.IsTrue(slots.Leave(2));
            Assert.IsNull(slots.OccupantOf(1));
            Assert.AreEqual(BodyPart.Arms, slots.MissingParts);
            Assert.AreEqual(1, slots.FirstFreeSlot());
        }

        [Test]
        public void Leave_WhenNotInside_ReturnsFalse()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 0);
            Assert.IsTrue(slots.Leave(1));
            Assert.IsFalse(slots.Leave(1));
            Assert.IsFalse(slots.Leave(99));
        }

        [Test]
        public void FirstFreeSlot_WhenFull_IsNull()
        {
            var slots = new SlotSystem(2);
            slots.TryEnter(1, 0);
            slots.TryEnter(2, 1);
            Assert.IsNull(slots.FirstFreeSlot());
        }

        [Test]
        public void MissingPartsChanged_FiresOnlyOnRealChanges()
        {
            var slots = new SlotSystem(3);
            var events = new List<BodyPart>();
            slots.MissingPartsChanged += events.Add;

            slots.TryEnter(1, 0);   // legs arrive
            slots.TryEnter(1, 1);   // rejected: no event
            slots.Leave(2);         // not inside: no event
            slots.Leave(1);         // legs leave

            CollectionAssert.AreEqual(
                new[] { BodyPart.Arms | BodyPart.Head, BodyPart.All },
                events);
        }
    }
}
