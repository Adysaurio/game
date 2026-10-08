using System;
using TrashPandas.Core.Raccoons;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>Raccoon noises (running, hard landings, crashes). The host's humans listen (see SuspicionDirector).</summary>
    public static class NoiseBus
    {
        public static event Action<NoiseKind, Vector3> Heard;
        public static void Emit(NoiseKind kind, Vector3 at)
        {
            if (NoiseModel.Radius(kind) <= 0f) return;
            Heard?.Invoke(kind, at);
        }
    }
}
