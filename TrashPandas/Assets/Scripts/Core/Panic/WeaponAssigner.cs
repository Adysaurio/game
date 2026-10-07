using System.Collections.Generic;
using UnityEngine;

namespace TrashPandas.Core.Panic
{
    /// <summary>Gives each human the nearest weapon nobody else is taking (closest pairs first).</summary>
    public static class WeaponAssigner
    {
        public static int[] Assign(IReadOnlyList<Vector3> humans, IReadOnlyList<Vector3> weapons)
        {
            var result = new int[humans.Count];
            for (int i = 0; i < result.Length; i++) result[i] = -1;
            var pairs = new List<(float d, int h, int w)>();
            for (int h = 0; h < humans.Count; h++)
                for (int w = 0; w < weapons.Count; w++)
                    pairs.Add((Vector3.Distance(humans[h], weapons[w]), h, w));
            pairs.Sort((a, b) => a.d.CompareTo(b.d));
            var taken = new HashSet<int>();
            foreach (var (_, h, w) in pairs)
            {
                if (result[h] >= 0 || taken.Contains(w)) continue;
                result[h] = w;
                taken.Add(w);
            }
            return result;
        }
    }
}
