using UnityEngine;

namespace TrashPandas.Core.Trenchcoat
{
    public sealed class MixerSettings
    {
        /// <summary>Max seconds between both legs' jump presses, and since the latest press.</summary>
        public float JumpWindow = 0.25f;
        /// <summary>How much a drive difference between legs turns the body.</summary>
        public float DesyncTurnFactor = 1f;
        /// <summary>Speed multiplier when only one leg is present.</summary>
        public float LimpSpeedFactor = 0.4f;
        /// <summary>Turn drift toward the missing leg, scaled by speed.</summary>
        public float LimpTurnBias = 0.3f;
    }

    /// <summary>Combines per-part inputs into one body intent. All comedy rules live here.</summary>
    public static class TrenchcoatIntentMixer
    {
        public static BodyIntent Mix(in PartInputs input, BodyPart present, float now, MixerSettings settings)
        {
            var intent = new BodyIntent();
            MixLegs(in input, present, now, settings, ref intent);
            MixArms(in input, present, ref intent);

            if ((present & BodyPart.Head) != 0)
            {
                intent.HeadYaw = Sanitize(input.Head.Yaw, 180f);
                intent.HeadPitch = Sanitize(input.Head.Pitch, 90f);
            }
            else
            {
                intent.HeadSlumped = true;
            }
            return intent;
        }

        static void MixLegs(in PartInputs input, BodyPart present, float now, MixerSettings s, ref BodyIntent intent)
        {
            bool hasLeft = (present & BodyPart.LegLeft) != 0;
            bool hasRight = (present & BodyPart.LegRight) != 0;

            if (hasLeft && hasRight)
            {
                float driveL = Unit(input.LegLeft.Drive);
                float driveR = Unit(input.LegRight.Drive);
                float steer = (Unit(input.LegLeft.Steer) + Unit(input.LegRight.Steer)) * 0.5f;
                intent.Forward = (driveL + driveR) * 0.5f;
                intent.Turn = Mathf.Clamp(steer + (driveL - driveR) * s.DesyncTurnFactor, -1f, 1f);
                intent.Jump = IsCoordinatedJump(input.LegLeft.JumpPressedAt, input.LegRight.JumpPressedAt, now, s.JumpWindow);
                intent.Crouch = input.LegLeft.Crouch || input.LegRight.Crouch;
            }
            else if (hasLeft || hasRight)
            {
                var leg = hasLeft ? input.LegLeft : input.LegRight;
                intent.Forward = Unit(leg.Drive) * s.LimpSpeedFactor;
                float towardMissing = hasLeft ? 1f : -1f; // missing right leg drags right
                float drift = towardMissing * s.LimpTurnBias * Mathf.Abs(intent.Forward);
                intent.Turn = Mathf.Clamp(Unit(leg.Steer) + drift, -1f, 1f);
                intent.Crouch = leg.Crouch;
            }
            else
            {
                intent.Collapsed = true;
            }
        }

        static void MixArms(in PartInputs input, BodyPart present, ref BodyIntent intent)
        {
            if ((present & BodyPart.ArmLeft) != 0)
            {
                intent.LeftHandTarget = input.ArmLeft.HandTarget;
                intent.LeftGrab = input.ArmLeft.Grab;
            }
            else
            {
                intent.LeftArmLimp = true;
            }

            if ((present & BodyPart.ArmRight) != 0)
            {
                intent.RightHandTarget = input.ArmRight.HandTarget;
                intent.RightGrab = input.ArmRight.Grab;
            }
            else
            {
                intent.RightArmLimp = true;
            }
        }

        static bool IsCoordinatedJump(float left, float right, float now, float window)
        {
            if (float.IsInfinity(left) || float.IsInfinity(right) || float.IsNaN(left) || float.IsNaN(right))
                return false;
            float latest = Mathf.Max(left, right);
            return Mathf.Abs(left - right) <= window && now >= latest && now - latest <= window;
        }

        static float Unit(float value) => Sanitize(value, 1f);

        static float Sanitize(float value, float limit) =>
            float.IsNaN(value) ? 0f : Mathf.Clamp(value, -limit, limit);
    }
}
