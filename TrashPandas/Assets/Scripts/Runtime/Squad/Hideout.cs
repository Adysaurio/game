using System.Collections;
using System.Collections.Generic;
using TrashPandas.Runtime.Raccoon;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>
    /// Somewhere you hop *into* (Fortnite-style): E to go in, look around freely from inside, E or Space to hop
    /// out. If a human saw you go in, they search it and you tumble out. Trash cans and bushes.
    /// </summary>
    public abstract class Hideout : MonoBehaviour
    {
        public const float EnterRadius = 1.1f;
        public RaccoonController Occupant { get; private set; }
        public abstract string Prompt { get; }
        /// <summary>Stable id (same on every machine): name + where it stands.</summary>
        public int Id => Animator.StringToHash(name + transform.position.ToString("F1"));

        static readonly List<Hideout> s_all = new List<Hideout>();
        public static IReadOnlyList<Hideout> All => s_all;
        protected virtual void OnEnable() { s_all.RemoveAll(h => !h); if (!s_all.Contains(this)) s_all.Add(this); }
        protected virtual void OnDisable() => s_all.Remove(this);

        public static Hideout Near(Vector3 p)
        {
            Hideout best = null;
            float bestD = EnterRadius + 0.6f;
            foreach (var h in s_all)
            {
                if (!h || h.Occupant) continue;
                Vector3 d = h.transform.position - p;
                d.y = 0f;
                float reach = EnterRadius + h.ExtraReach;
                if (d.magnitude <= reach && d.magnitude < bestD) { bestD = d.magnitude; best = h; }
            }
            return best;
        }

        /// <summary>Bigger hideouts (bushes) can be entered from a bit further away.</summary>
        protected virtual float ExtraReach => 0f;
        protected virtual Vector3 InsidePoint => transform.position + Vector3.up * 0.25f;
        protected virtual Vector3 ExitPoint(bool kicked) => transform.position + transform.forward * (kicked ? 1.6f : 1.0f) + Vector3.up * 0.05f;
        protected virtual IEnumerator OpenFx(bool fast) { yield break; }
        protected virtual IEnumerator CloseFx() { yield break; }
        protected virtual void OnEnteredFx() { }
        /// <summary>Called while someone hides inside and swings the camera around: a little give-away.</summary>
        public virtual void Wiggle() { }

        public IEnumerator HopIn(RaccoonController r)
        {
            Occupant = r;
            Ui.Sfx.Play(Ui.Sound.Grab, transform.position, 0.6f);
            yield return OpenFx(false);
            Vector3 from = r.transform.position, to = InsidePoint;
            for (float t = 0f; t < 0.35f; t += Time.deltaTime)
            {
                float k = t / 0.35f;
                r.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.9f;
                yield return null;
            }
            r.transform.position = to;
            SetVisible(r, false);
            OnEnteredFx();
            Ui.Sfx.Play(Ui.Sound.Land, transform.position, 0.6f);
            yield return CloseFx();
        }

        public IEnumerator HopOut(RaccoonController r, bool kicked)
        {
            Ui.Sfx.Play(kicked ? Ui.Sound.Hit : Ui.Sound.Grab, transform.position, 0.8f);
            yield return OpenFx(kicked);
            SetVisible(r, true);
            OnEnteredFx();
            Vector3 from = transform.position + Vector3.up * 0.6f, to = ExitPoint(kicked);
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                float k = t / 0.3f;
                r.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.7f;
                yield return null;
            }
            r.TeleportTo(to);
            r.LandedOutOfCan(kicked);
            Occupant = null;
            yield return CloseFx();
        }

        static void SetVisible(RaccoonController r, bool on)
        {
            foreach (var rend in r.GetComponentsInChildren<Renderer>()) if (!(rend is ParticleSystemRenderer)) rend.enabled = on;
        }
    }
}
