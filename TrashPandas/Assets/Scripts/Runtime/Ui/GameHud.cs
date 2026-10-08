using TrashPandas.Core.Raccoons;
using TrashPandas.Runtime.Loot;
using TrashPandas.Runtime.Net;
using TrashPandas.Runtime.Npc;
using TrashPandas.Runtime.Panic;
using TrashPandas.Runtime.Raccoon;
using TrashPandas.Runtime.Squad;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrashPandas.Runtime.Ui
{
    /// <summary>
    /// The heist HUD: a minimap (you, your crew in their bandana colors, the den, the objectives, alarmed
    /// humans, the exits and the cage once it's RUN) and an item bar (what you carry, pebbles, smoke bombs).
    /// 2/3 or the mouse wheel pick a tool, Q uses it.
    /// </summary>
    public sealed class GameHud : MonoBehaviour
    {
        public float MapSize = 62f;              // world meters shown (square)
        public Vector3 MapCenter = new Vector3(0f, 0f, 2f);

        RenderTexture _map;
        Texture2D _mapTex;
        int _renderedFrames;
        Gadget _selected = Gadget.Pebble;
        GUIStyle _small, _label, _count, _icon;
        static Texture2D s_round;

        RaccoonController Me
        {
            get
            {
                if (!SimulationAuthority.IsOnline) return SquadController.Instance ? SquadController.Instance.Active : null;
                return NetworkedRaccoon.LocalOwned ? NetworkedRaccoon.LocalOwned.Controller : null;
            }
        }

        System.Collections.IEnumerator Start()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break; // headless
            yield return null; // let the scene settle first
            yield return RenderMap();
        }

        /// <summary>A top-down snapshot of the estate (taken once; the map doesn't change).</summary>
        System.Collections.IEnumerator RenderMap()
        {
            _map = new RenderTexture(512, 512, 16) { name = "Minimap" };
            var cam = new GameObject("MinimapCamera").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = MapSize * 0.5f;
            cam.transform.SetPositionAndRotation(MapCenter + Vector3.up * 80f, Quaternion.Euler(90f, 0f, 0f));
            cam.farClipPlane = 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.2f, 0.3f, 0.2f);
            cam.targetTexture = _map;
            cam.depth = -10f;
            yield return null;
            yield return new WaitForEndOfFrame();
            Destroy(cam.gameObject);
            // Copy to an opaque texture (the render leaves alpha at 0, which OnGUI would blend away).
            var prev = RenderTexture.active;
            RenderTexture.active = _map;
            var tex = new Texture2D(_map.width, _map.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, _map.width, _map.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            _map.Release();
            _mapTex = tex;
        }

        Vector2 ToMap(Vector3 world, Rect r)
        {
            float u = (world.x - MapCenter.x) / MapSize + 0.5f, v = (world.z - MapCenter.z) / MapSize + 0.5f;
            return new Vector2(r.x + Mathf.Clamp01(u) * r.width, r.yMax - Mathf.Clamp01(v) * r.height);
        }

        void Update()
        {
            var k = Keyboard.current;
            var m = Mouse.current;
            if (k != null)
            {
                if (k.digit2Key.wasPressedThisFrame) _selected = Gadget.Pebble;
                if (k.digit3Key.wasPressedThisFrame) _selected = Gadget.SmokeBomb;
            }
            if (m != null && Mathf.Abs(m.scroll.ReadValue().y) > 0.1f) _selected = _selected == Gadget.Pebble ? Gadget.SmokeBomb : Gadget.Pebble;
            var me = Me;
            if (k != null && k.qKey.wasPressedThisFrame && me && GadgetDirector.Instance && !RoundIntro.Playing)
            {
                var cam = Camera.main;
                GadgetDirector.Instance.Use(me, _selected, cam ? cam.transform.forward : me.transform.forward);
            }
            if (Net.DevAutomation.GadgetBot(me, out var g, out var aim) && GadgetDirector.Instance) GadgetDirector.Instance.Use(me, g, aim);
        }

        void OnGUI()
        {
            if (RoundIntro.Playing) return;
            UiScale.Apply();
            _small ??= new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _label ??= new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
            _count ??= new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold };
            _icon ??= new GUIStyle(GUI.skin.label) { fontSize = 26, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            DrawMinimap();
            DrawItemBar();
        }

        void Dot(Vector2 p, float size, Color c)
        {
            s_round ??= MakeRound();
            GUI.color = c;
            GUI.DrawTexture(new Rect(p.x - size / 2f, p.y - size / 2f, size, size), s_round);
        }

        static Texture2D MakeRound()
        {
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f, d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 8f)));
            }
            t.Apply();
            return t;
        }

        void DrawMinimap()
        {
            float W = UiScale.Width, size = 180f;
            var r = new Rect(W - size - 14f, 84f, size, size);
            GUI.color = new Color(0.25f, 0.16f, 0.08f, 0.95f);
            GUI.DrawTexture(new Rect(r.x - 5, r.y - 5, r.width + 10, r.height + 10), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (_mapTex) GUI.DrawTexture(r, _mapTex);

            var loot = LootDirector.Instance;
            var pd = PanicDirector.Instance;
            bool panic = pd && pd.Phase != RoundPhase.Infiltration;
            if (loot)
            {
                Dot(ToMap(loot.DenCenter, r), 14f, new Color(0.55f, 0.35f, 0.15f));
                GUI.color = Color.white;
                GUI.Label(new Rect(ToMap(loot.DenCenter, r).x - 10, ToMap(loot.DenCenter, r).y - 10, 20, 20), "D", _small);
                var s = loot.Snapshot;
                foreach (var item in loot.Items)
                    if (item && item.IsObjective && item.State == LootState.Active && s.IsPicked(item.Objective))
                    {
                        var p = ToMap(item.transform.position, r);
                        Dot(p, 11f, new Color(1f, 0.82f, 0.15f));
                        GUI.color = new Color(0.3f, 0.2f, 0f);
                        GUI.Label(new Rect(p.x - 8, p.y - 9, 16, 18), "$", _small);
                    }
            }
            if (panic)
            {
                for (int i = 0; i < pd.Exits.Length; i++)
                    if (pd.ExitOpen(i)) { var p = ToMap(pd.Exits[i], r); Dot(p, 13f, new Color(0.3f, 1f, 0.4f)); GUI.color = Color.black; GUI.Label(new Rect(p.x - 10, p.y - 9, 20, 18), "X", _small); }
                if (pd.CageRadius > 0f) Dot(ToMap(pd.CagePosition, r), 11f, new Color(0.45f, 0.75f, 1f));
            }
            // Humans who've noticed something.
            var sd = SuspicionDirector.Instance;
            if (sd)
                foreach (var b in sd.Brains)
                {
                    if (!b.Pawn) continue;
                    byte mood = b.Pawn.Mood;
                    bool alarmed = mood == 2 || mood == NpcPawn.MoodChasing || mood == NpcPawn.MoodWindup;
                    bool curious = mood == 1 || mood == NpcPawn.MoodSearching;
                    if (alarmed || curious) Dot(ToMap(b.Pawn.transform.position, r), 7f, alarmed ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.85f, 0.3f));
                }
            // The crew (bandana colors), you on top with a facing tick.
            var me = Me;
            foreach (var rc in FindObjectsByType<RaccoonController>(FindObjectsSortMode.None))
            {
                if (!rc || rc.PlayerId < 0 || rc == me) continue;
                Dot(ToMap(rc.transform.position, r), 9f, RaccoonLook.PlayerColors[rc.PlayerId % RaccoonLook.PlayerColors.Length]);
            }
            if (me)
            {
                var p = ToMap(me.transform.position, r);
                Dot(p, 13f, Color.white);
                Dot(p, 10f, RaccoonLook.PlayerColors[Mathf.Max(0, me.PlayerId) % RaccoonLook.PlayerColors.Length]);
                Vector3 f = me.transform.forward;
                Dot(p + new Vector2(f.x, -f.z) * 9f, 5f, Color.white);
            }
            GUI.color = Color.white;
        }

        void DrawItemBar()
        {
            float W = UiScale.Width, H = UiScale.Height;
            float slot = 58f, gap = 14f, w = slot * 3 + gap * 4, h = 92f, x = (W - w) / 2f, y = H - h - 58f;
            // A wooden plank.
            GUI.color = new Color(0.36f, 0.22f, 0.11f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = new Color(0.5f, 0.32f, 0.16f, 1f);
            GUI.DrawTexture(new Rect(x + 3, y + 3, w - 6, 4), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x + 3, y + h - 7, w - 6, 4), Texture2D.whiteTexture);
            var me = Me;
            var gd = GadgetDirector.Instance;
            int pid = me ? me.PlayerId : 0;

            // Slot 1: what you're carrying.
            var carry = CarryDirector.Instance;
            var carried = me && carry ? carry.ItemOf(me.PlayerId) : null;
            var lootItem = carried ? carried.GetComponent<LootItem>() : null;
            DrawSlot(new Rect(x + gap, y + 8, slot, slot), "1", carried ? (lootItem ? "$" : "•") : "", carried ? (lootItem ? lootItem.Label : carried.name) : "paws free", false, -1);
            DrawSlot(new Rect(x + gap * 2 + slot, y + 8, slot, slot), "2", "●", "Pebble", _selected == Gadget.Pebble, gd ? gd.Snapshot.Count(pid, Gadget.Pebble) : 0);
            DrawSlot(new Rect(x + gap * 3 + slot * 2, y + 8, slot, slot), "3", "◍", "Smoke bomb", _selected == Gadget.SmokeBomb, gd ? gd.Snapshot.Count(pid, Gadget.SmokeBomb) : 0);
            GUI.color = new Color(1f, 0.9f, 0.7f);
            GUI.Label(new Rect(x, y - 18, w, 16), "Q: use tool · wheel / 2-3: pick", _label);
            GUI.color = Color.white;
        }

        void DrawSlot(Rect r, string key, string glyph, string name, bool selected, int count)
        {
            s_round ??= MakeRound();
            GUI.color = selected ? new Color(1f, 0.85f, 0.3f) : new Color(0.2f, 0.12f, 0.06f);
            GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), s_round);
            GUI.color = new Color(0.85f, 0.75f, 0.6f);
            GUI.DrawTexture(r, s_round);
            GUI.color = count == 0 ? new Color(0.35f, 0.3f, 0.25f, 0.6f) : new Color(0.25f, 0.2f, 0.15f);
            GUI.Label(r, glyph, _icon);
            GUI.color = Color.white;
            if (count >= 0)
            {
                GUI.color = new Color(0.2f, 0.12f, 0.06f);
                GUI.DrawTexture(new Rect(r.xMax - 16, r.yMax - 18, 24, 18), s_round);
                GUI.color = Color.white;
                GUI.Label(new Rect(r.xMax - 18, r.yMax - 18, 28, 18), $"x{count}", _small);
            }
            GUI.Label(new Rect(r.x - 20, r.yMax + 1, r.width + 40, 16), name.Length > 16 ? name.Substring(0, 15) + "…" : name, _small);
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(r.x - 4, r.y - 4, 16, 16), key, _small);
            GUI.color = Color.white;
        }
    }
}
