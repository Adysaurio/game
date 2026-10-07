using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Core.Perception
{
    /// <summary>How odd the trenchcoat looks to an onlooker, 0 (normal gentleman) to 1 (clearly not a person).</summary>
    public static class Weirdness
    {
        public const float PerVisibleProblem = 0.3f;
        public const float DiscordWeight = 0.5f;

        public static float Of(in BodyIntent i)
        {
            if (i.Collapsed) return 1f;
            float w = 0f;
            if (i.LeftArmLimp) w += PerVisibleProblem;
            if (i.RightArmLimp) w += PerVisibleProblem;
            if (i.HeadSlumped) w += PerVisibleProblem;
            if (i.LeftLegLimp || i.RightLegLimp) w += PerVisibleProblem;
            w += Mathf.Clamp01(i.Discord) * DiscordWeight;
            return Mathf.Clamp01(w);
        }
    }
}
