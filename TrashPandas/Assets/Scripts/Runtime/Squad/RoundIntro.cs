using System.Collections.Generic;
using System.Linq;
using TrashPandas.Core.Raccoons;
using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Net;
using TrashPandas.Runtime.Raccoon;
using TrashPandas.Runtime.Ui;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>
    /// The opening gag everyone watches together: the gang climbs out of the manhole one by one, lines up
    /// facing the camera, "GO!" — then each player gets their own camera and control. Online, the host
    /// stamps the start (server time) and every machine animates the raccoons it moves.
    /// </summary>
    public sealed class RoundIntro : NetworkBehaviour
    {
        public PlayerCameraRig CameraRig;
        public float CameraDistance = 3.3f;
        public float CameraLookHeight = 0.45f;

        public static RoundIntro Instance { get; private set; }
        /// <summary>True while the intro plays: nobody controls their raccoon yet.</summary>
        public static bool Playing { get; private set; }

        readonly NetworkVariable<double> _startServerTime = new NetworkVariable<double>(-1);
        readonly NetworkVariable<byte> _players = new NetworkVariable<byte>();
        double _offlineStart = -1;
        int _offlinePlayers;
        Transform _focus;
        bool _cameraSet, _finished;
        GUIStyle _big, _title;

        void Awake() => Instance = this;
        public override void OnDestroy() { if (Instance == this) Instance = null; Playing = false; PlayerCameraRig.CinematicLock = false; base.OnDestroy(); }

        /// <summary>Offline (debug squad) or host (online): start the show now.</summary>
        public void Begin(int players)
        {
            if (Net.DevAutomation.SkipIntro) return;
            if (SimulationAuthority.IsOnline)
            {
                if (!IsServer) return;
                _players.Value = (byte)players;
                _startServerTime.Value = NetworkManager.ServerTime.Time;
            }
            else
            {
                _offlinePlayers = players;
                _offlineStart = Time.timeAsDouble;
            }
            _finished = false;
        }

        double Now => SimulationAuthority.IsOnline ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
        double Start0 => SimulationAuthority.IsOnline ? _startServerTime.Value : _offlineStart;
        int Players => SimulationAuthority.IsOnline ? _players.Value : _offlinePlayers;

        void LateUpdate()
        {
            if (_finished || Start0 < 0 || Players == 0) { Playing = false; return; }
            var timeline = new IntroTimeline(Players);
            float t = (float)(Now - Start0);
            var phase = timeline.PhaseAt(t);
            Playing = phase != IntroPhase.Done;

            // Everyone in line, in player order.
            var gang = FindObjectsByType<RaccoonController>(FindObjectsSortMode.None).Where(r => r && r.PlayerId >= 0).OrderBy(r => r.PlayerId).ToList();
            for (int i = 0; i < gang.Count; i++)
            {
                var r = gang[i];
                if (!r.enabled) continue; // someone else's raccoon: its owner animates it
                var cc = r.GetComponent<CharacterController>();
                if (!Playing) { if (cc && !cc.enabled && !r.Mount) cc.enabled = true; continue; }
                if (cc) cc.enabled = false;
                float k = timeline.FlightProgress(i, t);
                Vector3 from = GameMode.Manhole + Vector3.down * 0.8f, to = GameMode.SpawnPoint(i);
                Vector3 p = Vector3.Lerp(from, to, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 1.3f);
                r.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, 180f + (1f - k) * 540f, 0f)); // a spinning leap out
            }

            if (Playing && CameraRig && !_cameraSet)
            {
                _cameraSet = true;
                if (!_focus) _focus = new GameObject("IntroFocus").transform;
                _focus.position = (GameMode.SpawnPoint(0) + GameMode.SpawnPoint(Mathf.Max(0, Players - 1))) * 0.5f;
                CameraRig.SetTarget(_focus, CameraDistance, CameraLookHeight);
                CameraRig.Orbit.HorizontalAxis.Value = 0f; // in front of the line (they face -Z)
                CameraRig.Orbit.VerticalAxis.Value = 12f;
                PlayerCameraRig.CinematicLock = true;
            }
            if (!Playing && _cameraSet)
            {
                _finished = true;
                PlayerCameraRig.CinematicLock = false;
                Finished?.Invoke();
            }
        }

        /// <summary>Raised on every machine when the intro ends (controllers hand the camera back).</summary>
        public static event System.Action Finished;

        void OnGUI()
        {
            if (!Playing || Start0 < 0) return;
            UiScale.Apply();
            float W = UiScale.Width, H = UiScale.Height;
            var timeline = new IntroTimeline(Players);
            float t = (float)(Now - Start0);
            var phase = timeline.PhaseAt(t);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, W, H * 0.1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, H * 0.9f, W, H * 0.1f), Texture2D.whiteTexture);
            _big ??= new GUIStyle(GUI.skin.label) { fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _title ??= new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            if (phase == IntroPhase.LineUp)
            {
                GUI.color = new Color(1f, 0.85f, 0.3f);
                GUI.Label(new Rect(0, H * 0.14f, W, 50), "THE TRASH PANDAS", _title);
                GUI.color = Color.white;
                GUI.Label(new Rect(0, H * 0.14f + 44, W, 30), "tonight's job: the wedding", new GUIStyle(_title) { fontSize = 20, fontStyle = FontStyle.Italic });
            }
            if (phase == IntroPhase.Go)
            {
                GUI.color = new Color(0.45f, 1f, 0.5f);
                GUI.Label(new Rect(0, H * 0.35f, W, 100), "GO!", _big);
            }
            GUI.color = Color.white;
        }
    }
}
