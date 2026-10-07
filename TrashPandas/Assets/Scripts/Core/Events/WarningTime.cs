using UnityEngine;

namespace TrashPandas.Core.Events
{
    /// <summary>
    /// How long the "EVENT IN N" warning lasts: enough for the farthest raccoon to run back, plus a margin
    /// (spec §6b), so it stays fair however big the map gets.
    /// </summary>
    public static class WarningTime
    {
        public const float RaccoonSpeed = 4f, Margin = 3f, Min = 8f, Max = 20f;

        public static float Compute(float farthestRaccoonDistance) =>
            Mathf.Clamp(Mathf.Max(0f, farthestRaccoonDistance) / RaccoonSpeed + Margin, Min, Max);
    }
}
