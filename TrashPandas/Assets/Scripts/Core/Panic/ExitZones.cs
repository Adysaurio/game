using System.Collections.Generic;
using UnityEngine;

namespace TrashPandas.Core.Panic
{
    public static class ExitZones
    {
        /// <summary>Index of the exit whose ground circle contains the position, or -1.</summary>
        public static int Contains(IReadOnlyList<Vector3> exits, Vector3 position, float radius)
        {
            for (int i = 0; i < exits.Count; i++)
                if (new Vector2(position.x - exits[i].x, position.z - exits[i].z).magnitude <= radius) return i;
            return -1;
        }
    }
}
