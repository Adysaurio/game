using System.Collections.Generic;
using UnityEngine;

namespace TrashPandas.Core.Panic
{
    public enum PursuitState : byte { Idle, FetchWeapon, Chase, Windup, Search, Winded, Stunned, Return }

    /// <summary>Who this human is when the panic starts (Pac-Man style: each one chases differently).</summary>
    public sealed class PursuitPersonality
    {
        public float SwingRange = 1.2f;
        public float SwingCooldown = 1.4f;
        /// <summary>The telegraph before a strike: time to dodge.</summary>
        public float Windup = 0.5f;
        /// <summary>How long they look around the last place they saw you before giving up.</summary>
        public float SearchSeconds = 5f;
        /// <summary>Seconds of continuous chasing before they stop to catch their breath.</summary>
        public float ChaseBeforeWinded = 9f;
        public float WindedSeconds = 2.2f;
    }

    public struct PursuitInput
    {
        public Vector3 Self;
        public bool HasWeapon;
        public Vector3? Weapon;
        /// <summary>The raccoon this human can see and is assigned to (null = sees nobody it may chase).</summary>
        public ChaseTarget? Visible;
    }

    public struct PursuitOutput
    {
        public PursuitState State;
        public Vector3 Destination;
        public bool Strike;
        public int TargetId;
        /// <summary>0..1 while winding up a strike (for the telegraph).</summary>
        public float Windup01;
    }

    /// <summary>
    /// A panicked human that has to see you to chase you. Lose it and it goes to where it last saw you, looks
    /// around for a while and gives up; long chases leave it winded; it winds up before every strike.
    /// (Stealth "evasion phase" + Pac-Man's breathers + a readable telegraph.)
    /// </summary>
    public sealed class PursuitMind
    {
        public readonly PursuitPersonality P;
        readonly Vector3 _home;
        Vector3? _lastKnown;
        int _target = -1;
        float _searchFor, _chaseFor, _windedFor, _stunnedFor, _windupFor = -1f, _cooldown;

        public PursuitMind(Vector3 home, PursuitPersonality personality = null)
        {
            _home = home;
            P = personality ?? new PursuitPersonality();
        }

        /// <summary>Someone screamed "over there!": go and look (unless already busy chasing).</summary>
        public void Hear(Vector3 at)
        {
            if (_target >= 0 && _searchFor == 0f && _lastKnown.HasValue) return; // already on someone
            _lastKnown = at;
            _searchFor = 0f;
        }

        /// <summary>Where caught raccoons wait: nobody camps it (a short look, then they leave).</summary>
        public Vector3? CageAt;
        public const float CageCampRadius = 5f, CageSearchSeconds = 1.5f;

        public void Stun(float seconds)
        {
            _stunnedFor = Mathf.Max(_stunnedFor, seconds);
            _windupFor = -1f;
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        public PursuitOutput Update(float dt, PursuitInput input)
        {
            _cooldown -= dt;
            if (_stunnedFor > 0f)
            {
                _stunnedFor -= dt;
                return new PursuitOutput { State = PursuitState.Stunned, Destination = input.Self, TargetId = _target };
            }
            if (!input.HasWeapon && input.Weapon.HasValue)
                return new PursuitOutput { State = PursuitState.FetchWeapon, Destination = input.Weapon.Value, TargetId = -1 };

            if (_windedFor > 0f)
            {
                _windedFor -= dt;
                if (input.Visible.HasValue) _lastKnown = input.Visible.Value.Position;
                return new PursuitOutput { State = PursuitState.Winded, Destination = input.Self, TargetId = _target };
            }

            // Mid-strike: finish the swing (it may miss if you dodged).
            if (_windupFor >= 0f)
            {
                _windupFor += dt;
                if (_windupFor < P.Windup)
                    return new PursuitOutput { State = PursuitState.Windup, Destination = input.Self, TargetId = _target, Windup01 = _windupFor / P.Windup };
                _windupFor = -1f;
                _cooldown = P.SwingCooldown;
                return new PursuitOutput { State = PursuitState.Chase, Destination = input.Self, Strike = true, TargetId = _target };
            }

            if (input.Visible.HasValue)
            {
                var t = input.Visible.Value;
                _target = t.Id;
                _lastKnown = t.Position;
                _searchFor = 0f;
                _chaseFor += dt;
                if (_chaseFor >= P.ChaseBeforeWinded)
                {
                    _chaseFor = 0f;
                    _windedFor = P.WindedSeconds;
                    return new PursuitOutput { State = PursuitState.Winded, Destination = input.Self, TargetId = _target };
                }
                if (Flat(input.Self, t.Position) <= P.SwingRange && _cooldown <= 0f)
                {
                    _windupFor = 0f;
                    return new PursuitOutput { State = PursuitState.Windup, Destination = input.Self, TargetId = _target };
                }
                return new PursuitOutput { State = PursuitState.Chase, Destination = t.Position, TargetId = _target };
            }

            // Lost you: go to where they last saw you and look around; give up after a while.
            _chaseFor = Mathf.Max(0f, _chaseFor - dt);
            if (_lastKnown.HasValue)
            {
                _searchFor += dt;
                float searchFor = CageAt.HasValue && Flat(_lastKnown.Value, CageAt.Value) < CageCampRadius ? Mathf.Min(P.SearchSeconds, CageSearchSeconds) : P.SearchSeconds;
                if (_searchFor < searchFor)
                    return new PursuitOutput { State = PursuitState.Search, Destination = _lastKnown.Value, TargetId = _target };
                _lastKnown = null;
                _target = -1;
            }
            return new PursuitOutput
            {
                State = Flat(input.Self, _home) < 0.8f ? PursuitState.Idle : PursuitState.Return,
                Destination = _home,
                TargetId = -1,
            };
        }
    }

    /// <summary>Spread the pressure: at most N humans chase the same raccoon; the nearest ones get it.</summary>
    public static class PursuitAssigner
    {
        public struct Chaser
        {
            public Vector3 Position;
            public List<ChaseTarget> Sees;
        }

        public static ChaseTarget?[] Assign(IReadOnlyList<Chaser> chasers, int maxPerTarget = 2)
        {
            var result = new ChaseTarget?[chasers.Count];
            var load = new Dictionary<int, int>();
            // Every (chaser, visible raccoon) pair, nearest first.
            var pairs = new List<(int chaser, ChaseTarget target, float d)>();
            for (int i = 0; i < chasers.Count; i++)
                if (chasers[i].Sees != null)
                    foreach (var t in chasers[i].Sees)
                        pairs.Add((i, t, new Vector2(chasers[i].Position.x - t.Position.x, chasers[i].Position.z - t.Position.z).magnitude));
            pairs.Sort((a, b) => a.d.CompareTo(b.d));
            foreach (var (chaser, target, _) in pairs)
            {
                if (result[chaser].HasValue) continue;
                load.TryGetValue(target.Id, out int n);
                if (n >= maxPerTarget) continue;
                result[chaser] = target;
                load[target.Id] = n + 1;
            }
            return result;
        }
    }
}
