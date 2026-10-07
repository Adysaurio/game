using NUnit.Framework;
using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class TrenchcoatIntentMixerTests
    {
        static readonly MixerSettings Settings = new MixerSettings();
        const float Eps = 1e-3f;

        static PartInputs Legs(Vector2 left, Vector2 right)
        {
            var input = PartInputs.Idle;
            input.LegLeft.Move = left;
            input.LegRight.Move = right;
            return input;
        }

        static void AssertVec(Vector2 expected, Vector2 actual, string msg = "")
        {
            Assert.AreEqual(expected.x, actual.x, Eps, msg + " (x)");
            Assert.AreEqual(expected.y, actual.y, Eps, msg + " (y)");
        }

        [Test]
        public void Mix_AgreeingLegs_MoveFullSpeed_NoDiscord()
        {
            var intent = TrenchcoatIntentMixer.Mix(Legs(Vector2.up, Vector2.up), BodyPart.All, 0f, Settings);
            AssertVec(Vector2.up, intent.Move);
            Assert.AreEqual(0f, intent.Discord, Eps);
            Assert.IsFalse(intent.Collapsed);
        }

        [Test]
        public void Mix_PerpendicularLegs_AverageDirection_HalfDiscord()
        {
            var intent = TrenchcoatIntentMixer.Mix(Legs(Vector2.up, Vector2.right), BodyPart.All, 0f, Settings);
            AssertVec(new Vector2(0.5f, 0.5f), intent.Move);
            Assert.AreEqual(0.5f, intent.Discord, Eps);
        }

        [Test]
        public void Mix_OppositeLegs_CancelAndReportFullDiscord()
        {
            var intent = TrenchcoatIntentMixer.Mix(Legs(Vector2.up, Vector2.down), BodyPart.All, 0f, Settings);
            AssertVec(Vector2.zero, intent.Move);
            Assert.AreEqual(1f, intent.Discord, Eps);
        }

        [Test]
        public void Mix_OneLegIdle_NoDiscord()
        {
            var intent = TrenchcoatIntentMixer.Mix(Legs(Vector2.up, Vector2.zero), BodyPart.All, 0f, Settings);
            AssertVec(new Vector2(0f, 0.5f), intent.Move);
            Assert.AreEqual(0f, intent.Discord, Eps);
        }

        [Test]
        public void Mix_SanitizesNaNMoveAndAim()
        {
            var input = Legs(new Vector2(float.NaN, 1f), new Vector2(3f, 0f));
            input.ArmLeft.Aim = new Vector3(float.NaN, 0f, 1f);
            input.ArmLeft.Reach = true;
            var intent = TrenchcoatIntentMixer.Mix(input, BodyPart.All, 0f, Settings);
            AssertVec(new Vector2(0.5f, 0f), intent.Move, "NaN leg ignored, over-long leg clamped to length 1");
            Assert.AreEqual(Vector3.zero, intent.LeftAim);
        }

        [Test]
        public void Mix_NoLegs_Collapses()
        {
            var intent = TrenchcoatIntentMixer.Mix(Legs(Vector2.up, Vector2.up), BodyPart.Arms | BodyPart.Head, 0f, Settings);
            Assert.IsTrue(intent.Collapsed);
            AssertVec(Vector2.zero, intent.Move);
            Assert.IsFalse(intent.Jump);
            Assert.IsTrue(intent.LeftLegLimp && intent.RightLegLimp);
        }

        [Test]
        public void Mix_OneLeg_SlowerAndDriftsTowardMissingSide()
        {
            var onlyLeft = BodyPart.All & ~BodyPart.LegRight;
            var intent = TrenchcoatIntentMixer.Mix(Legs(Vector2.up, Vector2.zero), onlyLeft, 0f, Settings);
            Assert.AreEqual(Settings.LimpSpeedFactor, intent.Move.magnitude, Eps);
            Assert.Greater(intent.Move.x, 0f, "missing right leg drags to the right");
            Assert.IsTrue(intent.RightLegLimp);
            Assert.IsFalse(intent.LeftLegLimp);

            var onlyRight = BodyPart.All & ~BodyPart.LegLeft;
            var intent2 = TrenchcoatIntentMixer.Mix(Legs(Vector2.zero, Vector2.up), onlyRight, 0f, Settings);
            Assert.Less(intent2.Move.x, 0f, "missing left leg drags to the left");
        }

        [Test]
        public void Mix_LegsJumpWithinWindow_Jumps()
        {
            var input = PartInputs.Idle;
            input.LegLeft.JumpPressedAt = 10.00f;
            input.LegRight.JumpPressedAt = 10.20f;
            Assert.IsTrue(TrenchcoatIntentMixer.Mix(input, BodyPart.All, now: 10.21f, Settings).Jump);
        }

        [Test]
        public void Mix_LegsJumpOutsideWindow_NoJump()
        {
            var input = PartInputs.Idle;
            input.LegLeft.JumpPressedAt = 10.00f;
            input.LegRight.JumpPressedAt = 10.30f;
            Assert.IsFalse(TrenchcoatIntentMixer.Mix(input, BodyPart.All, now: 10.31f, Settings).Jump, "too far apart");

            input.LegRight.JumpPressedAt = 10.00f;
            Assert.IsFalse(TrenchcoatIntentMixer.Mix(input, BodyPart.All, now: 11.00f, Settings).Jump, "stale");
        }

        [Test]
        public void Mix_OneLeg_NeverJumps()
        {
            var input = PartInputs.Idle;
            input.LegLeft.JumpPressedAt = 5f;
            input.LegRight.JumpPressedAt = 5f;
            Assert.IsFalse(TrenchcoatIntentMixer.Mix(input, BodyPart.All & ~BodyPart.LegRight, now: 5f, Settings).Jump);
        }

        [Test]
        public void Mix_IdleInputs_NeverJump()
        {
            Assert.IsFalse(TrenchcoatIntentMixer.Mix(PartInputs.Idle, BodyPart.All, now: 0f, Settings).Jump);
        }

        [Test]
        public void Mix_Arms_ReachAlongAim_MissingArmLimp()
        {
            var input = PartInputs.Idle;
            input.ArmLeft.Aim = new Vector3(0f, 0f, 2f);
            input.ArmLeft.Reach = true;
            input.ArmRight.Reach = true;
            var intent = TrenchcoatIntentMixer.Mix(input, BodyPart.All & ~BodyPart.ArmRight, 0f, Settings);

            Assert.IsTrue(intent.LeftReach);
            Assert.AreEqual(Vector3.forward, intent.LeftAim, "aim is normalized");
            Assert.IsTrue(intent.RightArmLimp);
            Assert.IsFalse(intent.RightReach, "a limp arm cannot reach");
        }

        [Test]
        public void Mix_Head_FollowsAim_OrSlumps()
        {
            var input = PartInputs.Idle;
            input.Head.Aim = Vector3.right;
            Assert.AreEqual(Vector3.right, TrenchcoatIntentMixer.Mix(input, BodyPart.All, 0f, Settings).HeadAim);

            var slumped = TrenchcoatIntentMixer.Mix(input, BodyPart.All & ~BodyPart.Head, 0f, Settings);
            Assert.IsTrue(slumped.HeadSlumped);
            Assert.AreEqual(Vector3.zero, slumped.HeadAim);
        }
    }
}
