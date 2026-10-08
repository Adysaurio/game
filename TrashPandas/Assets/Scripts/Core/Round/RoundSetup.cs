using System;
using System.Collections.Generic;
using TrashPandas.Core.Loot;
using UnityEngine;

namespace TrashPandas.Core.Round
{
    public struct ObjectiveSpots
    {
        public ObjectiveId Id;
        public Vector3[] Spots;
    }

    public struct PickedObjective
    {
        public ObjectiveId Id;
        public Vector3 Spot;
    }

    public struct ExitCandidate
    {
        public Vector3 Position;
        public int Zone;
    }

    /// <summary>What changes from one wedding to the next: objectives, open exits and where the loot lies (spec §16).</summary>
    public static class RoundSetup
    {
        public static List<PickedObjective> PickObjectives(IReadOnlyList<ObjectiveSpots> candidates, int count, System.Random random)
        {
            var order = Shuffled(candidates.Count, random);
            var result = new List<PickedObjective>();
            foreach (int i in order)
            {
                if (result.Count >= count) break;
                var c = candidates[i];
                if (c.Spots == null || c.Spots.Length == 0) continue;
                result.Add(new PickedObjective { Id = c.Id, Spot = c.Spots[random.Next(c.Spots.Length)] });
            }
            return result;
        }

        /// <summary>
        /// Indices of the open exits: from different zones and at least <paramref name="minDistance"/> from the
        /// center. If that's impossible, the zone rule is relaxed first, then the distance; never repeats.
        /// </summary>
        public static List<int> PickExits(IReadOnlyList<ExitCandidate> exits, Vector3 center, int count, float minDistance, System.Random random)
        {
            var order = Shuffled(exits.Count, random);
            var picked = new List<int>();
            var zones = new HashSet<int>();
            for (int pass = 0; pass < 3 && picked.Count < count; pass++)
                foreach (int i in order)
                {
                    if (picked.Count >= count) break;
                    if (picked.Contains(i)) continue;
                    bool far = Vector3.Distance(exits[i].Position, center) >= minDistance;
                    if (pass == 0 && (!far || zones.Contains(exits[i].Zone))) continue;
                    if (pass == 1 && !far) continue;
                    picked.Add(i);
                    zones.Add(exits[i].Zone);
                }
            return picked;
        }

        public static List<Vector3> PickLootSpots(IReadOnlyList<Vector3> spots, int count, System.Random random)
        {
            var result = new List<Vector3>();
            foreach (int i in Shuffled(spots.Count, random))
            {
                if (result.Count >= count) break;
                result.Add(spots[i]);
            }
            return result;
        }

        static int[] Shuffled(int n, System.Random random)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = i;
            for (int i = n - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (a[i], a[j]) = (a[j], a[i]);
            }
            return a;
        }
    }
}
