namespace TrashPandas.Core.Npc
{
    public enum GuestState : byte { Calm, Curious, Alarmed }

    public sealed class GuestSettings
    {
        /// <summary>Weirdness below this goes unnoticed.</summary>
        public float CuriousThreshold = 0.25f;
        /// <summary>Weirdness at or above this, seen for <see cref="AlarmAfter"/> seconds, alarms.</summary>
        public float AlarmThreshold = 0.7f;
        public float AlarmAfter = 1.5f;
        public float AlarmedHold = 4f;
        public float CuriousHold = 2.5f;
    }

    /// <summary>One wedding guest's reaction to what they see: calm → curious ("?") → alarmed ("!").</summary>
    public sealed class GuestMind
    {
        readonly GuestSettings _s;
        float _sinceOdd = float.MaxValue;   // since last noticeable weirdness
        float _veryOddFor;                  // continuous time seeing high weirdness
        float _alarmedFor;

        public GuestState State { get; private set; } = GuestState.Calm;
        /// <summary>True only on the update that turned this guest alarmed.</summary>
        public bool JustAlarmed { get; private set; }

        public GuestMind(GuestSettings settings = null) => _s = settings ?? new GuestSettings();

        public GuestState Update(float dt, float seenWeirdness, bool seesRaccoon)
        {
            JustAlarmed = false;
            bool odd = seenWeirdness >= _s.CuriousThreshold;
            _sinceOdd = odd || seesRaccoon ? 0f : _sinceOdd + dt;
            _veryOddFor = seenWeirdness >= _s.AlarmThreshold ? _veryOddFor + dt : 0f;

            if (seesRaccoon || _veryOddFor >= _s.AlarmAfter)
            {
                if (State != GuestState.Alarmed) JustAlarmed = true;
                State = GuestState.Alarmed;
                _alarmedFor = 0f;
                return State;
            }

            switch (State)
            {
                case GuestState.Alarmed:
                    _alarmedFor += dt;
                    if (_alarmedFor >= _s.AlarmedHold) { State = GuestState.Curious; _sinceOdd = 0f; }
                    break;
                case GuestState.Curious:
                    if (_sinceOdd >= _s.CuriousHold) State = GuestState.Calm;
                    break;
                default:
                    if (odd) State = GuestState.Curious;
                    break;
            }
            return State;
        }
    }
}
