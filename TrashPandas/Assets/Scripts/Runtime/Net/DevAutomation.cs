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
            if (Bot != "hopflee" && Bot != "flee" && Bot != "hopgap" && Bot != "mouthflee" && Bot != "stashflee" && Bot != "sneakflee" && Bot != "lureflee") return null;
            var pd = TrashPandas.Runtime.Panic.PanicDirector.Instance;
            if (!pd || pd.Phase == TrashPandas.Runtime.Panic.RoundPhase.Results) return null;
            if (pd.Phase == TrashPandas.Runtime.Panic.RoundPhase.Infiltration && !pd.ExitsUnlocked) return null;
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

        /// <summary>Squad (v2) dev bots steer the active raccoon; null = use the keyboard.</summary>
        public static Vector2? SquadMove(TrashPandas.Runtime.Raccoon.RaccoonController r)
        {
            if (Bot == "walk" || Bot == "sneak" || Bot == "sprint") return new Vector2(Mathf.Sin(Time.time * 0.6f), Mathf.Cos(Time.time * 0.6f));
            if (Bot == "noisy")
            {
                // Behind the two guests chatting west of the garden (they face each other, not us): run around.
                if (!s_noisyPlaced && Time.timeSinceLevelLoad > 1f)
                {
                    s_noisyPlaced = true;
                    var cc = r.GetComponent<CharacterController>();
                    cc.enabled = false;
                    r.transform.position = new Vector3(-9.4f, 0.1f, -5.5f);
                    cc.enabled = true;
                }
                return new Vector2(Mathf.Sin(Time.time * 3f), Mathf.Cos(Time.time * 3f)) * 0.8f;
            }
            if (Bot == "fetch") return TrashPandas.Runtime.Squad.SquadBots.FetchMove(r);
            if (Bot == "push" || Bot == "climb")
            {
                // Line up south of the garden crate, then walk north into it (push) or jump at it (climb).
                var crate = GameObject.Find("Crate_Garden");
                if (!crate) return Vector2.zero;
                Vector3 lineUp = crate.transform.position + new Vector3(0f, 0f, -1.3f);
                Vector3 gd = lineUp - r.transform.position; gd.y = 0f;
                if (!s_linedUp && gd.magnitude > 0.2f) return TrashPandas.Runtime.Squad.SquadBots.Steer(r.transform.position, lineUp);
                s_linedUp = true;
                return r.Hanging ? Vector2.zero : new Vector2(0f, 1f);
            }
            if (Bot == "tunnel" || Bot == "canhide")
            {
                if (r.Crawling) { var td = r.TunnelDirection; return new Vector2(td.x, td.z); } // keep crawling forward
                if (r.InCan) return Vector2.zero;
                Vector3? goal = null;
                if (Bot == "tunnel" && !s_tunnelDone)
                    foreach (var pipe in UnityEngine.Object.FindObjectsByType<TrashPandas.Runtime.Squad.RaccoonPipe>(FindObjectsSortMode.None))
                        if (!goal.HasValue || Vector3.Distance(pipe.transform.position, r.transform.position) < Vector3.Distance(goal.Value, r.transform.position)) goal = pipe.transform.position + pipe.transform.forward * 0.5f;
                if (Bot == "canhide" && !s_canDone)
                    foreach (var can in TrashPandas.Runtime.Squad.Hideout.All)
                        if (can && (!goal.HasValue || Vector3.Distance(can.transform.position, r.transform.position) < Vector3.Distance(goal.Value, r.transform.position))) goal = can.transform.position + can.transform.forward * 0.8f;
                if (!goal.HasValue) return Vector2.zero;
                Vector3 gd = goal.Value - r.transform.position; gd.y = 0f;
                return gd.magnitude < 0.35f ? Vector2.zero : TrashPandas.Runtime.Squad.SquadBots.Steer(r.transform.position, goal.Value);
            }
            if (Bot == "flee" || Bot == "sneakflee") return FleeMove(r.transform.position) ?? Vector2.zero;
            if (Bot == "hideflee")
            {
                // On RUN: dive into the nearest bush and stay there.
                var pdh = TrashPandas.Runtime.Panic.PanicDirector.Instance;
                if (!pdh || pdh.Phase != TrashPandas.Runtime.Panic.RoundPhase.Panic) return Vector2.zero;
                if (r.InCan) return Vector2.zero;
                Vector3? best = null;
                foreach (var h in TrashPandas.Runtime.Squad.Hideout.All)
                    if (h && !h.Occupant && (!best.HasValue || Vector3.Distance(h.transform.position, r.transform.position) < Vector3.Distance(best.Value, r.transform.position)))
                        best = h.transform.position;
                if (!best.HasValue) return Vector2.zero;
                Vector3 d = best.Value - r.transform.position; d.y = 0f;
                return d.magnitude < 0.3f ? Vector2.zero : TrashPandas.Runtime.Squad.SquadBots.Steer(r.transform.position, best.Value);
            }
            if (Bot == "lureflee")
            {
                // Walk into the party until spotted, then run for an exit.
                var pdl = TrashPandas.Runtime.Panic.PanicDirector.Instance;
                if (pdl && pdl.Phase == TrashPandas.Runtime.Panic.RoundPhase.Panic) return FleeMove(r.transform.position) ?? Vector2.zero;
                // Walk up to tonight's nemesis (falls back to the middle of the party).
                var nem = TrashPandas.Runtime.Panic.NemesisDirector.Instance;
                Vector3 goal = nem && nem.Pawn ? nem.Pawn.transform.position : new Vector3(3f, 0f, 6.8f);
                return TrashPandas.Runtime.Squad.SquadBots.Steer(r.transform.position, goal);
            }
            if (Bot == "towerhost" || Bot == "towerclient") return TrashPandas.Runtime.Squad.SquadBots.OnlineTowerMove(r, Bot == "towerclient");
            if (Bot == "heavyonline") return TrashPandas.Runtime.Squad.SquadBots.OnlineHeavyMove(r);
            return null;
        }
        /// <summary>Dev: exits open from the start (escape tests without delivering the objectives).</summary>
        public static bool UnlockExits => Debug.isDebugBuild && Array.IndexOf(Args, "-unlockexits") >= 0;
        static bool s_peelTestDone, s_bonkTestDone;
        /// <summary>Dev tests: "-peeltest" drops a peel on the nemesis' route, "-bonktest" throws a pebble at her.</summary>
        public static void GadgetTests()
        {
            if (!Debug.isDebugBuild || Time.timeSinceLevelLoad < 4f) return;
            var gd = TrashPandas.Runtime.Squad.GadgetDirector.Instance;
            var nd = TrashPandas.Runtime.Panic.NemesisDirector.Instance;
            if (!gd || !nd || !nd.Pawn) return;
            if (!s_peelTestDone && Array.IndexOf(Args, "-peeltest") >= 0)
            {
                s_peelTestDone = true;
                Vector3 ahead = nd.Pawn.transform.position + nd.Pawn.Velocity.normalized * 0.3f;
                gd.DevPeelAt(new Vector3(ahead.x, nd.Pawn.transform.position.y, ahead.z));
            }
            if (!s_bonkTestDone && Array.IndexOf(Args, "-bonktest") >= 0)
            {
                s_bonkTestDone = true;
                Vector3 her = nd.Pawn.transform.position;
                Vector3 from = her + new Vector3(4f, 0.6f, 0f);
                gd.HostUse(0, TrashPandas.Core.Raccoons.Gadget.Pebble, from, new Vector3(-0.95f, -0.3f, 0f));
            }
        }
        public static bool TowerHop => Array.IndexOf(Args, "-hopoff") >= 0;
        public static bool SquadCrouch => Bot == "sneak";
        static float s_nextGadget;
        /// <summary>"gadgets": throw a pebble, then pop a smoke bomb.</summary>
        public static bool GadgetBot(TrashPandas.Runtime.Raccoon.RaccoonController me, out TrashPandas.Core.Raccoons.Gadget g, out Vector3 aim)
        {
            g = TrashPandas.Core.Raccoons.Gadget.Pebble; aim = Vector3.forward;
            if (Bot != "gadgets" || !me || Time.timeSinceLevelLoad < 3f || Time.time < s_nextGadget) return false;
            s_nextGadget = Time.time + 2f;
            float tl = Time.timeSinceLevelLoad;
            g = tl < 6f ? TrashPandas.Core.Raccoons.Gadget.Pebble : tl < 9f ? TrashPandas.Core.Raccoons.Gadget.Banana : TrashPandas.Core.Raccoons.Gadget.SmokeBomb;
            aim = new Vector3(0f, 0f, 1f);
            return true;
        }
        static bool s_tunnelDone, s_canDone, s_linedUp;
        static float s_climbJumpAt = -1f;
        /// <summary>"climb": jump once lined up at the crate; Space again while hanging.</summary>
        public static bool SquadJump(TrashPandas.Runtime.Raccoon.RaccoonController r)
        {
            if (Bot != "climb" || !s_linedUp) return false;
            if (r.Hanging && Time.time > s_climbJumpAt + 0.6f) { s_climbJumpAt = Time.time; return true; }
            var crate = GameObject.Find("Crate_Garden");
            if (s_climbJumpAt < 0f && crate && crate.transform.position.z - r.transform.position.z < 0.95f) { s_climbJumpAt = Time.time; return true; }
            return false;
        }
        /// <summary>Dev bots press E when they reach a pipe mouth / a trash can.</summary>
        public static bool SquadUse(TrashPandas.Runtime.Raccoon.RaccoonController r)
        {
            if (Bot == "tunnel" && !s_tunnelDone && !r.Crawling && TrashPandas.Runtime.Squad.RaccoonPipe.Near(r.transform.position)) { s_tunnelDone = true; return true; }
            if (Bot == "hideflee" && !r.InCan && TrashPandas.Runtime.Panic.PanicDirector.Instance && TrashPandas.Runtime.Panic.PanicDirector.Instance.Phase == TrashPandas.Runtime.Panic.RoundPhase.Panic && TrashPandas.Runtime.Squad.Hideout.Near(r.transform.position)) return true;
            if (Bot == "canhide" && !s_canDone && !r.InCan && TrashPandas.Runtime.Squad.Hideout.Near(r.transform.position)) { s_canDone = true; return true; }
            return false;
        }
        public static bool SquadRun => Bot == "sprint" || (Bot == "noisy" && Time.timeSinceLevelLoad > 4f) || (Bot == "towerhost" && Time.timeSinceLevelLoad > 24f);
        static bool s_noisyPlaced;
        /// <summary>Dev: -nointro, and the bots that test specific mechanics skip the intro.</summary>
        public static bool SkipIntro => Array.IndexOf(Args, "-nointro") >= 0 || Bot == "heavy" || Bot == "tower" || Bot == "flee" || Bot == "sneakflee" || Bot == "rescue" || Bot == "hideflee" || Bot == "push" || Bot == "climb" || Bot == "gadgets";
        /// <summary>Bots built around the garden start (heavy, tower) keep spawning there.</summary>
        public static bool SquadNearOrigin => Bot == "heavy" || Bot == "tower" || Bot == "flee" || Bot == "sneakflee" || Bot == "rescue" || Bot == "hideflee" || Bot == "push" || Bot == "climb" || Bot == "gadgets";
        public static bool SquadTap(TrashPandas.Runtime.Raccoon.RaccoonController r) =>
            (Bot == "fetch" && TrashPandas.Runtime.Squad.SquadBots.FetchTap(r)) || (Bot == "heavyonline" && TrashPandas.Runtime.Squad.SquadBots.OnlineHeavyTap(r));
        /// <summary>Bots that grab something specific (not what the highlight picked).</summary>
        public static int? SquadTapIndex => Bot == "heavyonline" ? TrashPandas.Runtime.Squad.SquadBots.GiftIndex : (int?)null;

        static int GiftIndexForTelemetry(TrashPandas.Runtime.Squad.CarryDirector c) => TrashPandas.Runtime.Squad.SquadBots.GiftIndex;

        static Vector3? NearestLoot(Vector3 from)
        {
            var ld = TrashPandas.Runtime.Loot.LootDirector.Instance;
            if (!ld) return null;
            Vector3? best = null;
            foreach (var item in ld.Items)
                if (item && item.State == TrashPandas.Runtime.Loot.LootState.Active && !item.Grabbable.IsHeld && item.transform.position.y < 0.3f
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
            if (pdx) panic += $" chasers[{pdx.ChaserStates}]";
            var nmx = TrashPandas.Runtime.Panic.NemesisDirector.Instance;
            if (nmx && nmx.Pawn) panic += $" nemesis={nmx.Kind}/{nmx.Pawn.Mood}@{nmx.Pawn.transform.position:F1} nemnet={nmx.IsSpawned}/{nmx.DebugNet}";
            if (pdx) panic += $" alert={pdx.Alert01:F2}";
            var sqx = TrashPandas.Runtime.Squad.SquadController.Instance;
            var cg = GameObject.Find("Crate_Garden");
            if (sqx && sqx.Active) panic += $" crate={(cg ? cg.transform.position.ToString("F2") : "-")} hanging={sqx.Active.Hanging} pushing={sqx.Active.Pushing}";
            if (sqx && sqx.Active) panic += $" crawling={sqx.Active.Crawling} inCan={(bool)sqx.Active.InCan} pos={sqx.Active.transform.position:F1}";
            if (sqx && sqx.Active) panic += $" hidden={TrashPandas.Runtime.Squad.HidingSpot.Hides(sqx.Active)} spot={TrashPandas.Runtime.Squad.HidingSpot.SpotOf(sqx.Active)}";
            if (pdx) panic += $" P0loot=${pdx.Snapshot.LootOf(0)} clean={pdx.Snapshot.CleanExit} openExits={string.Join(",", pdx.OpenExitPositions)}";
            var mine = NetworkedRaccoon.LocalOwned;
            if (ld && mine)
            {
                var it = TrashPandas.Runtime.Squad.CarryDirector.Instance ? TrashPandas.Runtime.Squad.CarryDirector.Instance.ItemOf(mine.Controller.PlayerId) : null;
                if (it)
                {
                    var col = it.GetComponent<Collider>();
                    panic += $" mouthGap={Vector3.Distance(it.transform.position, mine.transform.position + mine.transform.forward * 0.28f + Vector3.up * 0.32f):F2} mouthCollider={(col && col.enabled)}";
                }
            }
            var cdx = TrashPandas.Runtime.Squad.CarryDirector.Instance;
            if (cdx) panic += $" carry1={cdx.Snapshot.ItemOf(1)}";
            if (cdx) foreach (var g in cdx.Items) if (g && g.name == "Loot_GiantGift") panic += $" gift={g.transform.position:F1} lifted0={cdx.Snapshot.Lifted(0)} lifted1={cdx.Snapshot.Lifted(1)}";
            var mineR = NetworkedRaccoon.LocalOwned;
            if (mineR) panic += $" myPid={mineR.Controller.PlayerId} myMount={(mineR.Controller.Mount ? mineR.Controller.Mount.PlayerId : -1)} ridersOnMe={mineR.Controller.RidersAbove} myPos={mineR.transform.position:F1}";
            if (cdx && GiftIndexForTelemetry(cdx) is int gi && gi >= 0)
            {
                var gift = cdx.Items[gi];
                var gcol = gift.GetComponent<Collider>();
                panic += $" giftCollider={(gcol && gcol.enabled)}";
            }
            if (ld) { var ls = ld.Snapshot; panic += $" carry0={(TrashPandas.Runtime.Squad.CarryDirector.Instance ? TrashPandas.Runtime.Squad.CarryDirector.Instance.Snapshot.ItemOf(0) : -1)} pocket=${ls.Total} objectives={ls.ObjectivesPicked:X2}/{ls.ObjectivesDone:X2} clock={ls.SecondsLeft:F0}"; }
            int hearing = 0;
            foreach (var b in d.Brains) if (b.HeardNoise.HasValue && Time.time < b.HeardUntil) hearing++;
            panic += $" hearing={hearing}";
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
