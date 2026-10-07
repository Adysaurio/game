using System;

namespace TrashPandas.Core.Events
{
    /// <summary>When the next social event happens and who comes over (never the same person twice in a row).</summary>
    public sealed class EventScheduler
    {
        public float FirstMin = 35f, FirstMax = 50f, GapMin = 45f, GapMax = 75f;

        readonly Random _random;
        int _lastSpeaker = -1;

        public float NextAt { get; private set; }

        public EventScheduler(int seed)
        {
            _random = new Random(seed);
            NextAt = Range(FirstMin, FirstMax);
        }

        public bool IsDue(float now) => now >= NextAt;

        public void EventFinished(float now) => NextAt = now + Range(GapMin, GapMax);

        public int PickSpeaker(int count)
        {
            if (count <= 1) return _lastSpeaker = 0;
            int pick = _random.Next(count - 1);
            if (pick >= _lastSpeaker && _lastSpeaker >= 0) pick++;
            return _lastSpeaker = pick;
        }

        float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
    }
}
