using UnityEngine;

namespace TrashPandas.Core.Raccoons
{
    public enum Gadget : byte { Pebble, SmokeBomb, Banana }

    /// <summary>A raccoon's tools: pebbles to throw (the noise lures humans away) and smoke bombs (nobody sees in or out).</summary>
    public sealed class GadgetKit
    {
        public const int MaxPerKind = 5;
        readonly int[] _count = new int[3];

        public static GadgetKit Starting()
        {
            var k = new GadgetKit();
            k.Add(Gadget.Pebble, 3);
            k.Add(Gadget.SmokeBomb, 1);
            k.Add(Gadget.Banana, 2);
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

    /// <summary>How a tool flies: where you look sets it (look up = farther). Pebbles fly, bananas and smoke get tossed.</summary>
    public static class GadgetThrow
    {
        public static float Speed(Gadget g) => g == Gadget.Pebble ? 12f : 8f;

        public static Vector3 Velocity(Gadget g, Vector3 look)
        {
            float speed = Speed(g);
            var flat = new Vector3(look.x, 0f, look.z);
            flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
            float up = Mathf.Clamp(look.y + 0.45f, 0.1f, 1.2f) * speed;
            return flat * speed + Vector3.up * up;
        }

        /// <summary>Distance covered before falling back to launch height (for tests / HUD).</summary>
        public static float FlatRange(Vector3 v) => new Vector2(v.x, v.z).magnitude * 2f * v.y / 9.81f;
    }

    /// <summary>A banana peel on the ground: whoever walks onto it goes flying (humans and raccoons alike).</summary>
    public static class BananaPeel
    {
        public const float Radius = 0.5f, SlipSeconds = 2.5f;
        public static bool Slips(Vector3 peel, Vector3 walker, bool moving) =>
            moving && new Vector2(peel.x - walker.x, peel.z - walker.z).magnitude <= Radius;
    }
}
