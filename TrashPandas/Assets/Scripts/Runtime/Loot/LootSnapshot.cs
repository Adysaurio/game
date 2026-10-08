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

        public bool IsPicked(Core.Loot.ObjectiveId id) => (ObjectivesPicked & (1 << (int)id)) != 0;
        public bool IsDone(Core.Loot.ObjectiveId id) => (ObjectivesDone & (1 << (int)id)) != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Total);
            s.SerializeValue(ref ObjectivesPicked); s.SerializeValue(ref ObjectivesDone);
            s.SerializeValue(ref SecondsLeft);
            s.SerializeValue(ref StashSerial); s.SerializeValue(ref LastStashValue); s.SerializeValue(ref LastStashAt);
        }

        public bool Equals(LootSnapshot o) =>
            Total == o.Total && ObjectivesPicked == o.ObjectivesPicked && ObjectivesDone == o.ObjectivesDone &&
            Mathf.Approximately(SecondsLeft, o.SecondsLeft) && StashSerial == o.StashSerial &&
            LastStashValue == o.LastStashValue && LastStashAt == o.LastStashAt;
    }
}
