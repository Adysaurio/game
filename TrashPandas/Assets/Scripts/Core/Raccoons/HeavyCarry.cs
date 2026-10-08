using System.Collections.Generic;
using UnityEngine;

namespace TrashPandas.Core.Raccoons
{
    /// <summary>Big things (the cake, the giant gift) need several raccoons pulling together.</summary>
    public static class HeavyCarry
    {
        public const int Needed = 2;
        public const float MaxSpread = 1.6f;

        public static bool Lifted(int carriers, int needed = Needed) => carriers >= needed;

        public static Vector3 Anchor(IReadOnlyList<Vector3> mouths)
        {
            if (mouths.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            foreach (var m in mouths) sum += m;
            return sum / mouths.Count;
        }

        /// <summary>Carriers pulled too far apart: it slips and falls.</summary>
        public static bool ShouldDrop(IReadOnlyList<Vector3> mouths, float maxSpread = MaxSpread)
        {
            for (int i = 0; i < mouths.Count; i++)
                for (int j = i + 1; j < mouths.Count; j++)
                    if (Vector3.Distance(mouths[i], mouths[j]) > maxSpread) return true;
            return false;
        }
    }
}
