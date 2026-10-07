using NUnit.Framework;
using TrashPandas.Core.Trenchcoat;
using TrashPandas.Runtime.Net;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.Tests.Net
{
    public class NetSerializationTests
    {
        static T RoundTrip<T>(T value) where T : unmanaged, INetworkSerializable
        {
            using var writer = new FastBufferWriter(1024, Allocator.Temp);
            writer.WriteNetworkSerializable(value);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out T result);
            return result;
        }

        [Test]
        public void NetSlotInput_RoundTrips_AllFields()
        {
            var input = new SlotInput
            {
                Move = new Vector2(0.3f, -0.7f),
                Crouch = true,
                JumpPressedAt = 12.5f,
                Aim = new Vector3(0.1f, -0.2f, 0.97f),
                AimPoint = new Vector3(1f, 0.9f, 4f),
                HasAimPoint = true,
                GrabOne = true,
                GrabBoth = false,
                PreferLeftHand = true,
            };
            var back = RoundTrip(new NetSlotInput { Sequence = 77, Input = input });

            Assert.AreEqual(77u, back.Sequence);
            Assert.AreEqual(input.Move, back.Input.Move);
            Assert.AreEqual(input.Crouch, back.Input.Crouch);
            Assert.AreEqual(input.JumpPressedAt, back.Input.JumpPressedAt);
            Assert.AreEqual(input.Aim, back.Input.Aim);
            Assert.AreEqual(input.AimPoint, back.Input.AimPoint);
            Assert.AreEqual(input.HasAimPoint, back.Input.HasAimPoint);
            Assert.AreEqual(input.GrabOne, back.Input.GrabOne);
            Assert.AreEqual(input.GrabBoth, back.Input.GrabBoth);
            Assert.AreEqual(input.PreferLeftHand, back.Input.PreferLeftHand);
        }

        [Test]
        public void NetSlotInput_KeepsNeverJumpedMarker()
        {
            var back = RoundTrip(new NetSlotInput { Sequence = 1, Input = SlotInput.Idle });
            Assert.IsTrue(float.IsNegativeInfinity(back.Input.JumpPressedAt));
        }

        [Test]
        public void BodyVisualState_RoundTrips_WhatClientsAnimate()
        {
            var intent = new BodyIntent
            {
                Move = new Vector2(0.5f, 0.5f),
                Discord = 0.4f,
                Crouch = true,
                LeftLegLimp = true,
                RightArmLimp = true,
                LeftReach = true,
                HasLeftPoint = true,
                LeftPoint = new Vector3(0.2f, 1f, 3f),
                LeftAim = Vector3.forward,
                HeadAim = Vector3.right,
            };
            var back = RoundTrip(BodyVisualState.From(intent)).ToIntent();

            Assert.AreEqual(intent.Move, back.Move);
            Assert.AreEqual(intent.Discord, back.Discord);
            Assert.IsTrue(back.Crouch && back.LeftLegLimp && back.RightArmLimp && back.LeftReach && back.HasLeftPoint);
            Assert.IsFalse(back.Collapsed || back.RightLegLimp || back.LeftArmLimp || back.RightReach || back.HeadSlumped || back.HasRightPoint);
            Assert.AreEqual(intent.LeftPoint, back.LeftPoint);
            Assert.AreEqual(intent.LeftAim, back.LeftAim);
            Assert.AreEqual(intent.HeadAim, back.HeadAim);
            Assert.IsFalse(back.Jump, "jumps are simulated on the host only, never replayed by clients");
        }

        [Test]
        public void BodyVisualState_CollapsedAndSlumped()
        {
            var back = RoundTrip(BodyVisualState.From(new BodyIntent { Collapsed = true, HeadSlumped = true, LeftArmLimp = true })).ToIntent();
            Assert.IsTrue(back.Collapsed && back.HeadSlumped && back.LeftArmLimp);
        }
    }
}
