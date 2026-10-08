namespace TrashPandas.Core.Raccoons
{
    /// <summary>Raccoons standing on each other's heads.</summary>
    public static class TowerRules
    {
        public const int MaxHeight = 5;
        public const float SlowPerRider = 0.6f;

        public static float SpeedFactor(int riders) => (float)System.Math.Pow(SlowPerRider, riders);

        public static bool Collapses(bool bottomRunning, bool bottomHit) => bottomRunning || bottomHit;

        public static bool CanMount(bool targetCarryingHeavy, bool targetFrozen, int towerSize, bool targetCaught = false) =>
            !targetCarryingHeavy && !targetFrozen && !targetCaught && towerSize < MaxHeight;

        /// <summary>Only the raccoon on the ground reaches an exit; riders fall off and keep playing.</summary>
        public static bool CountsForExit(bool riding) => !riding;
    }
}
