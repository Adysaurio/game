using UnityEngine;

namespace TrashPandas.Core.Raccoons
{
    public enum NoiseKind { Sneaking, Running, HardLanding, Crash }

    /// <summary>How far each noise carries, and who hears it (walls halve the distance).</summary>
    public static class NoiseModel
    {
        public static float Radius(NoiseKind kind) => kind switch
        {
            NoiseKind.Running => 6f,
            NoiseKind.HardLanding => 4f,
            NoiseKind.Crash => 7f,
            _ => 0f,
        };

        public static bool Hears(Vector3 listener, Vector3 source, float radius, bool occluded) =>
            Vector3.Distance(listener, source) <= (occluded ? radius * 0.5f : radius);
    }
}
