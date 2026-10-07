using System.Collections.Generic;
using NUnit.Framework;
using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class SlotInputRouterTests
    {
        [Test]
        public void Route_LegsSlot_DrivesBothLegsWithSameInput()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 0); // Legs
            var inputs = new Dictionary<int, SlotInput>
            {
                [1] = new SlotInput { Move = new Vector2(0.5f, 1f), JumpPressedAt = 3f, Crouch = true },
            };

            var parts = SlotInputRouter.Route(slots, inputs);

            Assert.AreEqual(1f, parts.LegLeft.Drive);
            Assert.AreEqual(1f, parts.LegRight.Drive);
            Assert.AreEqual(0.5f, parts.LegLeft.Steer);
            Assert.AreEqual(3f, parts.LegLeft.JumpPressedAt);
            Assert.AreEqual(3f, parts.LegRight.JumpPressedAt);
            Assert.IsTrue(parts.LegRight.Crouch);
        }

        [Test]
        public void Route_SeparateLegSlots_EachLegOwnInput()
        {
            var slots = new SlotSystem(4);
            slots.TryEnter(1, 0); // LegLeft
            slots.TryEnter(2, 1); // LegRight
            var inputs = new Dictionary<int, SlotInput>
            {
                [1] = new SlotInput { Move = new Vector2(0f, 1f), JumpPressedAt = float.NegativeInfinity },
                [2] = new SlotInput { Move = new Vector2(0f, 0.2f), JumpPressedAt = float.NegativeInfinity },
            };

            var parts = SlotInputRouter.Route(slots, inputs);

            Assert.AreEqual(1f, parts.LegLeft.Drive);
            Assert.AreEqual(0.2f, parts.LegRight.Drive);
        }

        [Test]
        public void Route_BothArmsSlot_SpreadsHandsAndSplitsGrabs()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 1); // Arms
            var target = new Vector3(0f, 1f, 0.5f);
            var inputs = new Dictionary<int, SlotInput>
            {
                [1] = new SlotInput { HandTarget = target, PrimaryGrab = true, SecondaryGrab = false },
            };

            var parts = SlotInputRouter.Route(slots, inputs);

            Assert.AreEqual(target + new Vector3(-SlotInputRouter.ArmSpread, 0f, 0f), parts.ArmLeft.HandTarget);
            Assert.AreEqual(target + new Vector3(SlotInputRouter.ArmSpread, 0f, 0f), parts.ArmRight.HandTarget);
            Assert.IsTrue(parts.ArmLeft.Grab);
            Assert.IsFalse(parts.ArmRight.Grab);
        }

        [Test]
        public void Route_SingleArmSlot_UsesExactTargetAndPrimaryGrab()
        {
            var slots = new SlotSystem(5);
            slots.TryEnter(9, 3); // ArmRight
            var target = new Vector3(0.4f, 1.2f, 0.6f);
            var inputs = new Dictionary<int, SlotInput>
            {
                [9] = new SlotInput { HandTarget = target, PrimaryGrab = true },
            };

            var parts = SlotInputRouter.Route(slots, inputs);

            Assert.AreEqual(target, parts.ArmRight.HandTarget);
            Assert.IsTrue(parts.ArmRight.Grab);
            Assert.IsFalse(parts.ArmLeft.Grab);
        }

        [Test]
        public void Route_UpperSlotOfTwoPlayers_ControlsArmsAndHead()
        {
            var slots = new SlotSystem(2);
            slots.TryEnter(1, 1); // Arms | Head
            var inputs = new Dictionary<int, SlotInput>
            {
                [1] = new SlotInput { Look = new Vector2(30f, -10f), PrimaryGrab = true },
            };

            var parts = SlotInputRouter.Route(slots, inputs);

            Assert.AreEqual(30f, parts.Head.Yaw);
            Assert.AreEqual(-10f, parts.Head.Pitch);
            Assert.IsTrue(parts.ArmLeft.Grab);
        }

        [Test]
        public void Route_EmptySlotsAndMissingInputs_StayIdle()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 0); // Legs occupied, but player 1 sent no input
            var parts = SlotInputRouter.Route(slots, new Dictionary<int, SlotInput>());

            Assert.AreEqual(0f, parts.LegLeft.Drive);
            Assert.IsTrue(float.IsNegativeInfinity(parts.LegLeft.JumpPressedAt));
            Assert.IsTrue(float.IsNegativeInfinity(parts.LegRight.JumpPressedAt));
        }
    }
}
