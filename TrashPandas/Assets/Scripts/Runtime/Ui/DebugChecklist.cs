using System.Collections.Generic;
using UnityEngine;

namespace TrashPandas.Runtime.Ui
{
    /// <summary>
    /// Debug mode: everything there is to try, ticking itself off as you do it (saved across rounds).
    /// F3 shows/hides it, F4 starts over.
    /// </summary>
    public sealed class DebugChecklist : MonoBehaviour
    {
        public static readonly (string key, string label)[] Items =
        {
            ("intro", "Watch the intro (manhole → line-up → GO!)"),
            ("run", "Run with Shift (noisy)"),
            ("sneak", "Sneak with C"),
            ("jump", "Jump (Space)"),
            ("switch", "Switch raccoon with Tab"),
            ("grab", "Grab something (click the ▼)"),
            ("throw", "Throw it (hold click, release)"),
            ("deliver", "Bring loot to the DEN (by the van)"),
            ("objective", "Deliver one of the 3 objectives"),
            ("heavy", "Lift the giant gift with 2 raccoons"),
            ("tower", "Make a tower (jump onto a raccoon)"),
            ("hopoff", "Hop off a tower (Space)"),
            ("heard", "Make a guest hear you (run near them)"),
            ("hide", "Hide (bush, trash can, under a tablecloth)"),
            ("pipe", "Crawl through a drain pipe (E)"),
            ("trashcan", "Hide in a trash can or a bush (E), look around, hop out"),
            ("push", "Push something (walk into a crate / the gift)"),
            ("getaway", "Deliver all 3 objectives: GETAWAY! (exits open)"),
            ("emote", "Dance (G) or cheer (H)"),
            ("calm", "During the RUN, everyone hide until the alert drains (PHEW)"),
            ("nemesis", "Get spotted by tonight's nemesis (planner / pest guy / granny)"),
            ("pebble", "Throw a pebble (2, HOLD Q to aim, let go)"),
            ("bonk", "Bonk a human with a pebble"),
            ("smoke", "Throw a smoke bomb (3, hold Q)"),
            ("banana", "Throw a banana peel (4, hold Q)"),
            ("slip", "Make a human slip on a banana"),
            ("pickup", "Pick up more tools lying around"),
            ("secret", "Push the wardrobe to find a secret pipe"),
            ("ledge", "Jump at a crate edge and hang on"),
            ("climb", "Climb up from hanging (Space)"),
            ("panic", "Get spotted → ¡¡RUUUN!!"),
            ("lost", "Lose a chaser (break line of sight)"),
            ("daze", "Daze a human with a thrown object"),
            ("caged", "Get caught (the pet carrier)"),
            ("rescue", "Free a friend from the cage"),
            ("escape", "Escape through an exit"),
            ("awards", "See an award on the results screen"),
        };

        const string Prefix = "dbgcheck_";
        static readonly HashSet<string> s_done = new HashSet<string>();
        static bool s_loaded, s_visible = true;
        GUIStyle _row, _title;
        float _flashUntil;
        string _flashLabel;
        static DebugChecklist s_instance;

        public static bool Enabled => !Net.SimulationAuthority.IsOnline;

        /// <summary>Tick an item off (anywhere in the code; no-op online).</summary>
        public static void Mark(string key)
        {
            if (!Enabled) return;
            Load();
            if (!s_done.Add(key)) return;
            // Dev bots and headless runs tick it in memory only: the saved list is what *you* tried.
            if (!Application.isBatchMode && Net.DevAutomation.Bot == null)
            {
                PlayerPrefs.SetInt(Prefix + key, 1);
                PlayerPrefs.Save();
            }
            if (s_instance)
            {
                foreach (var item in Items) if (item.key == key) s_instance._flashLabel = item.label;
                s_instance._flashUntil = Time.unscaledTime + 2.5f;
                Sfx.Play2D(Sound.Grab, 0.6f);
            }
        }

        static void Load()
        {
            if (s_loaded) return;
            s_loaded = true;
            foreach (var item in Items) if (PlayerPrefs.GetInt(Prefix + item.key, 0) == 1) s_done.Add(item.key);
        }

        void OnEnable() { s_instance = this; Load(); }
        void OnDisable() { if (s_instance == this) s_instance = null; }

        void Update()
        {
            if (!Enabled) return;
            var k = UnityEngine.InputSystem.Keyboard.current;
            if (k == null) return;
            if (k.f3Key.wasPressedThisFrame || Input.Pad.ChecklistPressed) s_visible = !s_visible;
            if (k.f4Key.wasPressedThisFrame)
            {
                foreach (var item in Items) PlayerPrefs.DeleteKey(Prefix + item.key);
                PlayerPrefs.Save();
                s_done.Clear();
            }
            Poll();
        }

        /// <summary>Things that are easiest to notice by looking at the state of the active raccoon.</summary>
        void Poll()
        {
            var squad = Squad.SquadController.Instance;
            var r = squad ? squad.Active : null;
            if (!r || Squad.RoundIntro.Playing) return;
            if (r.IsRunning) Mark("run");
            if (r.IsSneaking) Mark("sneak");
            if (!ReferenceEquals(r.Mount, null)) Mark("tower");
            var carry = Squad.CarryDirector.Instance;
            if (carry && carry.IsCarrying(r.PlayerId))
            {
                Mark("grab");
                if (carry.Snapshot.Lifted(r.PlayerId)) Mark("heavy");
            }
            var pd = Panic.PanicDirector.Instance;
            if (pd && pd.Phase == Panic.RoundPhase.Panic) Mark("panic");
        }

        void OnGUI()
        {
            if (!Enabled || Squad.RoundIntro.Playing) return;
            GUI.depth = -50; // on top of the exit labels and speech bubbles
            UiScale.Apply();
            float W = UiScale.Width;
            _row ??= new GUIStyle(GUI.skin.label) { fontSize = 11 };
            _title ??= new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };

            if (Time.unscaledTime < _flashUntil && _flashLabel != null)
            {
                GUI.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);
                GUI.DrawTexture(new Rect(W / 2f - 210, 92, 420, 30), Texture2D.whiteTexture);
                GUI.color = new Color(0.5f, 1f, 0.55f);
                GUI.Label(new Rect(W / 2f - 200, 96, 400, 24), "[x] " + _flashLabel, new GUIStyle(_title) { alignment = TextAnchor.MiddleCenter });
                GUI.color = Color.white;
            }
            if (!s_visible)
            {
                GUI.Label(new Rect(10, 176, 200, 20), $"F3: test checklist ({s_done.Count}/{Items.Length})", _row);
                return;
            }
            float w = 300f, h = 30f + Items.Length * 15f + 20f, x = 10f, y = 176f;
            GUI.color = new Color(0.05f, 0.05f, 0.07f, 0.88f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(x + 10, y + 6, w - 20, 22), $"TEST CHECKLIST  {s_done.Count}/{Items.Length}", _title);
            float ry = y + 30f;
            foreach (var item in Items)
            {
                bool done = s_done.Contains(item.key);
                GUI.color = done ? new Color(0.5f, 1f, 0.55f) : new Color(0.85f, 0.85f, 0.85f);
                GUI.Label(new Rect(x + 10, ry, w - 20, 16), (done ? "[x] " : "[  ] ") + item.label, _row);
                ry += 15f;
            }
            GUI.color = new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(new Rect(x + 10, ry + 2, w - 20, 18), "F3 hide · F4 start over", _row);
            GUI.color = Color.white;
        }
    }
}
