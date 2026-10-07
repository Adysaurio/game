namespace TrashPandas.Core.Panic
{
    /// <summary>
    /// The first moments of RUN!: the humans freeze in shock before chasing, and nobody can be hit until the
    /// raccoons have had a head start.
    /// </summary>
    public sealed class PanicGrace
    {
        readonly float _surprise, _noHit;
        float _start = float.NegativeInfinity;

        public PanicGrace(float surpriseSeconds = 2f, float noHitSeconds = 3f)
        {
            _surprise = surpriseSeconds;
            _noHit = noHitSeconds;
        }

        public void Begin(float now) => _start = now;
        public bool ChasersMayMove(float now) => now - _start >= _surprise;
        public bool MayHit(float now) => now - _start >= _noHit;
    }
}
