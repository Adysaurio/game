using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace TrashPandas.Runtime.Net
{
    /// <summary>
    /// Owns the single NetworkManager for the whole app (survives scene loads) and registers the prefabs
    /// that can be spawned at runtime.
    /// </summary>
    [RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
    public sealed class NetworkBootstrap : MonoBehaviour
    {
        public List<GameObject> SpawnablePrefabs = new List<GameObject>();

        public static NetworkBootstrap Instance { get; private set; }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            var nm = GetComponent<NetworkManager>();
            nm.NetworkConfig.NetworkTransport = GetComponent<UnityTransport>();
            nm.NetworkConfig.EnableSceneManagement = true;
            foreach (var prefab in SpawnablePrefabs)
                if (prefab && !nm.NetworkConfig.Prefabs.Contains(prefab)) nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
        }
    }
}
