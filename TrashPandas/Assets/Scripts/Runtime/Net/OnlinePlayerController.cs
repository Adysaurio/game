using TrashPandas.Core.Trenchcoat;
using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Input;
using Unity.Netcode;
using UnityEngine;

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

        readonly DebugInputReader _reader = new DebugInputReader();
        uint _sequence;
        float _nextSend;
        bool? _armsCamera;
        NetworkedTrenchcoat _coat;

        static bool Online => NetworkManager.Singleton && NetworkManager.Singleton.IsListening;

        void Awake()
        {
            if (!Online) enabled = false; // offline, the debug controller runs the scene
        }

        void Update()
        {
            if (!_coat) { _coat = NetworkedTrenchcoat.Instance; if (!_coat) return; _armsCamera = null; }
            var nm = NetworkManager.Singleton;
            float now = nm.ServerTime.TimeAsFloat;
            var snapshot = _coat.Slots;
            int? slot = snapshot.SlotOfClient(nm.LocalClientId);

            if (slot.HasValue)
            {
                var parts = snapshot.PartsOf(slot.Value);
                UpdateCamera(parts);
                var input = _reader.ReadSlotInput(CameraRig, _coat.Body, now);
                if (Time.unscaledTime >= _nextSend)
                {
                    _nextSend = Time.unscaledTime + 1f / SendRate;
                    _coat.SubmitInputRpc(new NetSlotInput { Sequence = ++_sequence, Input = input });
                }
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
            string hint = mine.HasValue ? DebugInputReader.HintFor(snapshot.PartsOf(mine.Value)) : "Waiting for a seat…";
            string room = SessionHost.Instance && !string.IsNullOrEmpty(SessionHost.Instance.RoomCode) ? $"Room {SessionHost.Instance.RoomCode} · " : "";
            var lines = new[] { $"{room}ONLINE · {(nm.IsHost ? "host" : "client")}   {seats}", hint };
            float h = lines.Length * 18f + 8f;
            GUI.Box(new Rect(8, Screen.height - h - 8, Screen.width - 16, h), GUIContent.none);
            for (int i = 0; i < lines.Length; i++)
                GUI.Label(new Rect(14, Screen.height - h - 4 + i * 18f, Screen.width - 28, 18), lines[i], style);

            if (mine.HasValue && (snapshot.PartsOf(mine.Value) & BodyPart.Arms) != 0)
            {
                var c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                var old = GUI.color;
                GUI.color = _reader.AssistTarget ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 1f, 1f, 0.6f);
                float size = _reader.AssistTarget ? 14f : 8f;
                GUI.DrawTexture(new Rect(c.x - size * 0.5f, c.y - size * 0.5f, size, size), Texture2D.whiteTexture);
                GUI.color = old;
            }
        }
    }
}
