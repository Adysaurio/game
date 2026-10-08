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

        /// <summary>The candidate the player is aiming at (closest to the aim point within <paramref name="lockRadius"/>), if any.</summary>
        public static int? LockedTarget(Vector3 aimPoint, IReadOnlyList<GrabCandidate> candidates, float lockRadius)
        {
            int? best = null;
            float bestD = lockRadius;
            for (int i = 0; i < candidates.Count; i++)
            {
                float d = Vector3.Distance(candidates[i].Position, aimPoint);
                if (d <= bestD) { bestD = d; best = candidates[i].Id; }
            }
            return best;
        }

        /// <summary>With a locked target the hand ignores whatever it brushes past on the way.</summary>
        public static bool MayGrab(int candidateId, int? locked) => !locked.HasValue || locked.Value == candidateId;

        public static bool LeftHandCloser(Vector3 target, Vector3 leftShoulder, Vector3 rightShoulder) =>
            (target - leftShoulder).sqrMagnitude <= (target - rightShoulder).sqrMagnitude;
    }
}
