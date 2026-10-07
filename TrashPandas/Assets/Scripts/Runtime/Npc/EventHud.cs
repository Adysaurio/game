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
            if (!d) return;
            UiScale.Apply();
            if (d.Phase == EventPhase.Idle) { DrawLetterbox(UiScale.Width, UiScale.Height); return; } // bars sliding out
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
                GUI.color = new Color(0f, 0f, 0f, 0.55f);
                GUI.DrawTexture(new Rect(W * 0.5f - 300f, 106, 600f, 32), Texture2D.whiteTexture);
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

            // Right-hand side, so the close-up of the coat and the speaker stays visible.
            DrawConversation(d, ev, snap, W, H);
        }

        // --- RPG conversation presentation --------------------------------------------------------
        float _barsK;            // 0 = no letterbox, 1 = full
        float _engagedAt = -1f;
        byte _engagedSerial = 255;
        Texture2D _vignette;

        void LateUpdate()
        {
            var d = SocialEventDirector.Instance;
            bool talking = d && (d.Phase == EventPhase.Engaged || d.Phase == EventPhase.Resolved);
            _barsK = Mathf.MoveTowards(_barsK, talking ? 1f : 0f, Time.unscaledDeltaTime / 0.35f);
            if (d && d.Phase == EventPhase.Engaged && d.Snapshot.Serial != _engagedSerial)
            {
                _engagedSerial = d.Snapshot.Serial;
                _engagedAt = Time.unscaledTime;
            }
        }

        void DrawLetterbox(float W, float H)
        {
            if (_barsK <= 0f) return;
            float ease = 1f - (1f - _barsK) * (1f - _barsK);
            float bar = H * 0.12f * ease;
            _vignette ??= MakeVignette();
            GUI.color = new Color(1f, 1f, 1f, 0.85f * ease);
            GUI.DrawTexture(new Rect(0, 0, W, H), _vignette);
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, W, bar), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0, H - bar, W, bar), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        static Texture2D MakeVignette()
        {
            const int n = 128;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy) / 0.7071f;
                    t.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 0.75f, (r - 0.45f) / 0.55f)));
                }
            t.Apply();
            return t;
        }

        void DrawConversation(SocialEventDirector d, SocialEvent ev, EventSnapshot snap, float W, float H)
        {
            DrawLetterbox(W, H);
            if (_barsK < 0.6f) return;

            float w = Mathf.Min(860f, W - 40f), x = (W - w) / 2f, h = 190f, y = H - h - 16f;
            // Name plate
            GUI.color = new Color(0.95f, 0.72f, 0.25f);
            GUI.DrawTexture(new Rect(x + 18, y - 30, 280, 32), Texture2D.whiteTexture);
            GUI.color = new Color(0.12f, 0.08f, 0.02f);
            GUI.Label(new Rect(x + 30, y - 29, 270, 30), ev.SpeakerName.ToUpperInvariant(), _plate ??= new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft });
            // Box
            GUI.color = new Color(0.05f, 0.06f, 0.08f, 0.92f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(0.95f, 0.72f, 0.25f);
            GUI.DrawTexture(new Rect(x, y, w, 2), Texture2D.whiteTexture);
            GUI.color = Color.white;

            var line = _line ??= new GUIStyle(GUI.skin.label) { fontSize = 21, wordWrap = true, alignment = TextAnchor.UpperLeft };
            var parts = MyParts();
            if (d.Phase == EventPhase.Engaged)
            {
                // Response timer along the top edge of the box.
                GUI.color = new Color(1f, 0.85f, 0.3f);
                GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(snap.SecondsLeft / d.ResponseWindow), 3), Texture2D.whiteTexture);
                GUI.color = Color.white;

                int shown = _engagedAt < 0f ? ev.Line.Length : Mathf.Clamp((int)((Time.unscaledTime - _engagedAt) * 55f), 0, ev.Line.Length);
                GUI.Label(new Rect(x + 22, y + 14, w - 44, 56), "“" + ev.Line.Substring(0, shown) + (shown < ev.Line.Length ? "" : "”"), line);

                float col = x + 22, ty = y + 76;
                if ((parts & BodyPart.Head) != 0)
                {
                    for (int i = 0; i < ev.Options.Length; i++)
                        GUI.Label(new Rect(col, ty + i * 24, w * 0.55f, 24), $"[{i + 1}]  {ev.Options[i]}", _option);
                }
                else GUI.Label(new Rect(col, ty, w * 0.55f, 24), parts == BodyPart.None ? "You're not in the coat — your part fails!" : "The head is talking…", _small);

                float rx = x + w * 0.6f, ry = y + 76;
                GUI.Label(new Rect(rx, ry, w * 0.38f, 22), "YOUR PART", _small);
                ry += 24;
                if ((parts & BodyPart.Head) != 0) { GUI.Label(new Rect(rx, ry, w * 0.38f, 22), "• Head: press 1, 2 or 3", _small); ry += 22; }
                if ((parts & BodyPart.Arms) != 0 && ev.Arms != ArmsTask.None) { GUI.Label(new Rect(rx, ry, w * 0.38f, 22), "• Arms: " + ArmsPrompt(ev.Arms), _small); ry += 22; }
                if ((parts & BodyPart.Legs) != 0 && ev.Legs != LegsTask.None) GUI.Label(new Rect(rx, ry, w * 0.38f, 22), "• Legs: " + LegsPrompt(ev.Legs), _small);
                return;
            }

            // Resolved: the verdict, in the same box.
            bool good = snap.ResultDelta <= 0f;
            GUI.Label(new Rect(x + 22, y + 14, w - 44, 30), good ? "“How delightful! Enjoy the party.”" : "“Hmm... how very... peculiar.”", line);
            GUI.color = good ? new Color(0.45f, 1f, 0.55f) : new Color(1f, 0.4f, 0.35f);
            GUI.Label(new Rect(x, y + 60, w, 50), good ? $"SMOOTH!   Suspicion {Mathf.RoundToInt(snap.ResultDelta)}" : $"AWKWARD...   Suspicion +{Mathf.RoundToInt(snap.ResultDelta)}", _big);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y + 118, w, 26), $"Head: {Describe(snap.HeadOutcome)}     Arms: {Describe(snap.ArmsOutcome)}     Legs: {Describe(snap.LegsOutcome)}", _mid);
        }

        GUIStyle _plate, _line;

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
