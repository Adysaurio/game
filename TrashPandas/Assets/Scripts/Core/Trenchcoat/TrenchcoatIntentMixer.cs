using UnityEngine;

namespace TrashPandas.Core.Trenchcoat
{
    public sealed class MixerSettings
    {
        /// <summary>Max seconds between both legs' jump presses, and since the latest press.</summary>
        public float JumpWindow = 0.25f;
        /// <summary>Speed multiplier when only one leg is present.</summary>
        public float LimpSpeedFactor = 0.4f;
        /// <summary>Degrees the walk direction is dragged toward the missing leg.</summary>
        public float LimpDriftDegrees = 20f;
        /// <summary>Below this input length a leg counts as idle (no discord).</summary>
        public float IdleThreshold = 0.1f;
    }

    /// <summary>Combines per-part inputs into one body intent. All comedy rules live here.</summary>
    public static class TrenchcoatIntentMixer
    {
        public static BodyIntent Mix(in PartInputs input, BodyPart present, float now, MixerSettings settings)
        {
            var intent = new BodyIntent();
            MixLegs(in input, present, now, settings, ref intent);

            if ((present & BodyPart.ArmLeft) != 0)
            {
                intent.LeftAim = Direction(input.ArmLeft.Aim);
                intent.LeftReach = input.ArmLeft.Reach;
            }
            else intent.LeftArmLimp = true;

            if ((present & BodyPart.ArmRight) != 0)
            {
                intent.RightAim = Direction(input.ArmRight.Aim);
                intent.RightReach = input.ArmRight.Reach;
            }
            else intent.RightArmLimp = true;

            if ((present & BodyPart.Head) != 0) intent.HeadAim = Direction(input.Head.Aim);
            else intent.HeadSlumped = true;

            return intent;
        }

        static void MixLegs(in PartInputs input, BodyPart present, float now, MixerSettings s, ref BodyIntent intent)
        {
            bool hasLeft = (present & BodyPart.LegLeft) != 0;
            bool hasRight = (present & BodyPart.LegRight) != 0;
            intent.LeftLegLimp = !hasLeft;
            intent.RightLegLimp = !hasRight;

            if (hasLeft && hasRight)
            {
                Vector2 left = Planar(input.LegLeft.Move), right = Planar(input.LegRight.Move);
                intent.Move = (left + right) * 0.5f;
                if (left.magnitude > s.IdleThreshold && right.magnitude > s.IdleThreshold)
                    intent.Discord = (1f - Vector2.Dot(left.normalized, right.normalized)) * 0.5f;
                intent.Jump = IsCoordinatedJump(input.LegLeft.JumpPressedAt, input.LegRight.JumpPressedAt, now, s.JumpWindow);
                intent.Crouch = input.LegLeft.Crouch || input.LegRight.Crouch;
            }
            else if (hasLeft || hasRight)
            {
                var leg = hasLeft ? input.LegLeft : input.LegRight;
                float drift = (hasLeft ? 1f : -1f) * s.LimpDriftDegrees; // missing right leg drags right (clockwise)
                intent.Move = RotateClockwise(Planar(leg.Move) * s.LimpSpeedFactor, drift);
                intent.Crouch = leg.Crouch;
            }
            else
            {
                intent.Collapsed = true;
            }
        }

        static bool IsCoordinatedJump(float left, float right, float now, float window)
        {
            if (float.IsInfinity(left) || float.IsInfinity(right) || float.IsNaN(left) || float.IsNaN(right))
                return false;
            float latest = Mathf.Max(left, right);
            return Mathf.Abs(left - right) <= window && now >= latest && now - latest <= window;
        }

        /// <summary>Rotates an XZ vector clockwise as seen from above (positive = toward +X when facing +Z).</summary>
        static Vector2 RotateClockwise(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c + v.y * s, -v.x * s + v.y * c);
        }

        static Vector2 Planar(Vector2 v) =>
            float.IsNaN(v.x) || float.IsNaN(v.y) ? Vector2.zero : Vector2.ClampMagnitude(v, 1f);

        static Vector3 Direction(Vector3 v) =>
            float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || v.sqrMagnitude < 1e-6f ? Vector3.zero : v.normalized;
    }
}
