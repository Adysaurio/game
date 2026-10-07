using System;
using TrashPandas.Core.Session;
using TrashPandas.Core.Trenchcoat;
using Unity.Netcode;

namespace TrashPandas.Runtime.Net
{
    /// <summary>Who sits in each trenchcoat slot, as the host tells every client (HUD, own role, camera).</summary>
    public struct SlotsSnapshot : INetworkSerializable, IEquatable<SlotsSnapshot>
    {
        byte _count;
        // Occupant client id + 1 per slot; 0 = empty (so the default value means "nobody", not client 0).
        ulong _s0, _s1, _s2, _s3, _s4;

        public int SlotCount => _count;

        public static SlotsSnapshot From(SessionRoster roster)
        {
            var snap = new SlotsSnapshot();
            var slots = roster.Slots;
            if (slots == null) return snap;
            snap._count = (byte)slots.SlotCount;
            for (int i = 0; i < slots.SlotCount; i++)
            {
                var player = slots.OccupantOf(i);
                ulong? client = player.HasValue ? roster.ClientOf(player.Value) : null;
                snap.Set(i, client.HasValue ? client.Value + 1 : 0);
            }
            return snap;
        }

        public ulong? OccupantClient(int slot)
        {
            ulong raw = Get(slot);
            return raw == 0 ? (ulong?)null : raw - 1;
        }

        public int? SlotOfClient(ulong clientId)
        {
            for (int i = 0; i < _count; i++)
                if (Get(i) == clientId + 1) return i;
            return null;
        }

        public BodyPart PartsOf(int slot) => SlotLayout.ForPlayerCount(_count)[slot];

        public BodyPart MissingParts
        {
            get
            {
                if (_count == 0) return BodyPart.All;
                var present = BodyPart.None;
                for (int i = 0; i < _count; i++) if (Get(i) != 0) present |= PartsOf(i);
                return BodyPart.All & ~present;
            }
        }

        ulong Get(int i) => i switch { 0 => _s0, 1 => _s1, 2 => _s2, 3 => _s3, 4 => _s4, _ => 0 };

        void Set(int i, ulong v)
        {
            switch (i) { case 0: _s0 = v; break; case 1: _s1 = v; break; case 2: _s2 = v; break; case 3: _s3 = v; break; case 4: _s4 = v; break; }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref _count);
            s.SerializeValue(ref _s0); s.SerializeValue(ref _s1); s.SerializeValue(ref _s2);
            s.SerializeValue(ref _s3); s.SerializeValue(ref _s4);
        }

        public bool Equals(SlotsSnapshot o) =>
            _count == o._count && _s0 == o._s0 && _s1 == o._s1 && _s2 == o._s2 && _s3 == o._s3 && _s4 == o._s4;
    }
}
