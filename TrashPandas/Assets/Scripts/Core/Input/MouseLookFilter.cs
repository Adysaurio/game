using UnityEngine;

namespace TrashPandas.Core.Input
{
    /// <summary>
    /// Guards raw mouse deltas: drops the frames right after the cursor lock changes (macOS reports a
    /// large bogus delta then) and discards single-frame spikes.
    /// </summary>
    public sealed class MouseLookFilter
    {
        public int SettleFrames = 3;
        public float MaxDeltaPerFrame = 400f;

        int _settle;

        public void NotifyLockChanged() => _settle = SettleFrames;

        public Vector2 Filter(Vector2 raw)
        {
            if (_settle > 0) { _settle--; return Vector2.zero; }
            if (float.IsNaN(raw.x) || float.IsNaN(raw.y)) return Vector2.zero;
            if (raw.magnitude > MaxDeltaPerFrame) return Vector2.zero;
            return raw;
        }
    }
}
