using System.Collections.Generic;

namespace TrashPandas.Core.Session
{
    /// <summary>Drops duplicated or out-of-order inputs sent over an unreliable channel.</summary>
    public sealed class InputSequencer
    {
        readonly Dictionary<int, uint> _last = new Dictionary<int, uint>();

        public bool Accept(int playerId, uint sequence)
        {
            if (_last.TryGetValue(playerId, out uint last) && sequence <= last) return false;
            _last[playerId] = sequence;
            return true;
        }

        public void Reset(int playerId) => _last.Remove(playerId);
    }
}
