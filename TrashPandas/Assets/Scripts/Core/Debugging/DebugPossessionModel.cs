using TrashPandas.Core.Trenchcoat;

namespace TrashPandas.Core.Debugging
{
    /// <summary>
    /// Debug mode: one person drives every slot. Each slot starts with virtual player i;
    /// the person possesses one player at a time.
    /// </summary>
    public sealed class DebugPossessionModel
    {
        readonly SlotSystem _slots;

        public DebugPossessionModel(SlotSystem slots)
        {
            _slots = slots;
            for (int i = 0; i < slots.SlotCount; i++)
                slots.TryEnter(i, i);
            ActivePlayerId = 0;
        }

        public int ActivePlayerId { get; private set; }

        public bool ActiveIsOutside => !_slots.SlotOf(ActivePlayerId).HasValue;

        public bool CycleNext()
        {
            if (ActiveIsOutside) return false;
            int count = _slots.SlotCount;
            for (int step = 1; step < count; step++)
            {
                int candidate = (ActivePlayerId + step) % count;
                if (_slots.SlotOf(candidate).HasValue)
                {
                    ActivePlayerId = candidate;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Jump straight to a player that is inside the coat (number keys).</summary>
        public bool TrySelect(int playerId)
        {
            if (ActiveIsOutside || playerId < 0 || playerId >= _slots.SlotCount) return false;
            if (!_slots.SlotOf(playerId).HasValue) return false;
            ActivePlayerId = playerId;
            return true;
        }

        public bool LeaveCoat() => _slots.Leave(ActivePlayerId);

        public bool ReturnToCoat()
        {
            if (!ActiveIsOutside) return false;
            var free = _slots.FirstFreeSlot();
            return free.HasValue && _slots.TryEnter(ActivePlayerId, free.Value);
        }
    }
}
