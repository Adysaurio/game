using TrashPandas.Core.Loot;
using TrashPandas.Runtime.Net;
using TrashPandas.Runtime.Panic;
using TrashPandas.Runtime.Trenchcoat;
using TrashPandas.Runtime.Ui;
using UnityEngine;

namespace TrashPandas.Runtime.Loot
{
    /// <summary>The coat's pocket ($), this round's objectives, the infiltration clock, "+$40" pop-ups, and what's in your mouth.</summary>
    public sealed class LootHud : MonoBehaviour
    {
        GUIStyle _title, _line, _clock, _pop;
        byte _lastSerial;
        float _popAt = -10f;
        int _popValue;
        Vector3 _popWorld;

        static int? LocalPlayer
        {
            get
            {
                if (!SimulationAuthority.IsOnline)
                    return Squad.SquadController.Instance ? Squad.SquadController.Instance.ActivePlayerId
                         : TrenchcoatController.Instance ? TrenchcoatController.Instance.LocalPlayerId : (int?)null;
                return NetworkedRaccoon.LocalOwned ? NetworkedRaccoon.LocalOwned.Controller.PlayerId : (int?)null;
            }
        }

        void OnGUI()
        {
            var d = LootDirector.Instance;
            if (!d) return;
            UiScale.Apply();
            _title ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
            _line ??= new GUIStyle(GUI.skin.label) { fontSize = 15 };
            _clock ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
            _pop ??= new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            float W = UiScale.Width;
            var s = d.Snapshot;
            var pd = PanicDirector.Instance;
            bool infiltrating = !pd || pd.Phase == RoundPhase.Infiltration;

            if (s.StashSerial != _lastSerial)
            {
                _lastSerial = s.StashSerial;
                _popAt = Time.unscaledTime;
                _popValue = s.LastStashValue;
                _popWorld = s.LastStashAt;
            }

            if (infiltrating)
            {
                // Pocket + objectives (top left).
                GUI.color = new Color(0f, 0f, 0f, 0.5f);
                GUI.DrawTexture(new Rect(12, 50, 270, 118), Texture2D.whiteTexture);
                GUI.color = new Color(1f, 0.85f, 0.3f);
                GUI.Label(new Rect(22, 54, 250, 30), Squad.GameMode.Raccoons ? $"LOOT AT THE DEN  ${s.Total}" : $"POCKET  ${s.Total}", _title);
                GUI.color = Color.white;
                float y = 86;
                foreach (var info in LootCatalog.Objectives)
                {
                    if (!s.IsPicked(info.Id)) continue;
                    bool done = s.IsDone(info.Id);
                    GUI.color = done ? new Color(0.45f, 1f, 0.55f) : Color.white;
                    GUI.Label(new Rect(22, y, 260, 22), $"{(done ? "[GOT IT]" : "[  ]")}  {info.Name}  ${info.Value}", _line);
                    y += 24;
                }
                GUI.color = Color.white;

                // Clock (top right), red in the last minute.
                int secs = Mathf.CeilToInt(s.SecondsLeft);
                GUI.color = secs <= 60 ? new Color(1f, 0.35f, 0.3f) : Color.white;
                GUI.Label(new Rect(W - 182, 50, 170, 30), $"{secs / 60}:{secs % 60:00}", _clock);
                GUI.color = Color.white;
            }

            // The den: always marked, so you know where to bring things.
            if (Squad.GameMode.Raccoons && Camera.main)
            {
                Vector3 dp = Camera.main.WorldToScreenPoint(d.DenCenter + Vector3.up * 2.2f);
                if (dp.z > 0f)
                {
                    Vector2 p = UiScale.FromScreen(dp);
                    GUI.color = new Color(1f, 0.75f, 0.35f);
                    GUI.Label(new Rect(p.x - 70, p.y - 22, 140, 44), "▼ DEN", new GUIStyle(_title) { alignment = TextAnchor.MiddleCenter });
                    GUI.color = Color.white;
                }
            }

            // What's in my mouth.
            int? me = LocalPlayer;
            if (me.HasValue)
            {
                var carried = Squad.CarryDirector.Instance ? Squad.CarryDirector.Instance.ItemOf(me.Value) : null;
                var it = carried ? carried.GetComponent<LootItem>() : null;
                if (it)
                {
                    GUI.color = new Color(1f, 0.85f, 0.3f);
                    GUI.Label(new Rect(0, UiScale.Height - 150, W, 28), $"Carrying: {it.Label} (${it.Value}) — take it to the DEN · click to drop", new GUIStyle(_line) { alignment = TextAnchor.MiddleCenter, fontSize = 18 });
                    GUI.color = Color.white;
                }
            }

            // "+$40" rising from where it went in.
            float age = Time.unscaledTime - _popAt;
            var cam = Camera.main;
            if (age < 1.4f && cam)
            {
                Vector3 sp = cam.WorldToScreenPoint(_popWorld + Vector3.up * (0.4f + age * 0.6f));
                if (sp.z > 0f)
                {
                    Vector2 p = UiScale.FromScreen(sp);
                    GUI.color = new Color(1f, 0.85f, 0.3f, 1f - age / 1.4f);
                    GUI.Label(new Rect(p.x - 80, p.y - 20, 160, 40), $"+${_popValue}", _pop);
                    GUI.color = Color.white;
                }
            }
        }
    }
}
