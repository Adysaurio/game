using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace TrashPandas.Runtime.Net
{
    /// <summary>
    /// Owns the single NetworkManager for the whole app (survives scene loads). Runtime-spawned prefabs are
    /// registered by Netcode's auto-generated DefaultNetworkPrefabs list, not here.
    /// </summary>
    [DefaultExecutionOrder(-10000)] // before NetworkManager.Awake, so a duplicate never initializes
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        public static NetworkBootstrap Instance { get; private set; }

        void Awake()
        {
            // Returning to the menu loads another copy of this object: keep the original.
            if (Instance && Instance != this) { DestroyImmediate(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            var nm = GetComponent<NetworkManager>();
            nm.NetworkConfig.NetworkTransport = GetComponent<UnityTransport>();
            nm.NetworkConfig.EnableSceneManagement = true;
        }
    }
}
