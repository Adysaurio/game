using Unity.Netcode;

namespace TrashPandas.Runtime.Net
{
    /// <summary>Whether this machine runs the game simulation: offline (debug mode) or as the online host.</summary>
    public static class SimulationAuthority
    {
        public static bool IsOnline => NetworkManager.Singleton && NetworkManager.Singleton.IsListening;
        public static bool IsSimulating => !IsOnline || NetworkManager.Singleton.IsServer;
    }
}
