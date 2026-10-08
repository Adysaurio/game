using System.Collections.Generic;
using TrashPandas.Core.Loot;

namespace TrashPandas.Core.Panic
{
    /// <summary>
    /// Who takes home what: each raccoon's share of the pocket plus whatever it carries in its mouth — paid only
    /// if it escapes (spec §16.2).
    /// </summary>
    public sealed class RoundPayout
    {
        struct Carried { public int Value; public ObjectiveId? Objective; }

        /// <summary>Concept v2: shares are loot already delivered to the den — kept even if caught.</summary>
        public bool SharesAreSafe;

        readonly Dictionary<int, int> _shares = new Dictionary<int, int>();
        readonly Dictionary<int, Carried> _mouth = new Dictionary<int, Carried>();
        readonly Dictionary<int, int> _paid = new Dictionary<int, int>();
        readonly HashSet<ObjectiveId> _objectives = new HashSet<ObjectiveId>();

        public void SetShares(Dictionary<int, int> shares)
        {
            _shares.Clear();
            foreach (var pair in shares) _shares[pair.Key] = pair.Value;
        }

        /// <summary>Loot delivered after the shares were set (during the RUN): safe like the rest.</summary>
        public void AddShare(int player, int value)
        {
            _shares[player] = (_shares.TryGetValue(player, out var s) ? s : 0) + value;
            if (_paid.ContainsKey(player)) _paid[player] += value;
        }

        public void Carry(int player, int value, ObjectiveId? objective) => _mouth[player] = new Carried { Value = value, Objective = objective };

        /// <returns>True if the player was carrying something.</returns>
        public bool Drop(int player) => _mouth.Remove(player);

        public void Escaped(int player)
        {
            if (_paid.ContainsKey(player)) return;
            int total = _shares.TryGetValue(player, out var share) ? share : 0;
            if (_mouth.TryGetValue(player, out var c))
            {
                total += c.Value;
                if (c.Objective.HasValue) _objectives.Add(c.Objective.Value);
                _mouth.Remove(player);
            }
            _paid[player] = total;
        }

        /// <returns>True if the player dropped something from its mouth.</returns>
        public bool Caught(int player)
        {
            if (!_paid.ContainsKey(player)) _paid[player] = SharesAreSafe && _shares.TryGetValue(player, out var safe) ? safe : 0;
            return Drop(player);
        }

        public int Of(int player) => _paid.TryGetValue(player, out var v) ? v : 0;
        public bool ObjectiveEscaped(ObjectiveId id) => _objectives.Contains(id);

        public int Total
        {
            get { int t = 0; foreach (var v in _paid.Values) t += v; return t; }
        }
    }

    public static class CleanExit
    {
        /// <summary>The coat walks out through the arch with nobody missing and something in the pocket (spec §16.2).</summary>
        public static bool Qualifies(bool infiltrating, bool coatInArch, int missingParts, int pocketTotal) =>
            infiltrating && coatInArch && missingParts == 0 && pocketTotal > 0;
    }
}

namespace TrashPandas.Core.Panic
{
    public static class MouthRules
    {
        /// <summary>Only a raccoon that's still in play may pick things up (a caught one keeps nothing).</summary>
        public static bool MayPickUp(bool infiltrating, PlayerOutcome status) => infiltrating || status == PlayerOutcome.Running;
    }
}
