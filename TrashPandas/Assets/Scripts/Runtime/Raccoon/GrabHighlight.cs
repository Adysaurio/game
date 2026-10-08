using System.Collections.Generic;
using TrashPandas.Core.Grabbing;
using TrashPandas.Core.Raccoons;
using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Grabbing;
using TrashPandas.Runtime.Ui;
using UnityEngine;

namespace TrashPandas.Runtime.Raccoon
{
    /// <summary>
    /// What your raccoon would grab right now: the grabbable nearest the center of the view, within reach of
    /// the mouth. It glows and gets a "▼" above it. Local only.
    /// </summary>
    public sealed class GrabHighlight : MonoBehaviour
    {
        public static GrabHighlight Instance { get; private set; }
        public Grabbable Current { get; private set; }

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        readonly List<GrabCandidate> _candidates = new List<GrabCandidate>();
        MaterialPropertyBlock _block;
        Renderer[] _lit;
        GUIStyle _arrow;

        void Awake() => _block = new MaterialPropertyBlock();
        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; Clear(); }

        public static Vector3 MouthOf(Transform raccoon) => MouthOf(raccoon, raccoon.position);
        public static Vector3 MouthOf(Transform raccoon, Vector3 position) => position + raccoon.forward * 0.28f + Vector3.up * 0.32f;

        /// <summary>Recompute for the raccoon this machine controls (null = nothing to highlight).</summary>
        public Grabbable Refresh(PlayerCameraRig rig, RaccoonController raccoon, bool carrying)
        {
            Grabbable pick = null;
            if (raccoon && rig && !carrying && !raccoon.Frozen)
            {
                var cam = rig.OutputCamera.transform;
                _candidates.Clear();
                var all = Grabbable.All;
                for (int i = 0; i < all.Count; i++)
                    if (all[i] && (!all[i].IsHeld || all[i].RequiresBothHands)) _candidates.Add(new GrabCandidate { Id = i, Position = all[i].transform.position });
                int? id = GrabPick.Pick(cam.position, cam.forward, MouthOf(raccoon.transform), _candidates);
                pick = id.HasValue ? all[id.Value] : null;
            }
            if (pick != Current) { Clear(); Current = pick; Light(); }
            return Current;
        }

        void Light()
        {
            if (!Current) return;
            _lit = Current.GetComponentsInChildren<Renderer>();
            foreach (var r in _lit)
            {
                if (!r || !r.sharedMaterial || !r.sharedMaterial.HasProperty(BaseColor)) continue;
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColor, Color.Lerp(r.sharedMaterial.GetColor(BaseColor), new Color(1f, 0.9f, 0.3f), 0.55f));
                r.SetPropertyBlock(_block);
            }
        }

        void Clear()
        {
            if (_lit != null) foreach (var r in _lit) if (r) r.SetPropertyBlock(null);
            _lit = null;
            Current = null;
        }

        void OnGUI()
        {
            if (!Current) return;
            var cam = Camera.main;
            if (!cam) return;
            Vector3 sp = cam.WorldToScreenPoint(Current.transform.position + Vector3.up * 0.35f);
            if (sp.z <= 0f) return;
            UiScale.Apply();
            Vector2 p = UiScale.FromScreen(sp + Vector3.up * Mathf.Sin(Time.time * 6f) * 4f);
            _arrow ??= new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            GUI.color = new Color(1f, 0.85f, 0.25f);
            GUI.Label(new Rect(p.x - 20, p.y - 20, 40, 40), "▼", _arrow);
            GUI.color = Color.white;
        }
    }
}
