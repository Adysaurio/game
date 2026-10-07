using System;
using System.Threading.Tasks;
using TrashPandas.Core.Session;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrashPandas.Runtime.Net
{
    /// <summary>
    /// The online session for the whole app: hosting/joining through an <see cref="ISessionConnector"/>, the
    /// host's <see cref="SessionRoster"/>, starting the round, and sending everyone back to the menu when the
    /// host goes away. Lives next to the NetworkManager and survives scene loads.
    /// </summary>
    [RequireComponent(typeof(NetworkBootstrap))]
    public sealed class SessionHost : MonoBehaviour
    {
        public string GameScene = "Greybox_Trenchcoat";
        public string MenuScene = "Menu";

        public static SessionHost Instance { get; private set; }
        /// <summary>Shown by the menu after being sent back to it (e.g. "The host left the game").</summary>
        public static string LastMessage;

        public SessionRoster Roster { get; private set; } = new SessionRoster();
        public string RoomCode { get; private set; }
        public bool IsOnline => NetworkManager.Singleton && NetworkManager.Singleton.IsListening;
        public bool IsHost => IsOnline && NetworkManager.Singleton.IsServer;

        ISessionConnector _connector;
        bool _leaving;

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start()
        {
            var nm = NetworkManager.Singleton;
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public async Task<string> HostAsync(ISessionConnector connector)
        {
            Roster = new SessionRoster();
            _connector = connector;
            RoomCode = await connector.HostAsync(SessionRoster.MaxPlayers);
            return RoomCode;
        }

        public Task JoinAsync(ISessionConnector connector, string code)
        {
            Roster = new SessionRoster();
            _connector = connector;
            return connector.JoinAsync(code);
        }

        /// <summary>Host only: seats the connected players and loads the game for everyone.</summary>
        public void StartRound()
        {
            if (!IsHost) throw new InvalidOperationException("Only the host can start the round.");
            Roster.StartRound();
            NetworkManager.Singleton.SceneManager.LoadScene(GameScene, LoadSceneMode.Single);
        }

        public async Task LeaveAsync(string message = null)
        {
            if (_leaving) return;
            _leaving = true;
            LastMessage = message;
            try { if (_connector != null) await _connector.LeaveAsync(); }
            finally
            {
                _connector = null;
                RoomCode = null;
                Roster = new SessionRoster();
                _leaving = false;
                if (SceneManager.GetActiveScene().name != MenuScene) SceneManager.LoadScene(MenuScene);
            }
        }

        void OnClientConnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (!nm.IsServer) return;
            if (!Roster.Join(clientId))
                nm.DisconnectClient(clientId, Roster.RoundStarted ? "The game already started." : "The room is full.");
        }

        void OnClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm.IsServer)
            {
                // A player who leaves mid-round leaves an empty slot (spec §10).
                if (clientId != nm.LocalClientId) Roster.Leave(clientId);
                return;
            }
            // On a client, this callback means we lost the host (or were refused).
            string reason = string.IsNullOrEmpty(nm.DisconnectReason) ? "The host left the game." : nm.DisconnectReason;
            _ = LeaveAsync(reason);
        }
    }
}
