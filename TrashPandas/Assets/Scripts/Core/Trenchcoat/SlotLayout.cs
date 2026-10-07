using System;
using System.Collections.Generic;

namespace TrashPandas.Core.Trenchcoat
{
    /// <summary>Which body parts each slot controls, by player count (spec §4).</summary>
    public static class SlotLayout
    {
        public const int MinPlayers = 2;
        public const int MaxPlayers = 5;

        static readonly BodyPart[][] Layouts =
        {
            new[] { BodyPart.Legs, BodyPart.Arms | BodyPart.Head },
            new[] { BodyPart.Legs, BodyPart.Arms, BodyPart.Head },
            new[] { BodyPart.LegLeft, BodyPart.LegRight, BodyPart.Arms, BodyPart.Head },
            new[] { BodyPart.LegLeft, BodyPart.LegRight, BodyPart.ArmLeft, BodyPart.ArmRight, BodyPart.Head },
        };

        public static IReadOnlyList<BodyPart> ForPlayerCount(int playerCount)
        {
            if (playerCount < MinPlayers || playerCount > MaxPlayers)
                throw new ArgumentOutOfRangeException(nameof(playerCount), playerCount,
                    $"A trenchcoat supports {MinPlayers}-{MaxPlayers} players.");
            return Array.AsReadOnly(Layouts[playerCount - MinPlayers]);
        }
    }
}
