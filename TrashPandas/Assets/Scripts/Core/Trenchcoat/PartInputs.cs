using UnityEngine;

namespace TrashPandas.Core.Trenchcoat
{
    public struct LegInput
    {
        /// <summary>-1..1, forward push of this leg.</summary>
        public float Drive;
        /// <summary>-1..1 turn rate (mouse), positive = right.</summary>
        public float Steer;
        /// <summary>-1..1 sidestep (A/D), positive = right.</summary>
        public float Strafe;
        /// <summary>Time (s) of the last jump press; NegativeInfinity = never.</summary>
        public float JumpPressedAt;
        public bool Crouch;

        public static LegInput Idle => new LegInput { JumpPressedAt = float.NegativeInfinity };
    }

    public struct ArmInput
    {
        /// <summary>Hand target in body-local space.</summary>
        public Vector3 HandTarget;
        public bool Grab;
    }

    public struct HeadInput
    {
        public float Yaw;
        public float Pitch;
    }

    public struct PartInputs
    {
        public LegInput LegLeft;
        public LegInput LegRight;
        public ArmInput ArmLeft;
        public ArmInput ArmRight;
        public HeadInput Head;

        public static PartInputs Idle => new PartInputs { LegLeft = LegInput.Idle, LegRight = LegInput.Idle };
    }
}
