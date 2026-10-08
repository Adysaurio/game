namespace TrashPandas.Core.Raccoons
{
    public enum ClickResult { None, Tap, Throw }

    public struct ClickOutcome
    {
        public ClickResult Kind;
        public float Strength; // 0..1, for throws
    }

    /// <summary>One mouse button, two meanings: a quick click grabs/drops, holding then releasing throws.</summary>
    public sealed class ClickIntent
    {
        public const float TapMax = 0.25f, FullCharge = 1f;
        float _pressedAt = float.NaN;

        public void Press(float now) => _pressedAt = now;

        public float Charge(float now)
        {
            if (float.IsNaN(_pressedAt)) return 0f;
            float held = now - _pressedAt;
            return held < TapMax ? 0f : System.Math.Min(1f, held / FullCharge);
        }

        public ClickOutcome Release(float now)
        {
            if (float.IsNaN(_pressedAt)) return default;
            float held = now - _pressedAt;
            _pressedAt = float.NaN;
            if (held < TapMax) return new ClickOutcome { Kind = ClickResult.Tap };
            return new ClickOutcome { Kind = ClickResult.Throw, Strength = System.Math.Min(1f, held / FullCharge) };
        }
    }
}
