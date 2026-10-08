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
        /// <summary>When the hop bots leave the coat (seconds into the level), -hopat N; default 3.</summary>
        public static float HopAt { get; private set; } = 3f;

        /// <summary>Dev bots: once the panic starts, run for an exit — "hopflee" the sewer (open path),
        /// "hopgap" crouching through the hedge gap where humans can't follow.</summary>
        public static Vector2? FleeMove(Vector3 from)
        {
            if (Bot != "hopflee" && Bot != "flee" && Bot != "hopgap" && Bot != "mouthflee" && Bot != "stashflee") return null;
            var pd = TrashPandas.Runtime.Panic.PanicDirector.Instance;
            if (!pd || pd.Phase != TrashPandas.Runtime.Panic.RoundPhase.Panic) return null;
            if (Bot == "mouthflee" && !s_mouthTaken)
            {
                var loot = NearestLoot(from);
                if (loot.HasValue) { var tl = loot.Value - from; return new Vector2(tl.x, tl.z).normalized; }
            }
            var open = pd.OpenExitPositions;
            if (open.Count == 0) return null;
            Vector3 exit = open[0];
            foreach (var e in open) if (Vector3.Distance(e, from) < Vector3.Distance(exit, from)) exit = e;
            Vector3 to = exit - from;
            return new Vector2(to.x, to.z).normalized;
        }

        /// <summary>Crouch only at the hedge (crouching halves speed).</summary>
        /// <summary>Dev bots for social events: "obey" plays every part right; "ignore" does nothing.</summary>
        public static void ApplyEventBot(TrashPandas.Core.Events.SocialEvent ev, ref TrashPandas.Core.Events.TaskInput input, ref int answerKey)
        {
            if (Bot == "ignore") { input = default; answerKey = 0; return; }
            if (Bot != "obey") return;
            answerKey = System.Array.IndexOf(ev.OptionKinds, TrashPandas.Core.Events.HeadAnswer.Good) + 1;
            input = new TrashPandas.Core.Events.TaskInput
            {
                Primary = true, Secondary = true,
                Crouch = ev.Legs == TrashPandas.Core.Events.LegsTask.Kneel,
                Jump = ev.Legs == TrashPandas.Core.Events.LegsTask.DanceStep,
            };
        }

        public static bool FleeCrouchAt(Vector3 pos) => Bot == "hopgap" && FleeActive && pos.z < -6.3f;

        static bool s_mouthTaken;

        /// <summary>"mouthflee": in the panic, grab the nearest loot with the mouth, then run for an exit.</summary>
        public static bool MouthBot(Vector3 raccoonPos)
        {
            if (Bot != "mouthflee" || s_mouthTaken || !FleeActive) return false;
            var loot = NearestLoot(raccoonPos);
            if (!loot.HasValue || Vector3.Distance(loot.Value, raccoonPos + Vector3.up * 0.3f) > 0.7f) return false;
            s_mouthTaken = true;
            return true;
        }

        static Vector3? NearestLoot(Vector3 from)
        {
            var ld = TrashPandas.Runtime.Loot.LootDirector.Instance;
            if (!ld) return null;
            Vector3? best = null;
            foreach (var item in ld.Items)
                if (item && item.State == TrashPandas.Runtime.Loot.LootState.Active && !item.Grabbable.IsHeld && item.transform.position.y < 1.2f
                    && (!best.HasValue || Vector3.Distance(item.transform.position, from) < Vector3.Distance(best.Value, from)))
                    best = item.transform.position;
            return best;
        }
        static bool FleeActive
        {
            get
            {
                var pd = TrashPandas.Runtime.Panic.PanicDirector.Instance;
                return pd && pd.Phase == TrashPandas.Runtime.Panic.RoundPhase.Panic;
            }
        }
        static string[] Args => Environment.GetCommandLineArgs();

        int _autoStart;
        float _quitAt = float.MaxValue;
        bool _telemetry;
        float _nextLog;
        bool _connected;
        float _shotAt = -1f;
        string _shotPath;

        static string Value(string flag)
        {
            var args = Args;
            int i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        static DevAutomation s_instance;
        static bool s_autoConnectDone;
        static bool s_restarted;

        void Awake()
        {
            if (s_instance && s_instance != this) { Destroy(gameObject); return; }
            s_instance = this;
            DontDestroyOnLoad(gameObject);
            Bot = Value("-bot");
            if (float.TryParse(Value("-hopat"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float hopAt)) HopAt = hopAt;
            int.TryParse(Value("-autostart"), out _autoStart);
            if (float.TryParse(Value("-quitafter"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float q)) _quitAt = q;
            _telemetry = Args.Contains("-telemetry");
            int td = Array.IndexOf(Args, "-topdown");
            if (td >= 0)
            {
                // Dev: an orthographic bird's-eye camera over the whole estate (drawn on top of the game camera).
                var cam = new GameObject("TopDownCamera").AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = td + 1 < Args.Length && float.TryParse(Args[td + 1], out var size) ? size : 31f;
                cam.transform.SetPositionAndRotation(new Vector3(0f, 80f, 2f), Quaternion.Euler(90f, 0f, 0f));
                cam.depth = 100;
                cam.farClipPlane = 200f;
                DontDestroyOnLoad(cam.gameObject);
            }
            int s = Array.IndexOf(Args, "-shot");
            if (s >= 0 && s + 2 < Args.Length && float.TryParse(Args[s + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float at))
            { _shotAt = at; _shotPath = Args[s + 2]; }
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
            if (_shotAt > 0f && Time.realtimeSinceStartup >= _shotAt)
            {
                _shotAt = -1f;
                ScreenCapture.CaptureScreenshot(_shotPath);
                Log($"screenshot {_shotPath}");
            }
            if (_telemetry && Time.realtimeSinceStartup >= _nextLog)
            {
                _nextLog = Time.realtimeSinceStartup + 0.5f;
                LogTelemetry();
            }
        }

        static string NpcSummary()
        {
            var d = TrashPandas.Runtime.Npc.SuspicionDirector.Instance;
            if (!d) return "";
            int curious = 0, alarmed = 0; string cat = "-";
            foreach (var b in d.Brains)
            {
                if (!b.Pawn) continue;
                if (b.Pawn.Kind == TrashPandas.Runtime.Npc.NpcKind.Cat) { cat = ((TrashPandas.Core.Npc.CatState)b.Pawn.Mood).ToString(); continue; }
                var s = (TrashPandas.Core.Npc.GuestState)b.Pawn.Mood;
                if (s == TrashPandas.Core.Npc.GuestState.Curious) curious++;
                if (s == TrashPandas.Core.Npc.GuestState.Alarmed) alarmed++;
            }
            var f = d.LastFrame;
            var pd = TrashPandas.Runtime.Panic.PanicDirector.Instance;
            string panic = "";
            if (pd)
            {
                var snap = pd.Snapshot;
                panic = $" phase={pd.Phase} me={pd.LocalPlayer}";
                for (int p = 0; p < snap.Count; p++) panic += $" P{p}={snap.OutcomeOf(p)}/{snap.HitsOf(p)}hits";
                int armed = 0;
                foreach (var w in UnityEngine.Object.FindObjectsByType<TrashPandas.Runtime.Panic.PanicWeapon>(FindObjectsSortMode.None)) if (w.Holder) armed++;
                panic += $" armed={armed} left={snap.SecondsLeft:F0}s";
                var opc = UnityEngine.Object.FindFirstObjectByType<OnlinePlayerController>();
                if (opc) panic += $" spectating={opc.IsSpectating}";
                if (Args.Contains("-autorestart") && pd.Phase == TrashPandas.Runtime.Panic.RoundPhase.Results && (!SimulationAuthority.IsOnline || (SessionHost.Instance && SessionHost.Instance.IsHost)) && !s_restarted)
                { s_restarted = true; Log("autorestart"); pd.PlayAgain(); }
            }
            string speeds = "";
            foreach (var pw in UnityEngine.Object.FindObjectsByType<TrashPandas.Runtime.Npc.NpcPawn>(FindObjectsSortMode.None))
                if (!string.IsNullOrEmpty(pw.SpeakerId)) speeds += $"{pw.SpeakerId}:{pw.Speed:F1} ";
            panic += $" speeds[{speeds.Trim()}]";
            var ed = TrashPandas.Runtime.Npc.SocialEventDirector.Instance;
            if (ed) { var es = ed.Snapshot; panic += $" event={ed.Phase}#{es.Serial}:{ed.Current.Speaker} t={es.SecondsLeft:F1} result={es.ResultDelta:F0}[{es.HeadOutcome}{es.ArmsOutcome}{es.LegsOutcome}] tasks={ed.Current.Arms}/{ed.Current.Legs}"; }
            var rig = UnityEngine.Object.FindFirstObjectByType<TrashPandas.Runtime.Cameras.PlayerCameraRig>();
            if (rig) panic += $" cam={(rig.VirtualCamera.Follow ? rig.VirtualCamera.Follow.name : "-")}{(rig.InConversation ? "(talk)" : "")}";
            var ld = TrashPandas.Runtime.Loot.LootDirector.Instance;
            var pdx = TrashPandas.Runtime.Panic.PanicDirector.Instance;
            if (pdx) panic += $" P0loot=${pdx.Snapshot.LootOf(0)} clean={pdx.Snapshot.CleanExit} openExits={string.Join(",", pdx.OpenExitPositions)}";
            if (ld) { var ls = ld.Snapshot; panic += $" mouth0={ls.MouthItemOf(0)} pocket=${ls.Total} objectives={ls.ObjectivesPicked:X2}/{ls.ObjectivesDone:X2} clock={ls.SecondsLeft:F0}"; }
            return panic + $" suspicion={d.Suspicion:F1} caught={d.Caught} curious={curious} alarmed={alarmed} cat={cat} frame[missing={f.MissingParts} seen={f.CoatWitnessed} weird={f.SeenWeirdness:F2} hiss={f.CatHissing}] dt={Time.deltaTime:F3}";
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
                    ? $"offline-debug held=[{string.Join(",", TrashPandas.Runtime.Grabbing.Grabbable.All.Where(g => g && g.IsHeld).Select(g => g.name))}] coat={offlineBody.transform.position} kinematic={offlineBody.GetComponent<Rigidbody>().isKinematic} glassKinematic={(glass ? glass.GetComponent<Rigidbody>().isKinematic.ToString() : "-")}{NpcSummary()}"
                    : "offline");
                return;
            }
            if (!coat) { Log($"lobby clients={nm.ConnectedClientsIds.Count}"); return; }
            var snap = coat.Slots;
            string seats = string.Join(",", Enumerable.Range(0, snap.SlotCount).Select(i => snap.OccupantClient(i)?.ToString() ?? "-"));
            var b = coat.Body;
            string held = string.Join(",", Grabbable.All.Where(g => g && g.IsHeld).Select(g => g.name));
            string raccoon = NetworkedRaccoon.LocalOwned ? $" myRaccoon={NetworkedRaccoon.LocalOwned.transform.position}" : "";
            Log($"me={nm.LocalClientId} host={nm.IsHost} seats=[{seats}] missing={snap.MissingParts} coat={b.transform.position} yaw={b.transform.eulerAngles.y:F0} leftHand={b.LeftHand.localPosition} heldOnHost=[{held}] raccoons={UnityEngine.Object.FindObjectsByType<NetworkedRaccoon>(FindObjectsSortMode.None).Length}{raccoon}{NpcSummary()}");
        }

        static void Log(string msg) => Debug.Log($"[DEV {Time.realtimeSinceStartup:F1}] {msg}");
    }
}
