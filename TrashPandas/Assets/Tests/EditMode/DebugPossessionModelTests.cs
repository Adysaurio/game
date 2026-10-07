using NUnit.Framework;
using TrashPandas.Core.Debugging;
using TrashPandas.Core.Trenchcoat;

namespace TrashPandas.Tests
{
    public class DebugPossessionModelTests
    {
        [Test]
        public void New_FillsEverySlot_ActiveIsPlayerZeroInside()
        {
            var slots = new SlotSystem(3);
            var model = new DebugPossessionModel(slots);

            Assert.AreEqual(BodyPart.None, slots.MissingParts);
            Assert.AreEqual(0, model.ActivePlayerId);
            Assert.IsFalse(model.ActiveIsOutside);
        }

        [Test]
        public void CycleNext_WrapsAroundAllInsidePlayers()
        {
            var model = new DebugPossessionModel(new SlotSystem(3));
            model.CycleNext(); Assert.AreEqual(1, model.ActivePlayerId);
            model.CycleNext(); Assert.AreEqual(2, model.ActivePlayerId);
            model.CycleNext(); Assert.AreEqual(0, model.ActivePlayerId);
        }

        [Test]
        public void LeaveCoat_MakesActiveOutside_AndPartMissing()
        {
            var slots = new SlotSystem(3);
            var model = new DebugPossessionModel(slots);
            model.CycleNext(); // player 1 = Arms

            Assert.IsTrue(model.LeaveCoat());
            Assert.IsTrue(model.ActiveIsOutside);
            Assert.AreEqual(BodyPart.Arms, slots.MissingParts);
            Assert.IsFalse(model.LeaveCoat(), "leaving twice is a no-op");
        }

        [Test]
        public void CycleNext_WhileOutside_DoesNothing()
        {
            var model = new DebugPossessionModel(new SlotSystem(3));
            model.LeaveCoat();
            Assert.IsFalse(model.CycleNext());
            Assert.AreEqual(0, model.ActivePlayerId);
            Assert.IsTrue(model.ActiveIsOutside);
        }

        [Test]
        public void ReturnToCoat_TakesFirstFreeSlot()
        {
            var slots = new SlotSystem(3);
            var model = new DebugPossessionModel(slots);
            model.LeaveCoat(); // player 0 frees slot 0

            Assert.IsTrue(model.ReturnToCoat());
            Assert.IsFalse(model.ActiveIsOutside);
            Assert.AreEqual(0, slots.SlotOf(0));
            Assert.AreEqual(BodyPart.None, slots.MissingParts);
        }

        [Test]
        public void ReturnToCoat_WhenFull_Fails()
        {
            var slots = new SlotSystem(2);
            var model = new DebugPossessionModel(slots);
            Assert.IsFalse(model.ReturnToCoat(), "already inside and coat full");
            Assert.AreEqual(0, slots.SlotOf(0));
            Assert.AreEqual(1, slots.SlotOf(1));
        }

        [Test]
        public void TrySelect_InsidePlayer_BecomesActive()
        {
            var model = new DebugPossessionModel(new SlotSystem(4));
            Assert.IsTrue(model.TrySelect(2));
            Assert.AreEqual(2, model.ActivePlayerId);
        }

        [Test]
        public void TrySelect_InvalidOrWhileOutside_DoesNothing()
        {
            var model = new DebugPossessionModel(new SlotSystem(3));
            Assert.IsFalse(model.TrySelect(7), "no such player");
            Assert.IsFalse(model.TrySelect(-1));
            Assert.AreEqual(0, model.ActivePlayerId);

            model.LeaveCoat();
            Assert.IsFalse(model.TrySelect(1), "can't abandon the loose raccoon");
            Assert.AreEqual(0, model.ActivePlayerId);
        }
    }
}
