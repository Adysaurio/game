namespace TrashPandas.Core.Raccoons
{
    public enum IntroPhase { Establishing, PoppingOut, LineUp, Go, Done }

    /// <summary>
    /// The round's opening gag, the same for everyone: the gang climbs out of the manhole one by one,
    /// lines up like a heist crew, "GO!", then each player gets their own camera.
    /// </summary>
    public sealed class IntroTimeline
    {
        public const float Establish = 1.2f, PopGap = 0.45f, PopFlight = 0.55f, LineUpHold = 1.4f, GoTime = 0.8f;
        readonly int _players;

        public IntroTimeline(int players) => _players = System.Math.Max(1, players);

        public float PopTime(int index) => Establish + index * PopGap;
        public float LastLanding => PopTime(_players - 1) + PopFlight;
        public float Duration => LastLanding + LineUpHold + GoTime;

        public IntroPhase PhaseAt(float t)
        {
            if (t >= Duration) return IntroPhase.Done;
            if (t >= Duration - GoTime) return IntroPhase.Go;
            if (t >= LastLanding) return IntroPhase.LineUp;
            if (t >= Establish) return IntroPhase.PoppingOut;
            return IntroPhase.Establishing;
        }

        /// <summary>0 = still underground, 1 = landed on its spot.</summary>
        public float FlightProgress(int index, float t) => System.Math.Clamp((t - PopTime(index)) / PopFlight, 0f, 1f);
    }
}
