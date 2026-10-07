using System.Collections.Generic;
using TrashPandas.Core.Debugging;
using TrashPandas.Core.Trenchcoat;
using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Input;
using TrashPandas.Runtime.Raccoon;
using UnityEngine;

namespace TrashPandas.Runtime.Trenchcoat
{
    /// <summary>Debug-mode orchestrator: one person drives every slot, can hop out as a raccoon and back.</summary>
    public sealed class TrenchcoatController : MonoBehaviour
    {
        public int PlayerCount = 3;
        public TrenchcoatBody Body;
        public RaccoonController RaccoonPrefab;
        public FollowCamera Camera;
        public float ReturnDistance = 1.6f;

        readonly MixerSettings _mixer = new MixerSettings();
        readonly Dictionary<int, SlotInput> _inputs = new Dictionary<int, SlotInput>();
        readonly DebugInputReader _reader = new DebugInputReader();
        SlotSystem _slots;
        DebugPossessionModel _possession;
        RaccoonController _raccoon;
        string _status = "";

        void Awake()
        {
            _slots = new SlotSystem(Mathf.Clamp(PlayerCount, SlotLayout.MinPlayers, SlotLayout.MaxPlayers));
            _possession = new DebugPossessionModel(_slots);
            Camera.Follow(Body.transform, 5f, 3f);
        }

        void Update()
        {
            if (_reader.CyclePressed) _possession.CycleNext();
            if (_reader.TogglePressed) Toggle();

            _inputs.Clear();
            if (_possession.ActiveIsOutside)
            {
                _raccoon.SetInput(_reader.Move(), _reader.MouseTurn(Time.deltaTime), _reader.JumpPressed, _reader.CrouchHeld);
                SetCursorLocked(true);
            }
            else
            {
                var parts = _slots.PartsOf(_slots.SlotOf(_possession.ActivePlayerId).Value);
                _inputs[_possession.ActivePlayerId] = _reader.ReadSlotInput(parts, Time.time, Time.deltaTime);
                SetCursorLocked(DebugInputReader.WantsLockedCursor(parts));
            }

            var partInputs = SlotInputRouter.Route(_slots, _inputs);
            Body.SetIntent(TrenchcoatIntentMixer.Mix(partInputs, _slots.ControlledParts, Time.time, _mixer));
        }

        void OnDisable() => SetCursorLocked(false);

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Toggle()
        {
            if (!_possession.ActiveIsOutside)
            {
                if (!_possession.LeaveCoat()) return;
                Vector3 spawn = Body.transform.position + Body.transform.right * 0.9f + Vector3.up * 0.2f;
                _raccoon = Instantiate(RaccoonPrefab, spawn, Body.transform.rotation);
                Camera.Follow(_raccoon.transform, 2.5f, 1.4f);
                _status = "";
                return;
            }

            float distance = Vector3.Distance(_raccoon.transform.position, Body.transform.position);
            if (distance > ReturnDistance) { _status = "Too far from the coat"; return; }
            if (!_possession.ReturnToCoat()) { _status = "No free slot"; return; }

            Destroy(_raccoon.gameObject);
            _raccoon = null;
            Camera.Follow(Body.transform, 5f, 3f);
            _status = "";
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 360, 260), GUI.skin.box);
            GUILayout.Label($"DEBUG — {_slots.SlotCount} players   [Tab] switch  [E] out/in  [Esc] free mouse");
            for (int i = 0; i < _slots.SlotCount; i++)
            {
                var occupant = _slots.OccupantOf(i);
                string who = occupant.HasValue ? $"P{occupant.Value}" : "— EMPTY —";
                string me = occupant == _possession.ActivePlayerId ? "  ◀ YOU" : "";
                GUILayout.Label($"Slot {i} [{_slots.PartsOf(i)}]: {who}{me}");
            }
            GUILayout.Label($"Missing: {_slots.MissingParts}");
            if (_possession.ActiveIsOutside) GUILayout.Label($"P{_possession.ActivePlayerId} is a loose raccoon");
            if (_status.Length > 0) GUILayout.Label(_status);
            GUILayout.EndArea();
        }
    }
}
