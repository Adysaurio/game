using System.Collections.Generic;
using UnityEngine;

namespace TrashPandas.Core.Panic
{
    /// <summary>Tonight's problem: one antagonist per round, picked at random (never the same twice in a row).</summary>
    public enum NemesisKind : byte { WeddingPlanner, PestControl, Granny }

    /// <summary>Who they are and their one readable rule (Lethal Company: every threat teaches its rule).</summary>
    public sealed class NemesisProfile
    {
        public string Title, Rule;
        /// <summary>× the base chaser speed.</summary>
        public float Speed;
        public float SightRange, Fov;
        /// <summary>× noise radius.</summary>
        public float Hearing;
        /// <summary>Hears what any witness sees, anywhere (otherwise only within <see cref="ReportRange"/>).</summary>
        public bool Radio;
        public float ReportRange = 16f;
        /// <summary>Sees raccoons inside bushes/cans closer than this (0 = never).</summary>
        public float SeesIntoHideoutsWithin;
        /// <summary>Throws the strike from afar instead of swinging (the slipper).</summary>
        public bool Throws;
        public PursuitPersonality Pursuit;

        public static NemesisProfile Of(NemesisKind kind)
        {
            switch (kind)
            {
                case NemesisKind.PestControl:
                    return new NemesisProfile
                    {
                        Title = "THE PEST CONTROL GUY", Rule = "Never gets tired. His flashlight sees into bushes up close.",
                        Speed = 0.85f, SightRange = 18f, Fov = 70f, Hearing = 1f, SeesIntoHideoutsWithin = 3.5f,
                        Pursuit = new PursuitPersonality { ChaseBeforeWinded = 999f, SearchSeconds = 10f, SwingRange = 1.5f, Windup = 0.55f },
                    };
                case NemesisKind.Granny:
                    return new NemesisProfile
                    {
                        Title = "GRANNY", Rule = "Slow, but she hears everything. Watch out for the flying slipper.",
                        Speed = 0.75f, SightRange = 12f, Fov = 160f, Hearing = 2f, Throws = true,
                        Pursuit = new PursuitPersonality { SwingRange = 5f, Windup = 0.9f, SwingCooldown = 2.2f, SearchSeconds = 7f, ChaseBeforeWinded = 8f, WindedSeconds = 2.5f },
                    };
                default:
                    return new NemesisProfile
                    {
                        Title = "THE WEDDING PLANNER", Rule = "Fast, and on the radio: whatever any guest sees, she knows.",
                        Speed = 1.1f, SightRange = 14f, Fov = 150f, Hearing = 1f, Radio = true,
                        Pursuit = new PursuitPersonality { ChaseBeforeWinded = 10f, WindedSeconds = 2f, SearchSeconds = 6f, Windup = 0.5f },
                    };
            }
        }
    }

    public static class NemesisPick
    {
        public static NemesisKind Roll(int seed, NemesisKind? last)
        {
            var rng = new System.Random(seed);
            int n = System.Enum.GetValues(typeof(NemesisKind)).Length;
            if (!last.HasValue) return (NemesisKind)rng.Next(n);
            int k = rng.Next(n - 1);
            if (k >= (int)last.Value) k++;
            return (NemesisKind)k;
        }
    }

    /// <summary>The nemesis learns your tricks (Alien: Isolation): the same hideout twice, pebbles, pipes.</summary>
    public sealed class NemesisMemory
    {
        public const int SuspectAfter = 2, PebblesThatWork = 2;
        readonly Dictionary<int, int> _hideUses = new Dictionary<int, int>();
        int _pebbles;

        public void HidIn(int hideoutId) => _hideUses[hideoutId] = (_hideUses.TryGetValue(hideoutId, out int n) ? n : 0) + 1;
        public bool Suspects(int hideoutId) => _hideUses.TryGetValue(hideoutId, out int n) && n >= SuspectAfter;

        /// <summary>A pebble clacked somewhere: true if she still goes to look.</summary>
        public bool FallsForPebble() => ++_pebbles <= PebblesThatWork;

        public Vector3? Ambush { get; private set; }
        public void SawEnterPipe(Vector3 otherEnd) => Ambush = otherEnd;
        public void AmbushDone() => Ambush = null;
    }
}
