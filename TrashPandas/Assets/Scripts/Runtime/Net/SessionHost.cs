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
        bool _leaving;          // a leave is in progress (by choice or after losing the host)
        bool _leftByChoice;     // the local player pressed Leave/F10
        bool _wasConnected;     // we reached the host at least once this session

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        bool _subscribed;

        /// <summary>Hook NetworkManager callbacks before any connection starts (script start order is not guaranteed).</summary>
        void EnsureSubscribed()
        {
            if (_subscribed) return;
            var nm = NetworkManager.Singleton;
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
            nm.OnClientStopped += OnClientStopped;
            _subscribed = true;
        }

        public async Task<string> HostAsync(ISessionConnector connector)
        {
            EnsureSubscribed();
            _leftByChoice = _wasConnected = false;
            Roster = new SessionRoster();
            _connector = connector;
            RoomCode = await connector.HostAsync(SessionRoster.MaxPlayers);
            // The host is a player too; register it even if its connect callback already fired.
            Roster.Join(NetworkManager.Singleton.LocalClientId);
            return RoomCode;
        }

        public Task JoinAsync(ISessionConnector connector, string code)
        {
            EnsureSubscribed();
            _leftByChoice = _wasConnected = false;
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

        /// <summary>Host: everyone still connected plays a fresh round of the game scene.</summary>
        public void RestartRound()
        {
            if (!IsHost) return;
            Roster.EndRound();
            Roster.StartRound();
            NetworkManager.Singleton.SceneManager.LoadScene(GameScene, LoadSceneMode.Single);
        }

        /// <summary>Leave on purpose (menu button, F10, or a connection error shown by the menu).</summary>
        public Task LeaveAsync(string message = null)
        {
            _leftByChoice = true;
            return StopAndReturnToMenuAsync(message);
        }

        async Task StopAndReturnToMenuAsync(string message)
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
                if (SceneManager.GetActiveScene().name != MenuScene) SceneManager.LoadScene(MenuScene);
                // _leaving stays set until NetworkManager reports it stopped (see OnClientStopped).
                if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsListening) _leaving = false;
            }
        }

        void OnClientConnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (clientId == nm.LocalClientId) _wasConnected = true;
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
            // On a client, losing the host is handled in OnClientStopped (it also covers transport failures).
        }

        /// <summary>Fires on every local stop: leaving, host gone, transport failure, session closed by the service.</summary>
        void OnClientStopped(bool wasHost)
        {
            var nm = NetworkManager.Singleton;
            string message = ConnectionMessages.ForClientStopped(_wasConnected, _leftByChoice, wasHost ? null : nm.DisconnectReason);
            bool alreadyLeaving = _leaving;
            _leaving = false;
            if (alreadyLeaving || _leftByChoice) { _leftByChoice = false; if (message != null) LastMessage = message; return; }
            _ = StopAndReturnToMenuAsync(message);
        }
    }
}
