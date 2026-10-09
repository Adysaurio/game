namespace TrashPandas.Core.Round
{
    /// <summary>
    /// The way out: the exits open once every objective is at the den ("GETAWAY!"), or when the clock runs
    /// out and the party's over (then it's a RUN you can't hide from — get out!).
    /// </summary>
    public static class Getaway
    {
        /// <summary>Time's up — but only once the round is set up (before that the clock reads 0).</summary>
        public static bool PartyOver(float secondsLeft, byte objectivesPicked) => objectivesPicked != 0 && secondsLeft <= 0f;

        public static bool ExitsOpen(byte objectivesPicked, byte objectivesDone, float secondsLeft) =>
            objectivesPicked != 0 && (PartyOver(secondsLeft, objectivesPicked) || (objectivesDone & objectivesPicked) == objectivesPicked);
    }
}
