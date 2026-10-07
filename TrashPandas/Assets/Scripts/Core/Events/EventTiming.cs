using UnityEngine;

namespace TrashPandas.Core.Events
{
    /// <summary>How long players get to read a line before the response timer starts.</summary>
    public static class EventTiming
    {
        public const float MinReading = 2.5f, MaxReading = 5f, CharsPerSecond = 22f;

        public static float ReadingTime(string line) =>
            Mathf.Clamp(1f + (line?.Length ?? 0) / CharsPerSecond, MinReading, MaxReading);
    }
}
