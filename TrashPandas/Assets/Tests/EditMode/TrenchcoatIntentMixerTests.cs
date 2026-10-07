using NUnit.Framework;
using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class TrenchcoatIntentMixerTests
    {
        static readonly MixerSettings Settings = new MixerSettings();
        const float Eps = 1e-4f;

        static PartInputs Legs(float driveL, float driveR, float steer = 0f)
        {
            var input = PartInputs.Idle;
            input.LegLeft.Drive = driveL;
            input.LegRight.Drive = driveR;
            input.LegLeft.Steer = steer;
            input.LegRight.Steer = steer;
            return input;
        }

        [Test]
        public void Mix_SyncedLegs_WalkStraight()
        {
            var intent = TrenchcoatIntentMixer.Mix(Legs(1f, 1f), BodyPart.All, 0f, Settings);
            Assert.AreEqual(1f, intent.Forward, Eps);
            Assert.AreEqual(0f, intent.Turn, Eps);
            Assert.IsFalse(intent.Collapsed);
        }

        [Test]
        public void Mix_LeftLegFaster_TurnsRight()
        {
            var intent = TrenchcoatIntentMixer.Mix(Legs(1f, 0f), BodyPart.All, 0f, Settings);
            Assert.AreEqual(0.5f, intent.Forward, Eps);
            Assert.Greater(intent.Turn, 0f);
        }

        [Test]
        public void Mix_ClampsOutOfRangeAndNaNInputs()
        {
            var input = Legs(1.41f, 1.41f, steer: 5f);
            var intent = TrenchcoatIntentMixer.Mix(input, BodyPart.All, 0f, Settings);
            Assert.AreEqual(1f, intent.Forward, Eps);
            Assert.AreEqual(1f, intent.Turn, Eps);

            var nan = Legs(float.NaN, float.NaN, steer: float.NaN);
            var nanIntent = TrenchcoatIntentMixer.Mix(nan, BodyPart.All, 0f, Settings);
            Assert.AreEqual(0f, nanIntent.Forward, Eps);
            Assert.AreEqual(0f, nanIntent.Turn, Eps);
        }

        [Test]
        public void Mix_NoLegs_CollapsesAndCannotMove()
        {
            var intent = TrenchcoatIntentMixer.Mix(Legs(1f, 1f), BodyPart.Arms | BodyPart.Head, 0f, Settings);
            Assert.IsTrue(intent.Collapsed);
            Assert.AreEqual(0f, intent.Forward, Eps);
            Assert.AreEqual(0f, intent.Turn, Eps);
            Assert.IsFalse(intent.Jump);
        }

        [Test]
        public void Mix_OneLeg_LimpsSlowerAndDriftsTowardMissingSide()
        {
            var onlyLeft = BodyPart.All & ~BodyPart.LegRight;
            var intent = TrenchcoatIntentMixer.Mix(Legs(1f, 0f), onlyLeft, 0f, Settings);
            Assert.AreEqual(Settings.LimpSpeedFactor, intent.Forward, Eps);
            Assert.Greater(intent.Turn, 0f, "missing right leg drags the body to the right");

            var onlyRight = BodyPart.All & ~BodyPart.LegLeft;
            var intent2 = TrenchcoatIntentMixer.Mix(Legs(0f, 1f), onlyRight, 0f, Settings);
            Assert.Less(intent2.Turn, 0f, "missing left leg drags the body to the left");
        }

        [Test]
        public void Mix_LegsJumpWithinWindow_Jumps()
        {
            var input = PartInputs.Idle;
            input.LegLeft.JumpPressedAt = 10.00f;
            input.LegRight.JumpPressedAt = 10.20f;
            var intent = TrenchcoatIntentMixer.Mix(input, BodyPart.All, now: 10.21f, Settings);
            Assert.IsTrue(intent.Jump);
        }

        [Test]
        public void Mix_LegsJumpOutsideWindow_NoJump()
        {
            var input = PartInputs.Idle;
            input.LegLeft.JumpPressedAt = 10.00f;
            input.LegRight.JumpPressedAt = 10.30f;
            Assert.IsFalse(TrenchcoatIntentMixer.Mix(input, BodyPart.All, now: 10.31f, Settings).Jump,
                "presses too far apart");

            input.LegLeft.JumpPressedAt = 10.00f;
            input.LegRight.JumpPressedAt = 10.00f;
            Assert.IsFalse(TrenchcoatIntentMixer.Mix(input, BodyPart.All, now: 11.00f, Settings).Jump,
                "presses are stale");
        }

        [Test]
        public void Mix_OneLeg_NeverJumps()
        {
            var input = PartInputs.Idle;
            input.LegLeft.JumpPressedAt = 5f;
            input.LegRight.JumpPressedAt = 5f;
            var onlyLeft = BodyPart.All & ~BodyPart.LegRight;
            Assert.IsFalse(TrenchcoatIntentMixer.Mix(input, onlyLeft, now: 5f, Settings).Jump);
        }

        [Test]
        public void Mix_IdleInputs_NeverJump()
        {
            Assert.IsFalse(TrenchcoatIntentMixer.Mix(PartInputs.Idle, BodyPart.All, now: 0f, Settings).Jump);
        }

        [Test]
        public void Mix_MissingArm_IsLimp_PresentArmFollowsTarget()
        {
            var input = PartInputs.Idle;
            input.ArmLeft.HandTarget = new Vector3(-0.3f, 1f, 0.5f);
            input.ArmLeft.Grab = true;
            input.ArmRight.Grab = true;
            var present = BodyPart.All & ~BodyPart.ArmRight;

            var intent = TrenchcoatIntentMixer.Mix(input, present, 0f, Settings);

            Assert.IsFalse(intent.LeftArmLimp);
            Assert.AreEqual(input.ArmLeft.HandTarget, intent.LeftHandTarget);
            Assert.IsTrue(intent.LeftGrab);
            Assert.IsTrue(intent.RightArmLimp);
            Assert.IsFalse(intent.RightGrab, "a limp arm cannot grab");
        }

        [Test]
        public void Mix_MissingHead_Slumps_IgnoresLook()
        {
            var input = PartInputs.Idle;
            input.Head.Yaw = 45f;
            var intent = TrenchcoatIntentMixer.Mix(input, BodyPart.All & ~BodyPart.Head, 0f, Settings);
            Assert.IsTrue(intent.HeadSlumped);
            Assert.AreEqual(0f, intent.HeadYaw, Eps);

            var withHead = TrenchcoatIntentMixer.Mix(input, BodyPart.All, 0f, Settings);
            Assert.IsFalse(withHead.HeadSlumped);
            Assert.AreEqual(45f, withHead.HeadYaw, Eps);
        }
        [Test]
        public void Mix_Strafe_AveragesBothLegs_AndIsClamped()
        {
            var input = PartInputs.Idle;
            input.LegLeft.Strafe = 1f;
            input.LegRight.Strafe = 0f;
            Assert.AreEqual(0.5f, TrenchcoatIntentMixer.Mix(input, BodyPart.All, 0f, Settings).Strafe, Eps);

            input.LegLeft.Strafe = 3f;
            input.LegRight.Strafe = float.NaN;
            Assert.AreEqual(0.5f, TrenchcoatIntentMixer.Mix(input, BodyPart.All, 0f, Settings).Strafe, Eps);
        }

        [Test]
        public void Mix_OneLeg_StrafeIsSlowed_NoLegs_NoStrafe()
        {
            var input = PartInputs.Idle;
            input.LegLeft.Strafe = 1f;
            input.LegRight.Strafe = 1f;
            var onlyLeft = BodyPart.All & ~BodyPart.LegRight;
            Assert.AreEqual(Settings.LimpSpeedFactor, TrenchcoatIntentMixer.Mix(input, onlyLeft, 0f, Settings).Strafe, Eps);
            Assert.AreEqual(0f, TrenchcoatIntentMixer.Mix(input, BodyPart.Arms | BodyPart.Head, 0f, Settings).Strafe, Eps);
        }

        [Test]
        public void Mix_MissingLeg_IsMarkedLimp()
        {
            var onlyLeft = BodyPart.All & ~BodyPart.LegRight;
            var intent = TrenchcoatIntentMixer.Mix(PartInputs.Idle, onlyLeft, 0f, Settings);
            Assert.IsFalse(intent.LeftLegLimp);
            Assert.IsTrue(intent.RightLegLimp);

            var none = TrenchcoatIntentMixer.Mix(PartInputs.Idle, BodyPart.Arms, 0f, Settings);
            Assert.IsTrue(none.LeftLegLimp);
            Assert.IsTrue(none.RightLegLimp);
        }
    }
}
