using TrashPandas.Core.Events;
using TrashPandas.Core.Trenchcoat;
using TrashPandas.Runtime.Net;
using TrashPandas.Runtime.Ui;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.Runtime.Npc
{
    /// <summary>"EVENT IN N" warning, the conversation panel with this player's part, and the result.</summary>
    public sealed class EventHud : MonoBehaviour
    {
        GUIStyle _big, _mid, _small, _option;

        static BodyPart MyParts()
        {
            if (!SimulationAuthority.IsOnline) return BodyPart.All; // debug: you play every role
            var coat = NetworkedTrenchcoat.Instance;
            if (!coat) return BodyPart.None;
            var slot = coat.Slots.SlotOfClient(NetworkManager.Singleton.LocalClientId);
            return slot.HasValue ? coat.Slots.PartsOf(slot.Value) : BodyPart.None;
        }

        void OnGUI()
        {
            var d = SocialEventDirector.Instance;
            if (!d || d.Phase == EventPhase.Idle) return;
            UiScale.Apply();
            _big ??= new GUIStyle(GUI.skin.label) { fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _mid ??= new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            _small ??= new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            _option ??= new GUIStyle(GUI.skin.label) { fontSize = 17, alignment = TextAnchor.MiddleLeft };
            float W = UiScale.Width, H = UiScale.Height;
            var ev = d.Current;
            var snap = d.Snapshot;

            if (d.Phase == EventPhase.Warning)
            {
                GUI.color = new Color(1f, 0.35f, 0.3f);
                GUI.Label(new Rect(0, 60, W, 50), $"EVENT IN {Mathf.CeilToInt(snap.SecondsLeft)}!", _big);
                GUI.color = Color.white;
                GUI.Label(new Rect(0, 108, W, 30), $"{ev.SpeakerName} is coming over. Everyone back in the coat!", _mid);
                var cam = Camera.main;
                var at = d.SpeakerPosition;
                if (cam && at.HasValue)
                {
                    Vector3 sp = cam.WorldToScreenPoint(at.Value + Vector3.up * 2.3f);
                    if (sp.z > 0f)
                    {
                        Vector2 p = UiScale.FromScreen(sp);
                        GUI.color = new Color(1f, 0.85f, 0.3f);
                        GUI.Label(new Rect(p.x - 90, p.y - 20, 180, 40), $"▼ {ev.SpeakerName}", _mid);
                        GUI.color = Color.white;
                    }
                }
                return;
            }

            float w = Mathf.Min(620f, W - 40f), x = (W - w) / 2f;
            var parts = MyParts();
            if (d.Phase == EventPhase.Engaged)
            {
                float h = 250f;
                float y = H - h - 90f;
                GUI.color = new Color(0f, 0f, 0f, 0.78f);
                GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.82f, 0.3f);
                GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(snap.SecondsLeft / d.ResponseWindow), 5), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 14, y + 12, w - 28, 50), $"{ev.SpeakerName}: “{ev.Line}”", _mid);
                float ty = y + 70;
                if ((parts & BodyPart.Head) != 0)
                {
                    GUI.Label(new Rect(x + 16, ty, w - 32, 22), "HEAD — press 1, 2 or 3 to answer:", _small);
                    for (int i = 0; i < ev.Options.Length; i++)
                        GUI.Label(new Rect(x + 36, ty + 24 + i * 22, w - 52, 22), $"[{i + 1}] {ev.Options[i]}", _option);
                    ty += 24 + ev.Options.Length * 22 + 6;
                }
                if ((parts & BodyPart.Arms) != 0 && ev.Arms != ArmsTask.None)
                {
                    GUI.Label(new Rect(x + 16, ty, w - 32, 22), "ARMS — " + ArmsPrompt(ev.Arms), _small);
                    ty += 24;
                }
                if ((parts & BodyPart.Legs) != 0 && ev.Legs != LegsTask.None)
                    GUI.Label(new Rect(x + 16, ty, w - 32, 22), "LEGS — " + LegsPrompt(ev.Legs), _small);
                if (parts == BodyPart.None)
                    GUI.Label(new Rect(x + 16, ty, w - 32, 44), "You're not in the coat! Your part fails...", _small);
                return;
            }

            // Resolved
            bool good = snap.ResultDelta <= 0f;
            GUI.color = good ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.4f, 0.35f);
            GUI.Label(new Rect(0, H * 0.28f, W, 40), good ? "SMOOTH! Suspicion " + Mathf.RoundToInt(snap.ResultDelta) : "AWKWARD... Suspicion +" + Mathf.RoundToInt(snap.ResultDelta), _big);
            GUI.color = Color.white;
            GUI.Label(new Rect(0, H * 0.28f + 46, W, 26), $"Head: {Describe(snap.HeadOutcome)}   Arms: {Describe(snap.ArmsOutcome)}   Legs: {Describe(snap.LegsOutcome)}", _mid);
        }

        static string ArmsPrompt(ArmsTask t) => t switch
        {
            ArmsTask.Handshake => "hold LEFT CLICK to shake hands",
            ArmsTask.TakeGlass => "hold LEFT CLICK to take it",
            ArmsTask.HandsTogether => "hold LEFT + RIGHT CLICK: hands together",
            _ => "",
        };

        static string LegsPrompt(LegsTask t) => t switch
        {
            LegsTask.StayStill => "DON'T MOVE!",
            LegsTask.Kneel => "hold CTRL to kneel",
            LegsTask.DanceStep => "press SPACE: dance step!",
            _ => "",
        };

        static string Describe(byte code) => code switch
        {
            0 => "—",
            1 => "great",
            2 => "odd",
            3 => "FAILED",
            _ => "MISSING",
        };
    }
}
