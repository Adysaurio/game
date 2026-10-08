using System.Collections.Generic;

namespace TrashPandas.Core.Loot
{
    /// <summary>The trenchcoat's shared pocket: everything stashed this round.</summary>
    public sealed class LootPocket
    {
        public const float CleanExitMultiplier = 1.5f;

        readonly HashSet<int> _items = new HashSet<int>();
        readonly HashSet<ObjectiveId> _objectives = new HashSet<ObjectiveId>();

        public int Total { get; private set; }

        /// <returns>False if that item was already in the pocket.</returns>
        public bool Add(int itemId, int value, ObjectiveId? objective)
        {
            if (!_items.Add(itemId)) return false;
            Total += value;
            if (objective.HasValue) _objectives.Add(objective.Value);
            return true;
        }

        public bool IsDone(ObjectiveId id) => _objectives.Contains(id);

        /// <summary>Equal shares; the remainder goes to the first players listed.</summary>
        public Dictionary<int, int> Split(IReadOnlyList<int> players) => Share(Total, players);

        public Dictionary<int, int> CleanExitPayout(IReadOnlyList<int> players) =>
            Share((int)System.Math.Round(Total * CleanExitMultiplier), players);

        public void Reset()
        {
            _items.Clear();
            _objectives.Clear();
            Total = 0;
        }

        static Dictionary<int, int> Share(int amount, IReadOnlyList<int> players)
        {
            var result = new Dictionary<int, int>();
            if (players.Count == 0) return result;
            int each = amount / players.Count, extra = amount % players.Count;
            for (int i = 0; i < players.Count; i++) result[players[i]] = each + (i < extra ? 1 : 0);
            return result;
        }
    }
}
