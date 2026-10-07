using UnityEngine;

namespace TrashPandas.Core.Trenchcoat
{
    public struct LegInput
    {
        /// <summary>World XZ direction this leg pushes toward, magnitude 0..1.</summary>
        public Vector2 Move;
        /// <summary>Time (s) of the last jump press; NegativeInfinity = never.</summary>
        public float JumpPressedAt;
        public bool Crouch;

        public static LegInput Idle => new LegInput { JumpPressedAt = float.NegativeInfinity };
    }

    public struct ArmInput
    {
        /// <summary>World-space direction to reach toward.</summary>
        public Vector3 Aim;
        public bool Reach;
    }

    public struct HeadInput
    {
        /// <summary>World-space direction to look toward.</summary>
        public Vector3 Aim;
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
