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
        /// <summary>Seconds a loose raccoon must stay in view (up close) before this guest registers it.</summary>
        public float NoticeTime = 1.4f;
        /// <summary>How fast the "?" drains once the raccoon is out of sight (per second, of a full meter).</summary>
        public float AwarenessDecay = 0.6f;
    }

    /// <summary>One wedding guest's reaction to what they see: calm → curious ("?") → alarmed ("!").</summary>
    public sealed class GuestMind
    {
        readonly GuestSettings _s;
        float _sinceOdd = float.MaxValue;   // since last noticeable weirdness
        float _veryOddFor;                  // continuous time seeing high weirdness
        float _alarmedFor;
        /// <summary>0..1: how close this guest is to registering a raccoon (the filling "?").</summary>
        public float Awareness { get; private set; }

        public GuestState State { get; private set; } = GuestState.Calm;
        /// <summary>True only on the update that turned this guest alarmed.</summary>
        public bool JustAlarmed { get; private set; }

        public GuestMind(GuestSettings settings = null) => _s = settings ?? new GuestSettings();

        /// <param name="noticeScale">Longer to notice (&gt;1) when the raccoon is far away or sneaking.</param>
        public GuestState Update(float dt, float seenWeirdness, bool seesRaccoon, float noticeScale = 1f)
        {
            JustAlarmed = false;
            bool odd = seenWeirdness >= _s.CuriousThreshold;
            _sinceOdd = odd || seesRaccoon ? 0f : _sinceOdd + dt;
            _veryOddFor = seenWeirdness >= _s.AlarmThreshold ? _veryOddFor + dt : 0f;
            Awareness = seesRaccoon
                ? System.Math.Min(1f, Awareness + dt / (_s.NoticeTime * System.Math.Max(0.1f, noticeScale)))
                : System.Math.Max(0f, Awareness - dt * _s.AwarenessDecay);
            bool registersRaccoon = Awareness >= 1f - 1e-4f;

            if (registersRaccoon || _veryOddFor >= _s.AlarmAfter)
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
                    if (odd || Awareness > 0.05f) State = GuestState.Curious;
                    break;
            }
            return State;
        }
    }
}
