using System;
using System.Threading.Tasks;
using TrashPandas.Core.Session;
using TrashPandas.Runtime.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrashPandas.Runtime.Menu
{
    /// <summary>Greybox main menu and lobby (IMGUI for now; the real UI comes with the art pass).</summary>
    public sealed class MainMenu : MonoBehaviour
    {
        public string GameScene = "Greybox_Trenchcoat";

        string _code = "";
        string _status = "";
        bool _busy;

        static SessionHost Session => SessionHost.Instance;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (!string.IsNullOrEmpty(SessionHost.LastMessage)) { _status = SessionHost.LastMessage; SessionHost.LastMessage = null; }
        }

        async void Run(Func<Task> action, string working)
        {
            if (_busy) return;
            _busy = true;
            _status = working;
            try { await action(); _status = ""; }
            catch (Exception e) { _status = e.Message; Debug.LogWarning(e); await Session.LeaveAsync(e.Message); _status = e.Message; }
            finally { _busy = false; }
        }

        void OnGUI()
        {
            float w = Mathf.Min(460f, Screen.width - 32f);
            GUILayout.BeginArea(new Rect((Screen.width - w) / 2f, 60f, w, Screen.height - 120f));
            var title = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
            GUILayout.Label("Trash Pandas in a Trenchcoat", title);
            GUILayout.Space(12);

            if (Session && Session.IsOnline) DrawLobby();
            else DrawMain();

            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Space(12);
                GUILayout.Label(_status, new GUIStyle(GUI.skin.label) { wordWrap = true });
            }
            GUILayout.EndArea();
        }

        void DrawMain()
        {
            GUI.enabled = !_busy;
            GUILayout.Label("Play online");
            if (GUILayout.Button("Host a room", GUILayout.Height(36)))
                Run(() => Session.HostAsync(new RelayConnector()), "Creating a room…");
            GUILayout.BeginHorizontal();
            _code = GUILayout.TextField(_code, 16, GUILayout.Height(30));
            if (GUILayout.Button("Join", GUILayout.Width(100), GUILayout.Height(30)))
            {
                string code = JoinCode.Normalize(_code);
                Run(() => Session.JoinAsync(new RelayConnector(), code), $"Joining {code}…");
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Type the room code your host shares with you.", new GUIStyle(GUI.skin.label) { fontSize = 11 });

            GUILayout.Space(16);
            GUILayout.Label("This computer (Multiplayer Play Mode / LAN testing)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Host local", GUILayout.Height(30)))
                Run(() => Session.HostAsync(new LocalConnector()), "Hosting on this computer…");
            if (GUILayout.Button("Join local", GUILayout.Height(30)))
                Run(() => Session.JoinAsync(new LocalConnector(), LocalConnector.DefaultAddress), "Joining local host…");
            GUILayout.EndHorizontal();

            GUILayout.Space(16);
            if (GUILayout.Button("Debug mode (one person plays every role)", GUILayout.Height(30)))
                SceneManager.LoadScene(GameScene);
            GUI.enabled = true;
        }

        void DrawLobby()
        {
            var nm = NetworkManager.Singleton;
            if (nm.IsServer)
            {
                int players = nm.ConnectedClientsIds.Count;
                if (!string.IsNullOrEmpty(Session.RoomCode))
                {
                    GUILayout.Label("Room code — share it with your friends:");
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(Session.RoomCode, new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold });
                    if (GUILayout.Button("Copy", GUILayout.Width(80), GUILayout.Height(30))) GUIUtility.systemCopyBuffer = Session.RoomCode;
                    GUILayout.EndHorizontal();
                }
                GUILayout.Label($"Players in the room: {players} / {SessionRoster.MaxPlayers}");
                GUI.enabled = players >= 2 && !_busy;
                if (GUILayout.Button(players >= 2 ? "Start" : "Start (needs at least 2 players)", GUILayout.Height(36)))
                    Session.StartRound();
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label(nm.IsConnectedClient ? "Connected! Waiting for the host to start…" : "Connecting…");
            }
            GUILayout.Space(8);
            if (GUILayout.Button("Leave", GUILayout.Height(30))) Run(() => Session.LeaveAsync(), "Leaving…");
        }
    }
}
