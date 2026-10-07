using UnityEngine;

namespace TrashPandas.Core.Trenchcoat
{
    /// <summary>What one player sends from their slot. Directions are world-space, already camera-relative.</summary>
    public struct SlotInput
    {
        public Vector2 Move;          // legs: world XZ direction, magnitude 0..1
        public bool Crouch;
        public float JumpPressedAt;   // NegativeInfinity = never
        public Vector3 Aim;           // arms and head: world-space direction the player's camera looks
        public bool PrimaryReach;     // left hand (or the only hand)
        public bool SecondaryReach;   // right hand when the slot has both arms

        public static SlotInput Idle => new SlotInput { JumpPressedAt = float.NegativeInfinity };
    }
}
