using System.Collections.Generic;
using TrashPandas.Runtime.Raccoon;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    public enum HidingKind { Bush, TrashCan, UnderTablecloth }

    /// <summary>
    /// Where a raccoon disappears from human eyes: inside a bush, crouched in a trash can, under a tablecloth.
    /// (Stealth rule: "the player is at their most powerful while hidden — let them get that back".)
    /// </summary>
    public sealed class HidingSpot : MonoBehaviour
    {
        public HidingKind Kind;
        public Vector3 Size = Vector3.one;

        static readonly List<HidingSpot> s_all = new List<HidingSpot>();
        public static IReadOnlyList<HidingSpot> All => s_all;
        void OnEnable() { s_all.RemoveAll(h => !h); if (!s_all.Contains(this)) s_all.Add(this); }
        void OnDisable() => s_all.Remove(this);

        bool Contains(Vector3 p)
        {
            Vector3 local = transform.InverseTransformPoint(p);
            return Mathf.Abs(local.x) <= Size.x * 0.5f && local.y >= -0.2f && local.y <= Size.y && Mathf.Abs(local.z) <= Size.z * 0.5f;
        }

        /// <summary>Is this raccoon invisible to humans right now?</summary>
        public static bool Hides(RaccoonController r)
        {
            if (!r) return false;
            if (r.HiddenInside) return true; // inside a pipe or a trash can
            if (DebugParked(r)) return true;
            if (!ReferenceEquals(r.Mount, null) || r.RidersAbove > 0) return false; // a tower sticks out of anything
            Vector3 p = r.transform.position + Vector3.up * 0.1f;
            foreach (var h in s_all)
            {
                if (!h || !h.Contains(p)) continue;
                return true;
            }
            return false;
        }

        public static HidingSpot At(Vector3 position)
        {
            Vector3 p = position + Vector3.up * 0.1f;
            foreach (var h in s_all) if (h && h.Contains(p)) return h;
            return null;
        }

        float _rustleT = -1f;
        Vector3 _restScale;

        /// <summary>Something moved inside: the bush shakes for a moment.</summary>
        public void Rustle()
        {
            if (_rustleT < 0f) _restScale = transform.localScale;
            _rustleT = 0f;
        }

        void Update()
        {
            if (_rustleT < 0f) return;
            _rustleT += Time.deltaTime;
            float k = Mathf.Exp(-_rustleT * 6f) * Mathf.Sin(_rustleT * 40f) * 0.08f;
            transform.localScale = _restScale + new Vector3(k, -k * 0.5f, k);
            if (_rustleT > 0.6f) { transform.localScale = _restScale; _rustleT = -1f; }
        }

        public static HidingKind? SpotOf(RaccoonController r)
        {
            if (!r) return null;
            if (r.InCan) return HidingKind.TrashCan;
            Vector3 p = r.transform.position + Vector3.up * 0.1f;
            foreach (var h in s_all) if (h && h.Contains(p)) return h.Kind;
            return null;
        }

        /// <summary>Debug squad: the raccoons you're not driving wait unseen, so you can switch with Tab calmly.</summary>
        static bool DebugParked(RaccoonController r)
        {
            var squad = SquadController.Instance;
            if (!squad || squad.Active == r) return false;
            if (!ReferenceEquals(r.Mount, null) || r.RidersAbove > 0) return false;
            var carry = CarryDirector.Instance;
            if (carry && carry.IsCarrying(r.PlayerId)) return false;
            return true;
        }
    }
}
