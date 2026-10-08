using System.Collections.Generic;
using TrashPandas.Core.Grabbing;
using UnityEngine;

namespace TrashPandas.Core.Raccoons
{
    /// <summary>Which thing lights up: the one closest to the center of the view, within reach of the mouth.</summary>
    public static class GrabPick
    {
        public const float Reach = 1.2f, ConeDegrees = 35f;

        public static int? Pick(Vector3 eye, Vector3 forward, Vector3 mouth, IReadOnlyList<GrabCandidate> candidates,
                                float reach = Reach, float coneDegrees = ConeDegrees)
        {
            int? best = null;
            float bestAngle = coneDegrees;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (Vector3.Distance(c.Position, mouth) > reach) continue;
                float angle = Vector3.Angle(forward, c.Position - eye);
                if (angle <= bestAngle) { bestAngle = angle; best = c.Id; }
            }
            return best;
        }
    }
}
