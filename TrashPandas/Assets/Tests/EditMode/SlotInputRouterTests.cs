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

            Assert.AreEqual(new Vector2(0.5f, 1f), parts.LegLeft.Move);
            Assert.AreEqual(new Vector2(0.5f, 1f), parts.LegRight.Move);
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
                [1] = new SlotInput { Move = Vector2.up, JumpPressedAt = float.NegativeInfinity },
                [2] = new SlotInput { Move = Vector2.right, JumpPressedAt = float.NegativeInfinity },
            };

            var parts = SlotInputRouter.Route(slots, inputs);

            Assert.AreEqual(Vector2.up, parts.LegLeft.Move);
            Assert.AreEqual(Vector2.right, parts.LegRight.Move);
        }

        [Test]
        public void Route_BothArmsSlot_GrabOneUsesPreferredHand()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 1); // Arms
            var point = new Vector3(0.5f, 1f, 0.5f);
            var inputs = new Dictionary<int, SlotInput>
            {
                [1] = new SlotInput { AimPoint = point, HasAimPoint = true, GrabOne = true, PreferLeftHand = false },
            };

            var parts = SlotInputRouter.Route(slots, inputs);

            Assert.IsFalse(parts.ArmLeft.Reach);
            Assert.IsTrue(parts.ArmRight.Reach);
            Assert.AreEqual(point, parts.ArmRight.Point);
            Assert.IsTrue(parts.ArmRight.HasPoint);
        }

        [Test]
        public void Route_BothArmsSlot_GrabBothUsesBothHands()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 1); // Arms
            var inputs = new Dictionary<int, SlotInput> { [1] = new SlotInput { GrabBoth = true } };

            var parts = SlotInputRouter.Route(slots, inputs);

            Assert.IsTrue(parts.ArmLeft.Reach);
            Assert.IsTrue(parts.ArmRight.Reach);
        }

        [Test]
        public void Route_SingleArmSlot_EitherButtonReaches()
        {
            var slots = new SlotSystem(5);
            slots.TryEnter(9, 3); // ArmRight
            var parts = SlotInputRouter.Route(slots, new Dictionary<int, SlotInput> { [9] = new SlotInput { GrabOne = true, PreferLeftHand = true } });
            Assert.IsTrue(parts.ArmRight.Reach, "a one-arm player always uses their arm");
            Assert.IsFalse(parts.ArmLeft.Reach);

            parts = SlotInputRouter.Route(slots, new Dictionary<int, SlotInput> { [9] = new SlotInput { GrabBoth = true } });
            Assert.IsTrue(parts.ArmRight.Reach);
        }

        [Test]
        public void Route_UpperSlotOfTwoPlayers_ControlsArmsAndHead()
        {
            var slots = new SlotSystem(2);
            slots.TryEnter(1, 1); // Arms | Head
            var inputs = new Dictionary<int, SlotInput>
            {
                [1] = new SlotInput { Aim = Vector3.left, GrabOne = true, PreferLeftHand = true },
            };

            var parts = SlotInputRouter.Route(slots, inputs);

            Assert.AreEqual(Vector3.left, parts.Head.Aim);
            Assert.IsTrue(parts.ArmLeft.Reach);
        }

        [Test]
        public void Route_EmptySlotsAndMissingInputs_StayIdle()
        {
            var slots = new SlotSystem(3);
            slots.TryEnter(1, 0); // Legs occupied, but player 1 sent no input
            var parts = SlotInputRouter.Route(slots, new Dictionary<int, SlotInput>());

            Assert.AreEqual(Vector2.zero, parts.LegLeft.Move);
            Assert.IsTrue(float.IsNegativeInfinity(parts.LegLeft.JumpPressedAt));
            Assert.IsTrue(float.IsNegativeInfinity(parts.LegRight.JumpPressedAt));
        }
    }
}
