using UnityEngine;

namespace TrashPandas.Core.Trenchcoat
{
    /// <summary>What the trenchcoat body should do this frame, after mixing every slot's input.</summary>
    public struct BodyIntent
    {
        public Vector2 Move;      // world XZ, magnitude 0..1
        public float Discord;     // 0 = legs agree, 1 = legs push in opposite directions
        public bool Jump;
        public bool Crouch;
        public bool Collapsed;    // no legs: the body sits down
        public bool LeftLegLimp;
        public bool RightLegLimp;

        public bool LeftArmLimp;
        public bool RightArmLimp;
        public bool LeftReach;
        public bool RightReach;
        public Vector3 LeftAim;   // world-space, normalized or zero
        public Vector3 RightAim;

        public bool HeadSlumped;
        public Vector3 HeadAim;   // world-space, normalized or zero
    }
}
