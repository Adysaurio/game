using UnityEngine;

namespace TrashPandas.Core.Trenchcoat
{
    /// <summary>Raw input one player sends from their slot. Unused fields are ignored by the router.</summary>
    public struct SlotInput
    {
        public Vector2 Move;          // legs: x = sidestep (A/D), y = drive (W/S)
        public float Turn;            // legs: -1..1 turn rate (gamepad stick)
        public float YawDelta;        // legs: degrees to turn this frame (mouse)
        public bool Crouch;
        public float JumpPressedAt;   // NegativeInfinity = never
        public Vector3 HandTarget;    // arms, body-local
        public bool PrimaryGrab;      // left hand (or the only hand)
        public bool SecondaryGrab;    // right hand when the slot has both arms
        public Vector2 Look;          // head: x = yaw, y = pitch (degrees)

        public static SlotInput Idle => new SlotInput { JumpPressedAt = float.NegativeInfinity };
    }
}
