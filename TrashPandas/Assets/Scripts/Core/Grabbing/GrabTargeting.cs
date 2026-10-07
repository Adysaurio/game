using System.Collections.Generic;
using UnityEngine;

namespace TrashPandas.Core.Grabbing
{
    public struct GrabCandidate
    {
        public int Id;
        public Vector3 Position;
    }

    /// <summary>
    /// Aim assist for grabbing: picks the grabbable closest to the crosshair, within a cone around the aim
    /// ray and within arm's reach of the body.
    /// </summary>
    public static class GrabTargeting
    {
        public static int? Pick(Vector3 rayOrigin, Vector3 rayDirection, IReadOnlyList<GrabCandidate> candidates,
                                Vector3 reachCenter, float reach, float coneDegrees)
        {
            int? best = null;
            float bestAngle = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                Vector3 toCandidate = c.Position - rayOrigin;
                if (Vector3.Dot(toCandidate, rayDirection) <= 0f) continue;
                if (Vector3.Distance(c.Position, reachCenter) > reach) continue;
                float angle = Vector3.Angle(rayDirection, toCandidate);
                if (angle > coneDegrees || angle >= bestAngle) continue;
                bestAngle = angle;
                best = c.Id;
            }
            return best;
        }

        public static bool LeftHandCloser(Vector3 target, Vector3 leftShoulder, Vector3 rightShoulder) =>
            (target - leftShoulder).sqrMagnitude <= (target - rightShoulder).sqrMagnitude;
    }
}
