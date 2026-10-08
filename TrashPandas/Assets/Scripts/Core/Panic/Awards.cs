using System.Collections.Generic;

namespace TrashPandas.Core.Panic
{
    public enum Award { Boss, Hero, Pinata, FirstToFall }

    public struct PlayerStats
    {
        public int Player;
        public int Loot;
        public int Hits;
        public int Rescues;
        public bool Escaped;
        /// <summary>0 = first caught, 1 = second… ; -1 = never caught.</summary>
        public int CaughtOrder;
    }

    /// <summary>End-of-round highlights: something to brag about (or be roasted for).</summary>
    public static class Awards
    {
        public static string Title(Award a) => a switch
        {
            Award.Boss => "THE BOSS — biggest haul",
            Award.Hero => "HERO — freed a friend",
            Award.Pinata => "PIÑATA — took a beating, got out anyway",
            Award.FirstToFall => "FIRST TO FALL",
            _ => a.ToString(),
        };

        public static Dictionary<Award, int> For(IReadOnlyList<PlayerStats> stats)
        {
            var result = new Dictionary<Award, int>();
            PlayerStats? boss = null, hero = null, pinata = null, first = null;
            foreach (var s in stats)
            {
                if (s.Loot > 0 && (!boss.HasValue || s.Loot > boss.Value.Loot)) boss = s;
                if (s.Rescues > 0 && (!hero.HasValue || s.Rescues > hero.Value.Rescues)) hero = s;
                if (s.Escaped && s.Hits > 0 && (!pinata.HasValue || s.Hits > pinata.Value.Hits)) pinata = s;
                if (s.CaughtOrder == 0) first = s;
            }
            if (boss.HasValue) result[Award.Boss] = boss.Value.Player;
            if (hero.HasValue) result[Award.Hero] = hero.Value.Player;
            if (pinata.HasValue) result[Award.Pinata] = pinata.Value.Player;
            if (first.HasValue) result[Award.FirstToFall] = first.Value.Player;
            return result;
        }
    }
}
