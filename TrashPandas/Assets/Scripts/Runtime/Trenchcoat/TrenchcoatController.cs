using System.Collections.Generic;
using TrashPandas.Core.Debugging;
using TrashPandas.Core.Trenchcoat;
using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Grabbing;
using TrashPandas.Runtime.Input;
using TrashPandas.Runtime.Raccoon;
using UnityEngine;

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

        void Awake()
        {
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
        void UpdateCoatCamera()
        {
            if (_possession.ActiveIsOutside) return;
            var parts = _slots.PartsOf(_slots.SlotOf(_possession.ActivePlayerId).Value);
            bool arms = (parts & BodyPart.Arms) != 0 && (parts & BodyPart.Legs) == 0;
            if (_armsCamera == arms) return;
            _armsCamera = arms;
            if (arms) CameraRig.SetTarget(Body.transform, ArmsCameraRadius, ArmsLookHeight);
            else CameraRig.SetTarget(Body.transform, CoatCameraRadius, CoatLookHeight);
        }

        void Update()
        {
            float now = Time.time;
            int selected = _reader.SelectPressed();
            if (selected >= 0 || _reader.CyclePressed) StopRecording(now);
            if (selected >= 0) _possession.TrySelect(selected);
            if (_reader.CyclePressed) _possession.CycleNext();
            if (_reader.TogglePressed) Toggle(now);
            if (_reader.RecordPressed) ToggleRecording(now);
            UpdateCoatCamera();
            if (UnityEngine.InputSystem.Keyboard.current?.f1Key.wasPressedThisFrame == true) _showHelp = !_showHelp;
            if (_reader.ClearGhostsPressed) { _ghosts.Clear(); _recording = null; _status = "Ghosts cleared"; }

            _inputs.Clear();
            foreach (var pair in _ghosts)
                if (pair.Value.HasRecording && pair.Key != _recording) _inputs[pair.Key] = pair.Value.Sample(now);

            if (_possession.ActiveIsOutside)
            {
                _raccoon.SetInput(_reader.CameraRelativeMove(CameraRig), _reader.JumpPressed, _reader.JumpHeld, _reader.CrouchHeld);
            }
            else
            {
                var live = _reader.ReadSlotInput(CameraRig, Body, now);
                if (Net.DevAutomation.Bot == "walk") live.Move = new Vector2(0f, 1f); // dev automation
                _inputs[_possession.ActivePlayerId] = live; // you always override your own ghost
                if (_recording == _possession.ActivePlayerId) _ghosts[_possession.ActivePlayerId].Record(now, live);
            }

            var partInputs = SlotInputRouter.Route(_slots, _inputs);
            Body.SetIntent(TrenchcoatIntentMixer.Mix(partInputs, _slots.ControlledParts, now, _mixer));
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
                if (!_possession.LeaveCoat()) return;
                Vector3 spawn = Body.transform.position + Body.transform.right * 0.9f + Vector3.up * 0.2f;
                _raccoon = Instantiate(RaccoonPrefab, spawn, Body.transform.rotation);
                CameraRig.SetTarget(_raccoon.transform, RaccoonCameraRadius, RaccoonLookHeight);
                _status = "";
                return;
            }

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
            if (_possession.ActiveIsOutside) return;
            var parts = _slots.PartsOf(_slots.SlotOf(_possession.ActivePlayerId).Value);
            if ((parts & BodyPart.Arms) == 0) return;

            bool holding = _grabber && (_grabber.HeldLeft || _grabber.HeldRight || _grabber.HeldBoth);
            Color color = holding ? new Color(1f, 0.85f, 0.2f)
                        : _reader.AssistTarget ? new Color(0.3f, 1f, 0.4f)
                        : _reader.AimInReach ? Color.white
                        : new Color(1f, 1f, 1f, 0.35f);
            float size = _reader.AssistTarget ? 14f : 8f;
            var c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
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
            DrawCrosshair();
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            var lines = new List<string>();
            var slotsLine = "";
            for (int i = 0; i < _slots.SlotCount; i++)
            {
                var occupant = _slots.OccupantOf(i);
                string mark = !occupant.HasValue ? "·empty"
                            : occupant == _possession.ActivePlayerId ? "◀YOU"
                            : _ghosts.ContainsKey(occupant.Value) ? "👻" : "";
                if (occupant.HasValue && occupant == _recording) mark += "●REC";
                slotsLine += $"[{i + 1}] {_slots.PartsOf(i)} {mark}   ";
            }
            lines.Add(slotsLine);
            if (_showHelp)
            {
                if (_possession.ActiveIsOutside)
                    lines.Add("RACCOON  Mouse camera · WASD run · Space jump (hold=higher) · Ctrl crouch · walk into red curtain to climb · E near coat");
                else
                    lines.Add(DebugInputReader.HintFor(_slots.PartsOf(_slots.SlotOf(_possession.ActivePlayerId).Value)));
                lines.Add($"Tab/1-5 switch · E out/in · R record ghost · Backspace clear ghosts · [ ] camera speed ({CameraRig.Sensitivity:F2}) · ←→ orbit · Esc free mouse · F1 hide help");
            }
            if (_status.Length > 0) lines.Add(_status);

            float h = lines.Count * 18f + 8f;
            GUI.Box(new Rect(8, Screen.height - h - 8, Screen.width - 16, h), GUIContent.none);
            for (int i = 0; i < lines.Count; i++)
                GUI.Label(new Rect(14, Screen.height - h - 4 + i * 18f, Screen.width - 28, 18), lines[i], style);
        }
    }
}
