using System.Collections;
using System.Collections.Generic;
using TrashPandas.Runtime.Raccoon;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>
    /// A trash can you can hide in, Fortnite-dumpster style: E to flip the lid and hop in, look around freely
    /// from inside, E or Space to hop out. If a human saw you go in, they come and kick it.
    /// </summary>
    public sealed class TrashCanHideout : MonoBehaviour
    {
        public Transform Lid;
        public const float EnterRadius = 1.0f;
        public RaccoonController Occupant { get; private set; }
        Quaternion _lidClosed;

        static readonly List<TrashCanHideout> s_all = new List<TrashCanHideout>();
        public static IReadOnlyList<TrashCanHideout> All => s_all;
        void OnEnable() { s_all.RemoveAll(h => !h); if (!s_all.Contains(this)) s_all.Add(this); }
        void OnDisable() => s_all.Remove(this);
        void Awake() { if (Lid) _lidClosed = Lid.localRotation; }

        public static TrashCanHideout Near(Vector3 p)
        {
            foreach (var c in s_all)
            {
                if (!c || c.Occupant) continue;
                Vector3 d = c.transform.position - p;
                d.y = 0f;
                if (d.magnitude <= EnterRadius) return c;
            }
            return null;
        }

        IEnumerator Lid01(float from, float to, float seconds)
        {
            if (!Lid) yield break;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(from, to, t / seconds);
                Lid.localRotation = _lidClosed * Quaternion.Euler(0f, 0f, 110f * k);
                yield return null;
            }
            Lid.localRotation = _lidClosed * Quaternion.Euler(0f, 0f, 110f * to);
        }

        public IEnumerator HopIn(RaccoonController r)
        {
            Occupant = r;
            Ui.Sfx.Play(Ui.Sound.Grab, transform.position, 0.6f);
            yield return Lid01(0f, 1f, 0.18f);
            Vector3 from = r.transform.position, to = transform.position + Vector3.up * 0.25f;
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                float k = t / 0.35f;
                r.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.9f;
                yield return null;
            }
            r.transform.position = to;
            SetVisible(r, false);
            Ui.Sfx.Play(Ui.Sound.Land, transform.position, 0.6f);
            yield return Lid01(1f, 0f, 0.15f);
        }

        public IEnumerator HopOut(RaccoonController r, bool kicked)
        {
            Ui.Sfx.Play(kicked ? Ui.Sound.Hit : Ui.Sound.Grab, transform.position, 0.8f);
            yield return Lid01(0f, 1f, kicked ? 0.08f : 0.15f);
            SetVisible(r, true);
            Vector3 from = transform.position + Vector3.up * 0.6f, to = transform.position + transform.forward * (kicked ? 1.6f : 0.9f) + Vector3.up * 0.05f;
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                float k = t / 0.3f;
                r.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.7f;
                yield return null;
            }
            r.TeleportTo(to);
            r.LandedOutOfCan(kicked);
            Occupant = null;
            yield return Lid01(1f, 0f, 0.2f);
        }

        static void SetVisible(RaccoonController r, bool on)
        {
            foreach (var rend in r.GetComponentsInChildren<Renderer>()) if (!(rend is ParticleSystemRenderer)) rend.enabled = on;
        }
    }
}
