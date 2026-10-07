using System.Collections.Generic;

namespace TrashPandas.Core.Session
{
    /// <summary>
    /// Re-stamps each player's jump press with the host time it first arrived. Client clocks lag the host by
    /// about half a round trip, so comparing their stamps with the host's "now" would make coordinated jumps
    /// fail on real internet latency; arrival times share one clock.
    /// </summary>
    public sealed class JumpStampRebaser
    {
        readonly Dictionary<int, (float client, float host)> _last = new Dictionary<int, (float, float)>();

        public float Rebase(int playerId, float clientStamp, float hostNow)
        {
            if (float.IsNegativeInfinity(clientStamp) || float.IsNaN(clientStamp)) return float.NegativeInfinity;
            if (_last.TryGetValue(playerId, out var seen) && seen.client == clientStamp) return seen.host;
            _last[playerId] = (clientStamp, hostNow);
            return hostNow;
        }

        public void Reset(int playerId) => _last.Remove(playerId);
    }
}
