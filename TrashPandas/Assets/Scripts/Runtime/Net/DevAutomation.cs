using System;
using System.Linq;
using TrashPandas.Core.Trenchcoat;
using TrashPandas.Runtime.Grabbing;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.Runtime.Net
{
    /// <summary>
    /// Development-only command-line automation so builds can test themselves:
    ///   -autohost-local | -autojoin-local      connect without the menu
    ///   -autostart N                            host starts the round once N players are in
    ///   -bot walk|reach|hop                     fake this player's input
    ///   -telemetry                              log what this instance sees twice a second
    ///   -quitafter SECONDS                      exit automatically
    /// </summary>
    public sealed class DevAutomation : MonoBehaviour
    {
        public static string Bot { get; private set; }
        static string[] Args => Environment.GetCommandLineArgs();

        int _autoStart;
        float _quitAt = float.MaxValue;
        bool _telemetry;
        float _nextLog;
        bool _connected;

        static string Value(string flag)
        {
            var args = Args;
            int i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static DevAutomation s_instance;
        static bool s_autoConnectDone;

        void Awake()
        {
            if (s_instance && s_instance != this) { Destroy(gameObject); return; }
            s_instance = this;
            DontDestroyOnLoad(gameObject);
            Bot = Value("-bot");
            int.TryParse(Value("-autostart"), out _autoStart);
            if (float.TryParse(Value("-quitafter"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float q)) _quitAt = q;
            _telemetry = Args.Contains("-telemetry");
        }

        async void Start()
        {
            if (_connected || s_autoConnectDone || !SessionHost.Instance) return;
            _connected = s_autoConnectDone = true; // only once per launch, not on every return to the menu
            if (Args.Contains("-debugmode")) { UnityEngine.SceneManagement.SceneManager.LoadScene("Greybox_Trenchcoat"); return; }
            if (Args.Contains("-autohost-local")) await SessionHost.Instance.HostAsync(new LocalConnector());
            else if (Args.Contains("-autojoin-local")) await SessionHost.Instance.JoinAsync(new LocalConnector(), LocalConnector.DefaultAddress);
        }

        void Update()
        {
            if (Time.realtimeSinceStartup > _quitAt) { Log("quit"); Application.Quit(); }
            var nm = NetworkManager.Singleton;
            var session = SessionHost.Instance;
            if (_autoStart > 0 && session && session.IsHost && !session.Roster.RoundStarted && session.Roster.Clients.Count >= _autoStart)
            {
                Log($"autostart with {nm.ConnectedClientsIds.Count} players");
                session.StartRound();
            }
            if (_telemetry && Time.realtimeSinceStartup >= _nextLog)
            {
                _nextLog = Time.realtimeSinceStartup + 0.5f;
                LogTelemetry();
            }
        }

        static void LogTelemetry()
        {
            var nm = NetworkManager.Singleton;
            var coat = NetworkedTrenchcoat.Instance;
            if (!nm || !nm.IsListening)
            {
                var offlineBody = UnityEngine.Object.FindFirstObjectByType<TrashPandas.Runtime.Trenchcoat.TrenchcoatBody>();
                var glass = UnityEngine.GameObject.Find("Glass_1a");
                Log(offlineBody
                    ? $"offline-debug coat={offlineBody.transform.position} kinematic={offlineBody.GetComponent<Rigidbody>().isKinematic} glassKinematic={(glass ? glass.GetComponent<Rigidbody>().isKinematic.ToString() : "-")}"
                    : "offline");
                return;
            }
            if (!coat) { Log($"lobby clients={nm.ConnectedClientsIds.Count}"); return; }
            var snap = coat.Slots;
            string seats = string.Join(",", Enumerable.Range(0, snap.SlotCount).Select(i => snap.OccupantClient(i)?.ToString() ?? "-"));
            var b = coat.Body;
            string held = string.Join(",", Grabbable.All.Where(g => g && g.IsHeld).Select(g => g.name));
            string raccoon = NetworkedRaccoon.LocalOwned ? $" myRaccoon={NetworkedRaccoon.LocalOwned.transform.position}" : "";
            Log($"me={nm.LocalClientId} host={nm.IsHost} seats=[{seats}] missing={snap.MissingParts} coat={b.transform.position} yaw={b.transform.eulerAngles.y:F0} leftHand={b.LeftHand.localPosition} heldOnHost=[{held}] raccoons={UnityEngine.Object.FindObjectsByType<NetworkedRaccoon>(FindObjectsSortMode.None).Length}{raccoon}");
        }

        static void Log(string msg) => Debug.Log($"[DEV {Time.realtimeSinceStartup:F1}] {msg}");
    }
}
