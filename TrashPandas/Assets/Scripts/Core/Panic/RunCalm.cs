namespace TrashPandas.Core.Panic
{
    /// <summary>
    /// The RUN can cool down (Metal Gear: alert → evasion → back to normal): while every free raccoon is hidden
    /// and nobody sees any of them, the alert drains; seen again, it's full. Empty: the RUN is over.
    /// </summary>
    public sealed class RunCalm
    {
        /// <summary>Seconds of everyone hiding to calm a full alert (a bit more than before the RUN).</summary>
        public const float HideSeconds = 10f;
        /// <summary>Hiding doesn't count during the first moments of the RUN.</summary>
        public const float MinRunSeconds = 6f;

        public float Alert01 { get; private set; } = 1f;

        public void Reset() => Alert01 = 1f;

        /// <summary>Returns true the moment the alert reaches zero.</summary>
        public bool Update(float dt, float runTime, bool seen, bool allHidden)
        {
            if (seen) { Alert01 = 1f; return false; }
            if (!allHidden || runTime < MinRunSeconds || Alert01 <= 0f) return false;
            Alert01 = System.Math.Max(0f, Alert01 - dt / HideSeconds);
            return Alert01 <= 0f;
        }
    }
}
