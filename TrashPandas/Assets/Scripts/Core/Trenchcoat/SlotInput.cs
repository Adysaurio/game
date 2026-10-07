using UnityEngine;

namespace TrashPandas.Core.Trenchcoat
{
    /// <summary>What one player sends from their slot. Directions are world-space, already camera-relative.</summary>
    public struct SlotInput
    {
        public Vector2 Move;          // legs: world XZ direction, magnitude 0..1
        public bool Crouch;
        public float JumpPressedAt;   // NegativeInfinity = never
        public Vector3 Aim;           // head (and arms without a point): world-space direction the camera looks
        public Vector3 AimPoint;      // arms: world point under the crosshair (or the assisted grab target)
        public bool HasAimPoint;
        public bool GrabOne;          // arms: reach/grab with one hand (the closer one when the slot has both)
        public bool GrabBoth;         // arms: reach/grab with both hands
        public bool PreferLeftHand;   // which hand GrabOne uses when the slot has both arms

        public static SlotInput Idle => new SlotInput { JumpPressedAt = float.NegativeInfinity };
    }
}
