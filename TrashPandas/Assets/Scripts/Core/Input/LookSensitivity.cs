using UnityEngine;

namespace TrashPandas.Core.Input
{
    /// <summary>Camera look sensitivity (degrees per pixel) adjustable in multiplicative steps.</summary>
    public sealed class LookSensitivity
    {
        public const float Default = 0.3f;   // tuned for trackpads; mice usually want less
        public const float Min = 0.03f;
        public const float Max = 2f;
        public const float Step = 1.25f;

        public float Value { get; private set; }

        public LookSensitivity(float initial)
        {
            Value = float.IsNaN(initial) || initial <= 0f ? Default : Mathf.Clamp(initial, Min, Max);
        }

        public void Increase() => Value = Mathf.Min(Max, Value * Step);
        public void Decrease() => Value = Mathf.Max(Min, Value / Step);
    }
}
