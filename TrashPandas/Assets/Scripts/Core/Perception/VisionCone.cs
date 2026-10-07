using UnityEngine;

namespace TrashPandas.Core.Perception
{
    /// <summary>Can a human see a point? Range and field of view are measured on the ground plane.</summary>
    public static class VisionCone
    {
        public static bool CanSee(Vector3 eye, Vector3 forward, Vector3 target, float range, float fovDegrees, bool occluded)
        {
            if (occluded) return false;
            Vector3 to = target - eye;
            Vector2 flatTo = new Vector2(to.x, to.z);
            if (flatTo.magnitude > range) return false;
            Vector2 flatForward = new Vector2(forward.x, forward.z);
            if (flatTo.sqrMagnitude < 1e-6f || flatForward.sqrMagnitude < 1e-6f) return true; // right at our feet
            return Vector2.Angle(flatForward, flatTo) <= fovDegrees * 0.5f;
        }
    }
}
