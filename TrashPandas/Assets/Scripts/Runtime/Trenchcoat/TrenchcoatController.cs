using System.Collections.Generic;
using TrashPandas.Core.Debugging;
using TrashPandas.Core.Trenchcoat;
using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Grabbing;
using TrashPandas.Runtime.Input;
using TrashPandas.Runtime.Raccoon;
using UnityEngine;
using TrashPandas.Runtime.Ui;

namespace TrashPandas.Runtime.Trenchcoat
{
    /// <summary>
    /// Debug-mode orchestrator: one person drives every slot, can hop out as a raccoon and back, and can
    /// record ghost inputs so recorded roles keep playing while they control another one.
    /// </summary>
    public sealed class TrenchcoatController : MonoBehaviour
    {
        public int PlayerCount = 3;
        public TrenchcoatBody Body;
        public RaccoonController RaccoonPrefab;
        public PlayerCameraRig CameraRig;
        public float ReturnDistance = 1.6f;
        public float CoatCameraRadius = 5.5f;
        public float CoatLookHeight = 1.4f;
        public float RaccoonCameraRadius = 3f;
        public float RaccoonLookHeight = 0.4f;
        [Tooltip("Closer, over-the-shoulder camera when you control the arms (and not the legs).")]
        public float ArmsCameraRadius = 3.2f;
        public float ArmsLookHeight = 1.7f;

        readonly MixerSettings _mixer = new MixerSettings();
        readonly Dictionary<int, SlotInput> _inputs = new Dictionary<int, SlotInput>();
        readonly Dictionary<int, InputGhost> _ghosts = new Dictionary<int, InputGhost>();
        readonly DebugInputReader _reader = new DebugInputReader();
        SlotSystem _slots;
        DebugPossessionModel _possession;
        RaccoonController _raccoon;
        int? _recording;
        string _status = "";
        bool? _armsCamera;
        HandGrabber _grabber;
        bool _showHelp = true;
        /// <summary>Debug: drive the whole body at once (default) instead of one seat at a time.</summary>
        public bool SoloMode = true;
        readonly TrashPandas.Runtime.Npc.EventParticipation _events = new TrashPandas.Runtime.Npc.EventParticipation();
        bool _eventCamera;

        void UpdateEventCamera(bool engaged)
        {
            var d = TrashPandas.Runtime.Npc.SocialEventDirector.Instance;
            bool talking = d && (engaged || d.Phase == TrashPandas.Runtime.Npc.EventPhase.Resolved || d.Phase == TrashPandas.Runtime.Npc.EventPhase.Talking) && !_possession.ActiveIsOutside;
            if (_burst) return; // after RUN the camera belongs to your raccoon
            if (talking == _eventCamera) return;
            _eventCamera = talking;
            if (talking) CameraRig.BeginConversation(Body.transform, d.SpeakerTransform);
            else if (_possession.ActiveIsOutside && _raccoon)
            {
                // Hopped out mid-conversation: the camera follows your raccoon, not the coat.
                CameraRig.EndConversation(restore: false);
                CameraRig.SetTarget(_raccoon.transform, RaccoonCameraRadius, RaccoonLookHeight);
            }
            else { CameraRig.EndConversation(); _armsCamera = null; UpdateCoatCamera(); }
        }
        readonly List<RaccoonController> _botRaccoons = new List<RaccoonController>();

        public static TrenchcoatController Instance { get; private set; }
        /// <summary>The player the person at the keyboard controls (offline debug).</summary>
        public int LocalPlayerId => _possession != null ? _possession.ActivePlayerId : 0;
        public int PlayerCountInRound => _slots != null ? _slots.SlotCount : 0;
        public BodyPart ControlledParts => _slots != null ? _slots.ControlledParts : BodyPart.None;
        bool _burst;

        /// <summary>RUN!: everyone still inside pops out as a raccoon, flung outward.</summary>
        public void BurstAll()
        {
            if (_burst) return;
            _burst = true;
            CameraRig.EndConversation(restore: false); // RUN! interrupts any conversation close-up
            _eventCamera = false;
            StopRecording(Time.time);
            Vector3 center = Body.transform.position;
            for (int player = 0; player < _slots.SlotCount; player++)
            {
                if (!_slots.SlotOf(player).HasValue) continue;
                _slots.Leave(player);
                float angle = player * Mathf.PI * 2f / _slots.SlotCount;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var raccoon = Instantiate(RaccoonPrefab, center + dir * 0.8f + Vector3.up * 0.5f, Quaternion.LookRotation(dir));
                raccoon.PlayerId = player;
                raccoon.ApplyHit(dir * 5f, 0.6f);
                if (player == _possession.ActivePlayerId)
                {
                    _raccoon = raccoon;
                    CameraRig.SetTarget(raccoon.transform, RaccoonCameraRadius, RaccoonLookHeight);
                }
                else _botRaccoons.Add(raccoon);
            }
            Body.Explode();
            // If you were already outside, make sure the camera is on your raccoon (not the vanished coat).
            if (_raccoon && _possession.ActiveIsOutside) CameraRig.SetTarget(_raccoon.transform, RaccoonCameraRadius, RaccoonLookHeight);
        }

        /// <summary>The local raccoon was removed (escaped or caught): watch the garden from above.</summary>
        public void ShowOverview(Transform overview)
        {
            _raccoon = null;
            if (overview) CameraRig.SetTarget(overview, 16f, 0f);
        }
        bool _botHopped;

        void Awake()
        {
            Instance = this;
            // This is the single-person debug mode; online play is driven by OnlinePlayerController.
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm && nm.IsListening) { enabled = false; return; }
            Time.fixedDeltaTime = 1f / 60f; // physics at 60 Hz: smoother follow on common displays
            _slots = new SlotSystem(Mathf.Clamp(PlayerCount, SlotLayout.MinPlayers, SlotLayout.MaxPlayers));
            _possession = new DebugPossessionModel(_slots);
            _grabber = Body.GetComponent<HandGrabber>();
            UpdateCoatCamera();
        }

        void Start()
        {
            // Offline, nothing gets spawned: undo NetworkRigidbody's "kinematic until spawned" so physics runs.
            // (In Start, not Awake: NetworkRigidbody sets itself up in its own Awake.)
            foreach (var nrb in FindObjectsByType<Unity.Netcode.Components.NetworkRigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                nrb.SetIsKinematic(false);
        }

        /// <summary>Over-the-shoulder when the active role aims hands; wide when it walks.</summary>
        /// <summary>Dev automation: walk to the nearest wallet, grab it, hold it against the chest (Q).</summary>
        readonly System.Collections.Generic.HashSet<TrashPandas.Runtime.Loot.LootItem> _botSkip = new System.Collections.Generic.HashSet<TrashPandas.Runtime.Loot.LootItem>();
        Vector3 _botLastPos;
        UnityEngine.AI.NavMeshPath _botPath;
        float _botStuck;

        void BotStash(ref SlotInput live)
        {
            var grabber = Body.GetComponent<TrashPandas.Runtime.Grabbing.HandGrabber>();
            var held = grabber ? (grabber.HeldLeft ? grabber.HeldLeft : grabber.HeldRight) : null;
            bool holding = held && held.GetComponent<TrashPandas.Runtime.Loot.LootItem>();
            live.GrabOne = holding;
            if (held && !holding) { live.GrabOne = false; return; } // grabbed the wrong thing: let go
            if (holding)
            {
                live.Move = Vector2.zero;
                live.AimPoint = Body.ChestWorld + Body.transform.forward * 0.15f;
                live.HasAimPoint = true;
                return;
            }
            TrashPandas.Runtime.Loot.LootItem best = null;
            float bestD = float.MaxValue;
            foreach (var item in FindObjectsByType<TrashPandas.Runtime.Loot.LootItem>(FindObjectsSortMode.None))
            {
                if (item.State != TrashPandas.Runtime.Loot.LootState.Active || _botSkip.Contains(item)) continue;
                float d = Vector3.Distance(item.transform.position, Body.transform.position);
                if (d < bestD) { bestD = d; best = item; }
            }
            if (!best) return;
            Vector3 to = best.transform.position - Body.transform.position;
            to.y = 0f;
            // Stuck against a table with the item out of reach: try another one.
            if (Body.transform.position.sqrMagnitude > 0.01f && (Body.transform.position - _botLastPos).magnitude < 0.02f && to.magnitude > 1.2f)
            { if ((_botStuck += Time.deltaTime) > 1f) { _botSkip.Add(best); _botStuck = 0f; } }
            else _botStuck = 0f;
            _botLastPos = Body.transform.position;
            if (Time.frameCount % 60 == 0) Debug.Log($"[BOT] target={best.name} at {best.transform.position} dist={to.magnitude:F2} skipped={_botSkip.Count} grab={to.magnitude < 1.4f}");
            // Walk the navmesh to a free spot beside the item, then reach for it.
            Vector3 dir = to;
            if (to.magnitude > 0.85f && UnityEngine.AI.NavMesh.SamplePosition(new Vector3(best.transform.position.x, 0f, best.transform.position.z) - to.normalized * 0.75f, out var near, 1.2f, UnityEngine.AI.NavMesh.AllAreas)
                && UnityEngine.AI.NavMesh.SamplePosition(Body.transform.position, out var me, 1.5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                _botPath ??= new UnityEngine.AI.NavMeshPath();
                if (UnityEngine.AI.NavMesh.CalculatePath(me.position, near.position, UnityEngine.AI.NavMesh.AllAreas, _botPath) && _botPath.corners.Length > 1)
                {
                    var corner = _botPath.corners[1];
                    dir = corner - Body.transform.position;
                    dir.y = 0f;
                    if (dir.magnitude < 0.3f && _botPath.corners.Length > 2) { dir = _botPath.corners[2] - Body.transform.position; dir.y = 0f; }
                }
            }
            bool arrived = to.magnitude <= 0.85f;
            live.Move = arrived ? Vector2.zero : new Vector2(dir.x, dir.z).normalized;
            live.GrabOne = to.magnitude < 1.4f;
            live.AimPoint = best.transform.position;
            live.HasAimPoint = true;
            live.PreferLeftHand = Vector3.Dot(to, Body.transform.right) < 0f;
        }

        void UpdateCoatCamera()
        {
            if (_possession.ActiveIsOutside) return;
            var parts = _slots.PartsOf(_slots.SlotOf(_possession.ActivePlayerId).Value);
            bool arms = !SoloMode && (parts & BodyPart.Arms) != 0 && (parts & BodyPart.Legs) == 0;
            if (_armsCamera == arms) return;
            _armsCamera = arms;
            if (arms) CameraRig.SetTarget(Body.transform, ArmsCameraRadius, ArmsLookHeight);
            else CameraRig.SetTarget(Body.transform, CoatCameraRadius, CoatLookHeight);
        }

        void Update()
        {
            float now = Time.time;
            // Social event response window: number keys answer instead of switching seats.
            bool engaged = _events.Tick(_reader, _reader.Move(), offlinePlayer: 0);
            UpdateEventCamera(engaged);
            int selected = engaged ? -1 : _reader.SelectPressed();
            if (selected >= 0 || _reader.CyclePressed) StopRecording(now);
            if (selected >= 0) _possession.TrySelect(selected);
            if (_reader.CyclePressed) _possession.CycleNext();
            if (_reader.TogglePressed) Toggle(now);
            if ((Net.DevAutomation.Bot == "hop" || Net.DevAutomation.Bot == "hopflee" || Net.DevAutomation.Bot == "hopgap") && !_botHopped && Time.timeSinceLevelLoad > Net.DevAutomation.HopAt) { _botHopped = true; Toggle(now); } // dev automation
            if (_reader.RecordPressed) ToggleRecording(now);
            UpdateCoatCamera();
            if (UnityEngine.InputSystem.Keyboard.current?.f1Key.wasPressedThisFrame == true) _showHelp = !_showHelp;
            if (UnityEngine.InputSystem.Keyboard.current?.f2Key.wasPressedThisFrame == true)
            {
                SoloMode = !SoloMode;
                _armsCamera = null;
                UpdateCoatCamera();
                _status = SoloMode ? "SOLO: you control the whole body" : "ROLES: one seat at a time (1-5 / Tab)";
            }
            if (_reader.ClearGhostsPressed) { _ghosts.Clear(); _recording = null; _status = "Ghosts cleared"; }

            _inputs.Clear();
            foreach (var pair in _ghosts)
                if (pair.Value.HasRecording && pair.Key != _recording) _inputs[pair.Key] = pair.Value.Sample(now);

            if (_possession.ActiveIsOutside)
            {
                if (!_raccoon) { Body.SetIntent(default); return; }
                bool dashBot = Net.DevAutomation.Bot == "hop" || Net.DevAutomation.Bot == "hopflee";
                var raccoonMove = Net.DevAutomation.FleeMove(_raccoon.transform.position)
                    ?? (dashBot ? new Vector2(0f, 1f) : _reader.CameraRelativeMove(CameraRig)); // dev bots
                _raccoon.SetInput(raccoonMove, _reader.JumpPressed, _reader.JumpHeld, _reader.CrouchHeld || Net.DevAutomation.FleeCrouchAt(_raccoon.transform.position));
            }
            else
            {
                var live = TrashPandas.Core.Events.ConversationInput.Filter(_reader.ReadSlotInput(CameraRig, Body, now), engaged);
                if (Net.DevAutomation.Bot == "walk") live.Move = new Vector2(0f, 1f); // dev automation
                if (Net.DevAutomation.Bot == "stash") BotStash(ref live);
                if (Net.DevAutomation.Bot == "walkgrab") { live.Move = Body.transform.position.z < 2.6f ? new Vector2(0f, 1f) : Vector2.zero; live.GrabOne = true; }
                if (Net.DevAutomation.Bot == "tocat") live.Move = TowardCat();
                _inputs[_possession.ActivePlayerId] = live; // you always override your own ghost
                if (SoloMode)
                {
                    // Solo: one person drives every seat that's still inside (ghosts still play their own seats).
                    for (int p = 0; p < _slots.SlotCount; p++)
                        if (_slots.SlotOf(p).HasValue && !(_ghosts.TryGetValue(p, out var g) && g.HasRecording && p != _possession.ActivePlayerId))
                            _inputs[p] = live;
                }
                if (_recording == _possession.ActivePlayerId) _ghosts[_possession.ActivePlayerId].Record(now, live);
            }

            var partInputs = SlotInputRouter.Route(_slots, _inputs);
            Body.SetIntent(TrenchcoatIntentMixer.Mix(partInputs, _slots.ControlledParts, now, _mixer));
        }

        /// <summary>Dev automation: walk the coat toward the cat, stopping a step away.</summary>
        Vector2 TowardCat()
        {
            var d = TrashPandas.Runtime.Npc.SuspicionDirector.Instance;
            if (!d) return Vector2.zero;
            foreach (var b in d.Brains)
                if (b.Pawn && b.Pawn.Kind == TrashPandas.Runtime.Npc.NpcKind.Cat)
                {
                    Vector3 to = b.Pawn.transform.position - Body.transform.position;
                    to.y = 0f;
                    return to.magnitude < 1.2f ? Vector2.zero : new Vector2(to.x, to.z).normalized;
                }
            return Vector2.zero;
        }

        void ToggleRecording(float now)
        {
            if (_recording.HasValue) { StopRecording(now); return; }
            if (_possession.ActiveIsOutside) return;
            var ghost = new InputGhost();
            _ghosts[_possession.ActivePlayerId] = ghost;
            _recording = _possession.ActivePlayerId;
            _status = $"● REC P{_recording} — press R to stop, then switch roles";
        }

        void StopRecording(float now)
        {
            if (!_recording.HasValue) return;
            var ghost = _ghosts[_recording.Value];
            if (ghost.HasRecording)
            {
                ghost.StartPlayback(now);
                _status = $"Ghost P{_recording} replaying ({ghost.Duration:F1}s loop)";
            }
            else
            {
                _ghosts.Remove(_recording.Value);
                _status = "";
            }
            _recording = null;
        }

        void Toggle(float now)
        {
            if (!_possession.ActiveIsOutside)
            {
                StopRecording(now);
                if (_burst || !_possession.LeaveCoat()) return;
                Vector3 spawn = Body.transform.position + Body.transform.right * 0.9f + Vector3.up * 0.2f;
                _raccoon = Instantiate(RaccoonPrefab, spawn, Body.transform.rotation);
                _raccoon.PlayerId = _possession.ActivePlayerId;
                CameraRig.SetTarget(_raccoon.transform, RaccoonCameraRadius, RaccoonLookHeight);
                _status = "";
                return;
            }

            if (_burst || !_raccoon) return; // no getting back in once the coat has burst
            float distance = Vector3.Distance(_raccoon.transform.position, Body.transform.position);
            if (distance > ReturnDistance) { _status = "Too far from the coat"; return; }
            if (!_possession.ReturnToCoat()) { _status = "No free slot"; return; }

            Destroy(_raccoon.gameObject);
            _raccoon = null;
            _armsCamera = null;
            UpdateCoatCamera();
            _status = "";
        }

        void DrawCrosshair()
        {
            UiScale.Apply();
            if (_possession.ActiveIsOutside) return;
            var parts = _slots.PartsOf(_slots.SlotOf(_possession.ActivePlayerId).Value);
            if ((parts & BodyPart.Arms) == 0 && !(SoloMode && (_slots.ControlledParts & BodyPart.Arms) != 0)) return;

            bool holding = _grabber && (_grabber.HeldLeft || _grabber.HeldRight || _grabber.HeldBoth);
            Color color = holding ? new Color(1f, 0.85f, 0.2f)
                        : _reader.AssistTarget ? new Color(0.3f, 1f, 0.4f)
                        : _reader.AimInReach ? Color.white
                        : new Color(1f, 1f, 1f, 0.35f);
            float size = _reader.AssistTarget ? 14f : 8f;
            var c = new Vector2(UiScale.Width * 0.5f, UiScale.Height * 0.5f);
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size), Texture2D.whiteTexture);
            GUI.color = old;
            if (_reader.AssistTarget && !holding)
            {
                string label = _reader.AssistTarget.RequiresBothHands ? "Right click (both hands)" : "Left click";
                GUI.Label(new Rect(c.x + 12, c.y - 9, 220, 18), $"{_reader.AssistTarget.name} — {label}");
            }
        }

        void OnGUI()
        {
            UiScale.Apply();
            if (CameraRig.InConversation) return; // the conversation owns the screen
            DrawCrosshair();
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            var lines = new List<string>();
            var slotsLine = "";
            for (int i = 0; i < _slots.SlotCount; i++)
            {
                var occupant = _slots.OccupantOf(i);
                string mark = !occupant.HasValue ? "·empty"
                            : occupant == _possession.ActivePlayerId ? "◀YOU"
                            : _ghosts.ContainsKey(occupant.Value) ? "[ghost]" : "";
                if (occupant.HasValue && occupant == _recording) mark += "●REC";
                slotsLine += $"[{i + 1}] {_slots.PartsOf(i)} {mark}   ";
            }
            lines.Add(slotsLine);
            if (_showHelp)
            {
                if (_possession.ActiveIsOutside)
                    lines.Add("RACCOON  Mouse camera · WASD run · Space jump (hold=higher) · Ctrl crouch · walk into red curtain to climb · E near coat");
                else
                    lines.Add((SoloMode ? "[SOLO — F2: roles]  " : "[ROLES — F2: solo]  ") + DebugInputReader.HintFor(SoloMode ? _slots.ControlledParts : _slots.PartsOf(_slots.SlotOf(_possession.ActivePlayerId).Value)));
                lines.Add($"Tab/1-5 switch · E out/in · R record ghost · Backspace clear ghosts · [ ] camera speed ({CameraRig.Sensitivity:F2}) · ←→ orbit · Esc free mouse · F1 hide help");
            }
            if (_status.Length > 0) lines.Add(_status);

            float h = lines.Count * 18f + 8f;
            GUI.Box(new Rect(8, UiScale.Height - h - 8, UiScale.Width - 16, h), GUIContent.none);
            for (int i = 0; i < lines.Count; i++)
                GUI.Label(new Rect(14, UiScale.Height - h - 4 + i * 18f, UiScale.Width - 28, 18), lines[i], style);
        }
    }
}
