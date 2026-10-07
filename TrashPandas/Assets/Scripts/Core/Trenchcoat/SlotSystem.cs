using System;
using System.Collections.Generic;

namespace TrashPandas.Core.Trenchcoat
{
    /// <summary>Tracks which player occupies each trenchcoat slot and which body parts are missing.</summary>
    public sealed class SlotSystem
    {
        readonly IReadOnlyList<BodyPart> _slots;
        readonly int?[] _occupants;

        public event Action<BodyPart> MissingPartsChanged;

        public SlotSystem(int playerCount)
        {
            _slots = SlotLayout.ForPlayerCount(playerCount);
            _occupants = new int?[_slots.Count];
        }

        public int SlotCount => _slots.Count;

        public BodyPart PartsOf(int slotIndex) => _slots[slotIndex];

        public int? OccupantOf(int slotIndex) => _occupants[slotIndex];

        public int? SlotOf(int playerId)
        {
            for (int i = 0; i < _occupants.Length; i++)
                if (_occupants[i] == playerId) return i;
            return null;
        }

        public int? FirstFreeSlot()
        {
            for (int i = 0; i < _occupants.Length; i++)
                if (!_occupants[i].HasValue) return i;
            return null;
        }

        public bool TryEnter(int playerId, int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _occupants.Length) return false;
            if (_occupants[slotIndex].HasValue || SlotOf(playerId).HasValue) return false;

            var before = MissingParts;
            _occupants[slotIndex] = playerId;
            NotifyIfChanged(before);
            return true;
        }

        public bool Leave(int playerId)
        {
            var slot = SlotOf(playerId);
            if (!slot.HasValue) return false;

            var before = MissingParts;
            _occupants[slot.Value] = null;
            NotifyIfChanged(before);
            return true;
        }

        public BodyPart ControlledParts
        {
            get
            {
                var parts = BodyPart.None;
                for (int i = 0; i < _occupants.Length; i++)
                    if (_occupants[i].HasValue) parts |= _slots[i];
                return parts;
            }
        }

        public BodyPart MissingParts => BodyPart.All & ~ControlledParts;

        void NotifyIfChanged(BodyPart before)
        {
            var after = MissingParts;
            if (after != before) MissingPartsChanged?.Invoke(after);
        }
    }
}
