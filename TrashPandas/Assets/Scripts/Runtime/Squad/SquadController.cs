using System.Collections.Generic;
using TrashPandas.Core.Raccoons;
using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Input;
using TrashPandas.Runtime.Raccoon;
using TrashPandas.Runtime.Trenchcoat;
using TrashPandas.Runtime.Ui;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>
    /// Debug mode for concept v2: one person plays a squad of raccoons. Tab switches which one you control;
    /// the others stay where you left them (so you can build a tower on your own). Online play is driven by
    /// OnlinePlayerController instead.
    /// </summary>
    public sealed class SquadController : MonoBehaviour
    {
        public RaccoonController RaccoonPrefab;
        public PlayerCameraRig CameraRig;
        public TrenchcoatBody Coat;
        public int Count = 3;
        public Vector3 SpawnCenter = new Vector3(0f, 0.1f, 0f);
        public float CameraRadius = 3.2f;
        public float LookHeight = 0.4f;

        public static SquadController Instance { get; private set; }
        public IReadOnlyList<RaccoonController> Raccoons => _raccoons;
        public RaccoonController Active => _active >= 0 && _active < _raccoons.Count ? _raccoons[_active] : null;
        public int ActivePlayerId => Active ? Active.PlayerId : 0;

        readonly List<RaccoonController> _raccoons = new List<RaccoonController>();
        readonly DebugInputReader _reader = new DebugInputReader();
        readonly RaccoonHands _hands = new RaccoonHands();
        int _active;
        float _nextRunNoise;
        GUIStyle _help;

        void Awake()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (!GameMode.Raccoons || (nm && nm.IsListening)) { enabled = false; return; }
            Instance = this;
            Time.fixedDeltaTime = 1f / 60f;
        }

        void OnDestroy() { if (Instance == this) Instance = null; RoundIntro.Finished -= OnIntroFinished; }

        void Start()
        {
            // Offline nothing is spawned: undo NetworkRigidbody's "kinematic until spawned" so props have physics.
            foreach (var nrb in FindObjectsByType<Unity.Netcode.Components.NetworkRigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                nrb.SetIsKinematic(false);
            GameMode.ParkCoat(Coat);
            for (int i = 0; i < Count; i++)
            {
                var r = Instantiate(RaccoonPrefab, Net.DevAutomation.SquadNearOrigin ? SpawnCenter + new Vector3(Mathf.Sin(i * 2.1f), 0f, Mathf.Cos(i * 2.1f)) * 0.9f : GameMode.SpawnPoint(i), Quaternion.Euler(0f, 180f, 0f));
                r.name = $"Raccoon_{i}";
                // Offline: the network sync components must not touch the transform.
                foreach (var nb in r.GetComponents<Unity.Netcode.Components.NetworkTransform>()) nb.enabled = false;
                r.PlayerId = i;
                r.Noise += (kind, at) => NoiseBus.Emit(kind, at);
                _raccoons.Add(r);
            }
            Activate(0);
            if (RoundIntro.Instance) RoundIntro.Instance.Begin(_raccoons.Count);
            RoundIntro.Finished += OnIntroFinished;
        }

        void OnIntroFinished() => Activate(_active);

        /// <summary>Take over the next raccoon that isn't caught (and isn't <paramref name="except"/>). Returns its player id or -1.</summary>
        public int ActivateNextFree(int except)
        {
            for (int k = 1; k <= _raccoons.Count; k++)
            {
                int i = (_active + k) % _raccoons.Count;
                var r = _raccoons[i];
                if (r && !r.Frozen && r.PlayerId != except) { Activate(i); return r.PlayerId; }
            }
            return -1;
        }

        void Activate(int index)
        {
            _active = index;
            if (Active) CameraRig.SetTarget(Active.transform, CameraRadius, LookHeight);
        }

        void Update()
        {
            if (_raccoons.Count == 0 || RoundIntro.Playing) return;
            if (_reader.CyclePressed)
            {
                // Next raccoon still in play.
                for (int k = 1; k <= _raccoons.Count; k++)
                {
                    int i = (_active + k) % _raccoons.Count;
                    if (_raccoons[i] && !_raccoons[i].Frozen) { Activate(i); break; }
                }
            }
            if (Net.DevAutomation.Bot == "heavy") { SquadBots.HeavyTick(_raccoons); return; }
            if (Net.DevAutomation.Bot == "tower") { SquadBots.TowerTick(_raccoons); return; }
            if (Net.DevAutomation.Bot == "rescue" && SquadBots.RescueTick(_raccoons, ref _active)) { Activate(_active); }
            for (int i = 0; i < _raccoons.Count; i++)
            {
                var r = _raccoons[i];
                if (!r) continue;
                if (i != _active) { r.SetInput(Vector2.zero, false, false, false); continue; }
                Vector2 move = (Net.DevAutomation.Bot == "rescue" ? SquadBots.RescueMove(r) : null) ?? Net.DevAutomation.SquadMove(r) ?? _reader.CameraRelativeMove(CameraRig);
                bool run = _reader.RunHeld || Net.DevAutomation.SquadRun;
                r.SetInput(move, _reader.JumpPressed, _reader.JumpHeld, _reader.CrouchHeld, run);
                if (r.IsRunning && Time.time >= _nextRunNoise) { _nextRunNoise = Time.time + 0.5f; NoiseBus.Emit(NoiseKind.Running, r.transform.position); }
            }
            var active = Active;
            if (active)
                _hands.Tick(_reader, CameraRig, active,
                    (index, at) => CarryDirector.Instance.Tap(active, index, at),
                    (dir, strength) => CarryDirector.Instance.Throw(active, dir, strength),
                    Net.DevAutomation.SquadTap(active));
        }

        void OnGUI()
        {
            if (TrashPandas.Runtime.Squad.RoundIntro.Playing) return; // the intro has the screen
            UiScale.Apply();
            _help ??= new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
            float W = UiScale.Width, H = UiScale.Height;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(8, H - 52, W - 16, 44), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (Active && Active.Mount)
            {
                GUI.color = new Color(1f, 0.85f, 0.3f);
                GUI.Label(new Rect(0, H - 84, W, 28), "RIDING — Space: hop off", new GUIStyle(_help) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
                GUI.color = Color.white;
            }
            GUI.Label(new Rect(16, H - 50, W - 32, 42),
                $"RACCOON {ActivePlayerId + 1}/{_raccoons.Count}   WASD move · Shift run (noisy) · C sneak · Space jump · Click grab/drop · Hold click + release: throw · Jump onto a raccoon: ride (Space: hop off) · E use\n" +
                "[DEBUG] Tab: switch raccoon (the others wait where you left them)", _help);
        }
    }
}
