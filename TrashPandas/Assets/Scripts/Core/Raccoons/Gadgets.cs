using UnityEngine;

namespace TrashPandas.Core.Raccoons
{
    public enum Gadget : byte { Pebble, SmokeBomb }

    /// <summary>A raccoon's tools: pebbles to throw (the noise lures humans away) and smoke bombs (nobody sees in or out).</summary>
    public sealed class GadgetKit
    {
        public const int MaxPerKind = 5;
        readonly int[] _count = new int[2];

        public static GadgetKit Starting()
        {
            var k = new GadgetKit();
            k.Add(Gadget.Pebble, 3);
            k.Add(Gadget.SmokeBomb, 1);
            return k;
        }

        public int Count(Gadget g) => _count[(int)g];
        public void Add(Gadget g, int n) => _count[(int)g] = Mathf.Min(MaxPerKind, _count[(int)g] + n);

        public bool TryUse(Gadget g)
        {
            if (_count[(int)g] <= 0) return false;
            _count[(int)g]--;
            return true;
        }
    }

    /// <summary>A smoke bomb's cloud: whoever is inside can't be seen.</summary>
    public readonly struct SmokeCloud
    {
        public readonly Vector3 Center;
        public readonly float Radius, Until;
        public SmokeCloud(Vector3 center, float radius, float until) { Center = center; Radius = radius; Until = until; }
        public bool Hides(Vector3 p, float now) => now < Until && Vector3.Distance(new Vector3(p.x, Center.y, p.z), Center) <= Radius;
    }
}
