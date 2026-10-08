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
            if (r.Crawling) return true; // inside a pipe
            if (DebugParked(r)) return true;
            if (!ReferenceEquals(r.Mount, null) || r.RidersAbove > 0) return false; // a tower sticks out of anything
            Vector3 p = r.transform.position + Vector3.up * 0.1f;
            foreach (var h in s_all)
            {
                if (!h || !h.Contains(p)) continue;
                // A trash can only hides you if you crouch into it; bushes and tablecloths hide you anyway.
                if (h.Kind == HidingKind.TrashCan && !r.IsSneakingOrRemote) continue;
                return true;
            }
            return false;
        }

        public static HidingKind? SpotOf(RaccoonController r)
        {
            if (!r) return null;
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
