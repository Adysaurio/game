using System.Collections.Generic;

namespace TrashPandas.Core.Session
{
    /// <summary>
    /// Drops duplicated or out-of-order inputs sent over an unreliable channel, and tells when a player has
    /// gone quiet (crashed or lost connection) so their last input stops being obeyed.
    /// </summary>
    public sealed class InputSequencer
    {
        readonly Dictionary<int, uint> _last = new Dictionary<int, uint>();
        readonly Dictionary<int, float> _acceptedAt = new Dictionary<int, float>();

        public bool Accept(int playerId, uint sequence, float now = 0f)
        {
            if (_last.TryGetValue(playerId, out uint last) && sequence <= last) return false;
            _last[playerId] = sequence;
            _acceptedAt[playerId] = now;
            return true;
        }

        public bool IsFresh(int playerId, float now, float maxAge) =>
            _acceptedAt.TryGetValue(playerId, out float at) && now - at <= maxAge;

        public void Reset(int playerId)
        {
            _last.Remove(playerId);
            _acceptedAt.Remove(playerId);
        }
    }
}
