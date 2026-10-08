using System;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.Runtime.Loot
{
    /// <summary>What everyone sees of the loot: the pocket, this round's objectives, the clock and the last "+$".</summary>
    public struct LootSnapshot : INetworkSerializable, IEquatable<LootSnapshot>
    {
        public int Total;
        public byte ObjectivesPicked, ObjectivesDone; // bit per ObjectiveId
        public float SecondsLeft;
        public byte StashSerial;
        public short LastStashValue;
        public Vector3 LastStashAt;
        byte _m0, _m1, _m2, _m3, _m4; // item index + 1 in each player's mouth (0 = nothing)

        public int MouthItemOf(int player) => (player switch { 0 => _m0, 1 => _m1, 2 => _m2, 3 => _m3, 4 => _m4, _ => 0 }) - 1;

        public void SetMouth(int player, int itemIndex)
        {
            byte v = (byte)(itemIndex + 1);
            switch (player) { case 0: _m0 = v; break; case 1: _m1 = v; break; case 2: _m2 = v; break; case 3: _m3 = v; break; case 4: _m4 = v; break; }
        }

        public bool IsPicked(Core.Loot.ObjectiveId id) => (ObjectivesPicked & (1 << (int)id)) != 0;
        public bool IsDone(Core.Loot.ObjectiveId id) => (ObjectivesDone & (1 << (int)id)) != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Total);
            s.SerializeValue(ref ObjectivesPicked); s.SerializeValue(ref ObjectivesDone);
            s.SerializeValue(ref SecondsLeft);
            s.SerializeValue(ref StashSerial); s.SerializeValue(ref LastStashValue); s.SerializeValue(ref LastStashAt);
            s.SerializeValue(ref _m0); s.SerializeValue(ref _m1); s.SerializeValue(ref _m2); s.SerializeValue(ref _m3); s.SerializeValue(ref _m4);
        }

        public bool Equals(LootSnapshot o) =>
            Total == o.Total && ObjectivesPicked == o.ObjectivesPicked && ObjectivesDone == o.ObjectivesDone &&
            Mathf.Approximately(SecondsLeft, o.SecondsLeft) && StashSerial == o.StashSerial &&
            LastStashValue == o.LastStashValue && LastStashAt == o.LastStashAt &&
            _m0 == o._m0 && _m1 == o._m1 && _m2 == o._m2 && _m3 == o._m3 && _m4 == o._m4;
    }
}
