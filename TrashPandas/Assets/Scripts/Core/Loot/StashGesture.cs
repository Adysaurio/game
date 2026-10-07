namespace TrashPandas.Core.Loot
{
    /// <summary>"Bring it to your chest and hold it there" — how the arms put something in the coat's pocket.</summary>
    public sealed class StashGesture
    {
        public const float Distance = 0.45f, HoldSeconds = 0.5f;
        float _held;
        bool _fired;

        /// <returns>True exactly once, when the item has been held close enough for long enough.</returns>
        public bool Tick(float distanceToChest, float dt)
        {
            if (_fired) return false;
            if (distanceToChest > Distance) { _held = 0f; return false; }
            _held += dt;
            if (_held < HoldSeconds) return false;
            _fired = true;
            return true;
        }

        public void Reset()
        {
            _held = 0f;
            _fired = false;
        }
    }
}
