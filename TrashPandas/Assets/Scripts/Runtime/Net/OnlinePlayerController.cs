using TrashPandas.Core.Trenchcoat;
using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Input;
using Unity.Netcode;
using UnityEngine;
using TrashPandas.Runtime.Ui;

namespace TrashPandas.Runtime.Net
{
    /// <summary>
    /// The local player's side of online play: reads keyboard/mouse with this player's own camera, sends the
    /// slot input to the host, and draws the HUD for this player's role.
    /// </summary>
    public sealed class OnlinePlayerController : MonoBehaviour
    {
        public PlayerCameraRig CameraRig;
        public float SendRate = 30f;
        public float CoatCameraRadius = 5.5f, CoatLookHeight = 1.4f, ArmsCameraRadius = 3.2f, ArmsLookHeight = 1.7f;
        public float RaccoonCameraRadius = 3f, RaccoonLookHeight = 0.4f;

        readonly DebugInputReader _reader = new DebugInputReader();
        uint _sequence;
        float _nextSend;
        bool? _armsCamera;
        NetworkedRaccoon _cameraOnRaccoon;
        float _botHopAt = 8f;
        bool _spectating;
        float _caughtAt = -1f;
        bool _eventCamera;
        readonly TrashPandas.Runtime.Npc.EventParticipation _events = new TrashPandas.Runtime.Npc.EventParticipation();
        public bool IsSpectating => _spectating;
        NetworkedTrenchcoat _coat;

        static bool Online => NetworkManager.Singleton && NetworkManager.Singleton.IsListening;

        void Awake()
        {
            if (!Online) enabled = false; // offline, the debug controller runs the scene
        }

        void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current?.f10Key.wasPressedThisFrame == true && SessionHost.Instance)
            {
                _ = SessionHost.Instance.LeaveAsync();
                return;
            }
            if (!_coat) { _coat = NetworkedTrenchcoat.Instance; if (!_coat) return; _armsCamera = null; }
            var nm = NetworkManager.Singleton;
            float now = nm.ServerTime.TimeAsFloat;
            var snapshot = _coat.Slots;
            int? slot = snapshot.SlotOfClient(nm.LocalClientId);

            var raccoon = NetworkedRaccoon.LocalOwned;
            // Caught: after a beat on your own dizzy raccoon, watch the chase from above.
            if (raccoon && raccoon.Controller.Frozen)
            {
                if (_caughtAt < 0f) _caughtAt = Time.time;
                var pd = TrashPandas.Runtime.Panic.PanicDirector.Instance;
                if (Time.time - _caughtAt > 1.5f && pd && pd.Overview && !_spectating) { _spectating = true; CameraRig.SetTarget(pd.Overview, 16f, 0f); }
                return;
            }
            if ((DevAutomation.Bot == "hop" || DevAutomation.Bot == "hopflee" || DevAutomation.Bot == "hopgap") && slot.HasValue && Time.realtimeSinceStartup > _botHopAt) { _botHopAt = float.MaxValue; _coat.RequestLeaveRpc(); }
            if (_reader.TogglePressed)
            {
                if (slot.HasValue) _coat.RequestLeaveRpc();
                else if (raccoon) _coat.RequestReturnRpc();
            }

            if (raccoon && !slot.HasValue)
            {
                if (_cameraOnRaccoon != raccoon)
                {
                    _cameraOnRaccoon = raccoon;
                    CameraRig.SetTarget(raccoon.transform, RaccoonCameraRadius, RaccoonLookHeight);
                }
                var move = DevAutomation.FleeMove(raccoon.transform.position)
                    ?? (DevAutomation.Bot == "hopflee" ? new Vector2(0f, 1f) : _reader.CameraRelativeMove(CameraRig)); // dev bots
                raccoon.Controller.SetInput(move, _reader.JumpPressed, _reader.JumpHeld, _reader.CrouchHeld || DevAutomation.FleeCrouchAt(raccoon.transform.position));
                return;
            }
            if (_cameraOnRaccoon) { _cameraOnRaccoon = null; _armsCamera = null; }

            // Escaped or caught during the panic: no seat, no raccoon — watch the others from above.
            var panic = TrashPandas.Runtime.Panic.PanicDirector.Instance;
            if (!slot.HasValue && panic && panic.Phase != TrashPandas.Runtime.Panic.RoundPhase.Infiltration && panic.Overview)
            {
                if (!_spectating) { _spectating = true; CameraRig.SetTarget(panic.Overview, 16f, 0f); }
                return;
            }

            bool engaged = _events.Tick(_reader, _reader.Move(), offlinePlayer: 0);
            if (slot.HasValue)
            {
                var parts = snapshot.PartsOf(slot.Value);
                var ed = TrashPandas.Runtime.Npc.SocialEventDirector.Instance;
                bool talking = ed && (engaged || ed.Phase == TrashPandas.Runtime.Npc.EventPhase.Resolved);
                if (talking != _eventCamera)
                {
                    _eventCamera = talking;
                    if (talking) CameraRig.BeginConversation(_coat.Body.transform, ed.SpeakerTransform);
                    else { CameraRig.EndConversation(); _armsCamera = null; }
                }
                if (!talking) UpdateCamera(parts);
                var input = TrashPandas.Core.Events.ConversationInput.Filter(_reader.ReadSlotInput(CameraRig, _coat.Body, now), engaged);
                ApplyBot(ref input);
                if (Time.unscaledTime >= _nextSend)
                {
                    _nextSend = Time.unscaledTime + 1f / SendRate;
                    _coat.SubmitInputRpc(new NetSlotInput { Sequence = ++_sequence, Input = input });
                }
            }
        }

        /// <summary>Development automation (-bot): fake input so builds can test themselves.</summary>
        void ApplyBot(ref SlotInput input)
        {
            switch (DevAutomation.Bot)
            {
                case "walk":
                    input.Move = new Vector2(0f, 1f);
                    break;
                case "reach":
                    input.GrabOne = true;
                    input.HasAimPoint = true;
                    input.AimPoint = _coat.Body.transform.TransformPoint(new Vector3(-0.6f, 2.2f, 0.8f));
                    input.PreferLeftHand = true;
                    break;
            }
        }

        void UpdateCamera(BodyPart parts)
        {
            bool arms = (parts & BodyPart.Arms) != 0 && (parts & BodyPart.Legs) == 0;
            if (_armsCamera == arms) return;
            _armsCamera = arms;
            CameraRig.SetTarget(_coat.Body.transform, arms ? ArmsCameraRadius : CoatCameraRadius, arms ? ArmsLookHeight : CoatLookHeight);
        }

        void OnGUI()
        {
            UiScale.Apply();
            if (CameraRig.InConversation) return; // the conversation owns the screen
            if (!_coat) return;
            var nm = NetworkManager.Singleton;
            var snapshot = _coat.Slots;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            string seats = "";
            for (int i = 0; i < snapshot.SlotCount; i++)
            {
                var client = snapshot.OccupantClient(i);
                string who = !client.HasValue ? "·empty" : client == nm.LocalClientId ? "◀YOU" : $"P{client}";
                seats += $"[{snapshot.PartsOf(i)}] {who}   ";
            }
            int? mine = snapshot.SlotOfClient(nm.LocalClientId);
            string hint = mine.HasValue ? DebugInputReader.HintFor(snapshot.PartsOf(mine.Value)) + "   E: hop out"
                        : NetworkedRaccoon.LocalOwned ? "RACCOON  Mouse camera · WASD run · Space jump · Ctrl crouch · E next to the coat: hop back in"
                        : "Waiting for a seat…";
            string room = SessionHost.Instance && !string.IsNullOrEmpty(SessionHost.Instance.RoomCode) ? $"Room {SessionHost.Instance.RoomCode} · " : "";
            var lines = new[] { $"{room}ONLINE · {(nm.IsHost ? "host" : "client")}   {seats}", hint + "   F10: leave" };
            float h = lines.Length * 18f + 8f;
            GUI.Box(new Rect(8, UiScale.Height - h - 8, UiScale.Width - 16, h), GUIContent.none);
            for (int i = 0; i < lines.Length; i++)
                GUI.Label(new Rect(14, UiScale.Height - h - 4 + i * 18f, UiScale.Width - 28, 18), lines[i], style);

            if (mine.HasValue && (snapshot.PartsOf(mine.Value) & BodyPart.Arms) != 0)
            {
                var c = new Vector2(UiScale.Width * 0.5f, UiScale.Height * 0.5f);
                var old = GUI.color;
                GUI.color = _reader.AssistTarget ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 1f, 1f, 0.6f);
                float size = _reader.AssistTarget ? 14f : 8f;
                GUI.DrawTexture(new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size), Texture2D.whiteTexture);
                GUI.color = old;
            }
        }
    }
}
