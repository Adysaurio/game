using TrashPandas.Core.Panic;
using TrashPandas.Runtime.Net;
using TrashPandas.Runtime.Ui;
using UnityEngine;

namespace TrashPandas.Runtime.Panic
{
    /// <summary>Panic HUD: RUN prompt, timer, your hits left, EXIT markers, your fate, and the results screen.</summary>
    public sealed class PanicHud : MonoBehaviour
    {
        float _panicStartedAt = -1f;
        GUIStyle _big, _mid, _small, _exit;

        void OnDisable() => TrashPandas.Runtime.Cameras.PlayerCameraRig.UiWantsCursor = false;

        void OnGUI()
        {
            var d = PanicDirector.Instance;
            var cam = Camera.main;
            TrashPandas.Runtime.Cameras.PlayerCameraRig.UiWantsCursor = d && d.Phase == RoundPhase.Results;
            if (!d || d.Phase == RoundPhase.Infiltration) { _panicStartedAt = -1f; return; }
            UiScale.Apply();
            if (_panicStartedAt < 0f) _panicStartedAt = Time.time;
            _big ??= new GUIStyle(GUI.skin.label) { fontSize = 64, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _mid ??= new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _small ??= new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            _exit ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            var snap = d.Snapshot;
            int? me = d.LocalPlayer;
            var myOutcome = me.HasValue ? snap.OutcomeOf(me.Value) : PlayerOutcome.None;
            float W = UiScale.Width, H = UiScale.Height;

            if (d.Phase == RoundPhase.Panic)
            {
                if (Time.time - _panicStartedAt < 3f)
                {
                    GUI.color = new Color(1f, 0.3f, 0.25f);
                    GUI.Label(new Rect(0, H * 0.22f, W, 90), "¡¡RUUUN!!", _big);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(0, H * 0.22f + 80, W, 30), "Get to an EXIT before they whack you!", _mid);
                }

                GUI.Label(new Rect(W - 170, 50, 160, 30), $"Time {Mathf.CeilToInt(snap.SecondsLeft)}s", _mid);

                if (myOutcome == PlayerOutcome.Running && me.HasValue)
                {
                    int left = 3 - snap.HitsOf(me.Value);
                    GUI.color = new Color(1f, 0.4f, 0.45f);
                    GUI.Label(new Rect(10, 50, 220, 30), "Hits left: " + new string('♥', Mathf.Max(0, left)) + new string('·', Mathf.Max(0, 3 - left)), _mid);
                    GUI.color = Color.white;
                }
                else if (myOutcome == PlayerOutcome.Escaped)
                {
                    GUI.color = new Color(0.4f, 1f, 0.5f);
                    GUI.Label(new Rect(0, H * 0.4f, W, 60), "YOU ESCAPED!", _mid);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(0, H * 0.4f + 40, W, 24), "Watching the others…", _small);
                }
                else if (myOutcome == PlayerOutcome.Caught)
                {
                    GUI.color = new Color(1f, 0.35f, 0.3f);
                    GUI.Label(new Rect(0, H * 0.4f, W, 60), "YOU GOT CAUGHT!", _mid);
                    GUI.color = Color.white;
                    GUI.Label(new Rect(0, H * 0.4f + 40, W, 24), "Watching the others…", _small);
                }

                if (cam)
                    for (int i = 0; i < d.Exits.Length; i++)
                    {
                        if (!d.ExitOpen(i)) continue;
                        Vector3 sp = cam.WorldToScreenPoint(d.Exits[i] + Vector3.up * 1.2f);
                        if (sp.z <= 0f) continue;
                        Vector2 p = UiScale.FromScreen(sp);
                        GUI.color = new Color(1f, 0.85f, 0.2f);
                        string name = i < d.ExitNames.Length ? d.ExitNames[i] : "EXIT";
                        GUI.Label(new Rect(p.x - 80, p.y - 24, 160, 48), $"▼ EXIT\n{name}", _exit);
                        GUI.color = Color.white;
                    }
                return;
            }

            // Results
            float w = 420f, h = 120f + snap.Count * 28f;
            var box = new Rect((W - w) / 2f, (H - h) / 2f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(box.x, box.y + 10, w, 36), "ROUND OVER", _mid);
            for (int p = 0; p < snap.Count; p++)
            {
                var o = snap.OutcomeOf(p);
                if (o == PlayerOutcome.None) continue;
                string who = me == p ? $"P{p} (you)" : $"P{p}";
                GUI.color = o == PlayerOutcome.Escaped ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.45f, 0.4f);
                GUI.Label(new Rect(box.x, box.y + 52 + p * 28, w, 26), $"{who} — {(o == PlayerOutcome.Escaped ? "ESCAPED" : "CAUGHT")}", _small);
            }
            GUI.color = Color.white;
            bool online = SimulationAuthority.IsOnline;
            bool isHost = online && SessionHost.Instance && SessionHost.Instance.IsHost;
            if (!online || (isHost && SessionHost.Instance.Roster.CanStart))
            {
                if (GUI.Button(new Rect(box.x + 40, box.yMax - 50, w - 80, 36), "Play again  (Enter)")) d.PlayAgain();
            }
            else if (isHost)
            {
                GUI.Label(new Rect(box.x, box.yMax - 72, w, 22), "Everyone else left — you need 2 players to play again.", _small);
                if (GUI.Button(new Rect(box.x + 40, box.yMax - 46, w - 80, 34), "Back to menu")) _ = SessionHost.Instance.LeaveAsync();
            }
            else GUI.Label(new Rect(box.x, box.yMax - 50, w, 36), "Waiting for the host to start again…", _small);
            if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                && (!online || (isHost && SessionHost.Instance.Roster.CanStart))) d.PlayAgain();
        }
    }
}
