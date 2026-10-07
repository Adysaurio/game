using System.Collections.Generic;

namespace TrashPandas.Core.Panic
{
    public enum HitResult : byte { Ignored, Stunned, Caught }

    /// <summary>Panic-phase hits per player: each hit stuns, a short grace stops double hits, N hits = caught.</summary>
    public sealed class HitTracker
    {
        readonly int _hitsToCatch;
        readonly float _stun;
        readonly float _invulnerable;
        readonly Dictionary<int, (int hits, float lastHit)> _state = new Dictionary<int, (int, float)>();

        public HitTracker(int hitsToCatch = 3, float stunSeconds = 1f, float invulnerableSeconds = 0.8f)
        {
            _hitsToCatch = hitsToCatch;
            _stun = stunSeconds;
            _invulnerable = invulnerableSeconds;
        }

        public int HitsToCatch => _hitsToCatch;

        public HitResult TryHit(int player, float now)
        {
            _state.TryGetValue(player, out var s);
            if (s.hits >= _hitsToCatch) return HitResult.Ignored;
            if (s.hits > 0 && now - s.lastHit < _invulnerable) return HitResult.Ignored;
            s = (s.hits + 1, now);
            _state[player] = s;
            return s.hits >= _hitsToCatch ? HitResult.Caught : HitResult.Stunned;
        }

        public int Hits(int player) => _state.TryGetValue(player, out var s) ? s.hits : 0;

        public bool IsStunned(int player, float now) =>
            _state.TryGetValue(player, out var s) && s.hits > 0 && now - s.lastHit < _stun;

        public void Reset() => _state.Clear();
    }
}
