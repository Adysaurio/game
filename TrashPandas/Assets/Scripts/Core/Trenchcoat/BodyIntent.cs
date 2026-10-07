using UnityEngine;

namespace TrashPandas.Core.Trenchcoat
{
    /// <summary>What the trenchcoat body should do this frame, after mixing every slot's input.</summary>
    public struct BodyIntent
    {
        public float Forward;     // -1..1
        public float Turn;        // -1..1, positive = right
        public float Strafe;      // -1..1 sidestep, positive = right
        public bool Jump;
        public bool Crouch;
        public bool Collapsed;    // no legs: the body slumps down
        public bool LeftLegLimp;
        public bool RightLegLimp;

        public bool LeftArmLimp;
        public bool RightArmLimp;
        public Vector3 LeftHandTarget;   // body-local
        public Vector3 RightHandTarget;  // body-local
        public bool LeftGrab;
        public bool RightGrab;

        public bool HeadSlumped;
        public float HeadYaw;
        public float HeadPitch;
    }
}
