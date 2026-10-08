using System.Collections.Generic;
using TrashPandas.Core.Loot;

namespace TrashPandas.Core.Raccoons
{
    /// <summary>Loot dropped off at the den: safe, credited to whoever brought it.</summary>
    public sealed class DeliveryLedger
    {
        readonly HashSet<int> _items = new HashSet<int>();
        readonly Dictionary<int, int> _byPlayer = new Dictionary<int, int>();
        readonly HashSet<ObjectiveId> _objectives = new HashSet<ObjectiveId>();

        public int Total { get; private set; }

        public bool Deliver(int player, int itemId, int value, ObjectiveId? objective)
        {
            if (!_items.Add(itemId)) return false;
            _byPlayer[player] = Of(player) + value;
            Total += value;
            if (objective.HasValue) _objectives.Add(objective.Value);
            return true;
        }

        public int Of(int player) => _byPlayer.TryGetValue(player, out var v) ? v : 0;
        public bool IsDone(ObjectiveId id) => _objectives.Contains(id);

        public void Reset()
        {
            _items.Clear();
            _byPlayer.Clear();
            _objectives.Clear();
            Total = 0;
        }
    }
}
