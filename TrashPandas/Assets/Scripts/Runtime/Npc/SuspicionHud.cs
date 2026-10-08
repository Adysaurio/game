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
            foreach (var brain in d.Brains)
            {
                var pawn = brain.Pawn;
                if (!pawn) continue;
                string icon = null; Color color = Color.white;
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
