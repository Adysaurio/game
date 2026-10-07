namespace TrashPandas.Core.Round
{
    /// <summary>The infiltration has a time limit; after it, suspicion climbs until RUN! (spec §16.5).</summary>
    public sealed class InfiltrationClock
    {
        public readonly float Limit, OvertimeRate;
        float _start = float.NaN;

        public InfiltrationClock(float limit = 480f, float overtimeRate = 10f)
        {
            Limit = limit;
            OvertimeRate = overtimeRate;
        }

        public void Begin(float now) => _start = now;

        public float SecondsLeft(float now) => float.IsNaN(_start) ? Limit : System.Math.Max(0f, Limit - (now - _start));

        public float OvertimeSuspicion(float now, float dt) =>
            float.IsNaN(_start) || now - _start < Limit ? 0f : OvertimeRate * dt;
    }
}
