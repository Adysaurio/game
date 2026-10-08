namespace TrashPandas.Core.Round
{
    /// <summary>
    /// The way out: the exits open once every objective is at the den ("GETAWAY!"), or when the clock runs
    /// out and the party's over (then it's a RUN you can't hide from — get out!).
    /// </summary>
    public static class Getaway
    {
        public static bool PartyOver(float secondsLeft) => secondsLeft <= 0f;

        public static bool ExitsOpen(byte objectivesPicked, byte objectivesDone, float secondsLeft) =>
            PartyOver(secondsLeft) || (objectivesPicked != 0 && (objectivesDone & objectivesPicked) == objectivesPicked);
    }
}
