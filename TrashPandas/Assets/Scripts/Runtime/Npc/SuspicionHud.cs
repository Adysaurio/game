using TrashPandas.Core.Npc;
using TrashPandas.Runtime.Net;
using UnityEngine;
using TrashPandas.Runtime.Ui;

namespace TrashPandas.Runtime.Npc
{
    /// <summary>Suspicion bar, "?" / "!" over humans, and the RUN banner. Works offline and online.</summary>
    public sealed class SuspicionHud : MonoBehaviour
    {
        float _caughtAt = -1f;
        GUIStyle _icon, _banner, _label;

        void Update()
        {
            var d = SuspicionDirector.Instance;
            if (!d) return;
            if (d.Caught && _caughtAt < 0f) _caughtAt = Time.time;   // show the banner once per catch
            if (!d.Caught) _caughtAt = -1f;
            if (!SimulationAuthority.IsOnline && UnityEngine.InputSystem.Keyboard.current?.f5Key.wasPressedThisFrame == true)
                d.ResetSuspicion();
        }

        GUIStyle _bubble;
        readonly System.Collections.Generic.List<Rect> _drawnBubbles = new System.Collections.Generic.List<Rect>();
        static readonly string[] Chase = { "THIEF!", "RACCOON!", "GET IT!", "MY CAKE!", "COME BACK HERE!", "NOT TODAY!", "SECURITY!" };
        static readonly string[] Search = { "where'd it go?", "here, kitty…?", "I KNOW you're here", "hmm…", "show yourself!" };
        static readonly string[] Winded = { "huff… huff…", "too old for this", "*wheeze*", "gimme a sec…" };
        static readonly string[] Stunned = { "@_@", "OW!", "my eye!", "who threw that?!" };

        /// <summary>A line that fits the mood, stable for a few seconds per person.</summary>
        static string PanicLine(NpcPawn pawn, byte mood)
        {
            int seed = pawn.name.GetHashCode() ^ (int)(Time.time / 3.5f);
            string Pick(string[] lines) => lines[(seed & 0x7fffffff) % lines.Length];
            switch (mood)
            {
                case NpcPawn.MoodWindup: return "!!";
                case NpcPawn.MoodChasing:
                    if (pawn.Kind == NpcKind.Cat) return "HSSSS!";
                    if (pawn.SpeakerId == "MotherInLaw") return "THERE! GET IT!";
                    if (pawn.SpeakerId == "Priest") return "LORD HAVE MERCY";
                    return Pick(Chase);
                case NpcPawn.MoodSearching: return Pick(Search);
                case NpcPawn.MoodWinded: return Pick(Winded);
                case NpcPawn.MoodStunned: return Pick(Stunned);
                default: return null;
            }
        }

        void OnGUI()
        {
            if (TrashPandas.Runtime.Squad.RoundIntro.Playing) return; // the intro has the screen
            UiScale.Apply();
            var d = SuspicionDirector.Instance;
            var cam = Camera.main;
            if (!d || !cam) return;
            _icon ??= new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _banner ??= new GUIStyle(GUI.skin.label) { fontSize = 72, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };

            // Bar
            float w = Mathf.Min(360f, UiScale.Width - 40f), x = (UiScale.Width - w) / 2f, y = 14f;
            float k = d.Suspicion / 100f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x - 4, y - 4, w + 8, 26), Texture2D.whiteTexture);
            GUI.color = Color.Lerp(new Color(1f, 0.8f, 0.2f), new Color(1f, 0.25f, 0.2f), k);
            GUI.DrawTexture(new Rect(x, y, w * k, 18), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, w, 18), $"SUSPICION {Mathf.RoundToInt(d.Suspicion)}", _label);

            // Icons over humans
            if (Event.current.type == EventType.Repaint || Event.current.type == EventType.Layout) _drawnBubbles.Clear();
            foreach (var brain in d.Brains)
            {
                var pawn = brain.Pawn;
                if (!pawn) continue;
                string icon = null; Color color = Color.white;
                if (pawn.Mood >= NpcPawn.MoodChasing)
                {
                    // The RUN: little speech bubbles with personality (Goose-style reactions).
                    string line = PanicLine(pawn, pawn.Mood);
                    if (line == null) continue;
                    Vector3 bp = cam.WorldToScreenPoint(pawn.Eye + Vector3.up * 0.6f);
                    if (bp.z <= 0f || bp.z > 30f) continue;
                    Vector2 q = UiScale.FromScreen(bp);
                    bool windup = pawn.Mood == NpcPawn.MoodWindup;
                    var style = windup ? _icon : (_bubble ??= new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter });
                    float bw = windup ? 60f : Mathf.Max(70f, line.Length * 10.5f + 18f);
                    var rect = new Rect(q.x - bw / 2f, q.y - 14f, bw, 26f);
                    bool overlaps = false;
                    foreach (var other in _drawnBubbles) if (other.Overlaps(rect)) { overlaps = true; break; }
                    if (overlaps && !windup) continue; // a crowd shouts one line at a time
                    _drawnBubbles.Add(rect);
                    if (!windup)
                    {
                        GUI.color = new Color(1f, 1f, 1f, 0.92f);
                        GUI.DrawTexture(new Rect(q.x - bw / 2f, q.y - 14f, bw, 26f), Texture2D.whiteTexture);
                    }
                    GUI.color = windup ? new Color(1f, 0.2f, 0.15f, 0.6f + 0.4f * Mathf.PingPong(Time.time * 6f, 1f)) : new Color(0.1f, 0.1f, 0.12f);
                    GUI.Label(new Rect(q.x - bw / 2f, q.y - 14f, bw, 26f), line, style);
                    GUI.color = Color.white;
                    continue;
                }
                if (pawn.Kind == NpcKind.Cat)
                {
                    var s = (CatState)pawn.Mood;
                    if (s == CatState.Sniffing) { icon = "?"; color = new Color(1f, 0.9f, 0.4f); }
                    else if (s == CatState.Hissing) { icon = "HSSS!"; color = new Color(1f, 0.35f, 0.3f); }
                }
                else
                {
                    var s = (GuestState)pawn.Mood;
                    if (s == GuestState.Curious) { icon = "?"; color = new Color(1f, 0.9f, 0.4f); }
                    else if (s == GuestState.Alarmed) { icon = "!"; color = new Color(1f, 0.35f, 0.3f); }
                }
                if (icon == null) continue;
                Vector3 sp = cam.WorldToScreenPoint(pawn.Eye + Vector3.up * 0.55f);
                if (sp.z <= 0f) continue;
                Vector2 p = UiScale.FromScreen(sp);
                GUI.color = color;
                GUI.Label(new Rect(p.x - 40, p.y - 18, 80, 36), icon, _icon);
                // The "?" fills up while they see you: hide before it's full.
                float aw = pawn.Awareness;
                if (icon == "?" && aw > 0.02f)
                {
                    GUI.color = new Color(0f, 0f, 0f, 0.6f);
                    GUI.DrawTexture(new Rect(p.x - 18, p.y + 16, 36, 6), Texture2D.whiteTexture);
                    GUI.color = Color.Lerp(new Color(1f, 0.9f, 0.3f), new Color(1f, 0.25f, 0.2f), aw);
                    GUI.DrawTexture(new Rect(p.x - 17, p.y + 17, 34 * aw, 4), Texture2D.whiteTexture);
                }
            }
            GUI.color = Color.white;

            if (d.Caught && _caughtAt >= 0f && Time.time - _caughtAt < 4f && !TrashPandas.Runtime.Panic.PanicDirector.Instance)
            {
                GUI.color = new Color(1f, 0.3f, 0.25f);
                GUI.Label(new Rect(0, UiScale.Height * 0.3f, UiScale.Width, 120), "¡¡RUUUN!!", _banner);
                GUI.color = Color.white;
                if (!SimulationAuthority.IsOnline)
                    GUI.Label(new Rect(0, UiScale.Height * 0.3f + 110, UiScale.Width, 20), "(panic phase comes in stage 4 — F5 resets suspicion)", _label);
            }
        }
    }
}
