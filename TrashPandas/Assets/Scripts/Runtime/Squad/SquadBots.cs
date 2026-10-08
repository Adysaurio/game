using TrashPandas.Runtime.Grabbing;
using TrashPandas.Runtime.Loot;
using TrashPandas.Runtime.Raccoon;
using UnityEngine;
using UnityEngine.AI;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>Dev automation for concept v2: simple raccoon bots that steer along the navmesh.</summary>
    public static class SquadBots
    {
        static NavMeshPath s_path;
        public static Vector3? DenTarget; // set by the den when it exists

        /// <summary>Direction along the navmesh from <paramref name="from"/> toward <paramref name="to"/>.</summary>
        public static Vector2 Steer(Vector3 from, Vector3 to)
        {
            Vector3 direct = to - from;
            direct.y = 0f;
            s_path ??= new NavMeshPath();
            if (NavMesh.SamplePosition(to, out var goal, 2f, NavMesh.AllAreas) && NavMesh.SamplePosition(from, out var me, 1.5f, NavMesh.AllAreas)
                && NavMesh.CalculatePath(me.position, goal.position, NavMesh.AllAreas, s_path) && s_path.corners.Length > 1)
            {
                for (int i = 1; i < s_path.corners.Length; i++)
                {
                    Vector3 d = s_path.corners[i] - from;
                    d.y = 0f;
                    if (d.magnitude > 0.35f || i == s_path.corners.Length - 1) { direct = d; break; }
                }
            }
            return direct.sqrMagnitude < 1e-4f ? Vector2.zero : new Vector2(direct.x, direct.z).normalized;
        }

        static LootItem Target(RaccoonController r)
        {
            var ld = LootDirector.Instance;
            if (!ld) return null;
            LootItem best = null;
            foreach (var item in ld.Items)
                if (item && item.State == LootState.Active && !item.IsObjective && !item.Grabbable.IsHeld && item.transform.position.y < 1.2f
                    && (!best || Vector3.Distance(item.transform.position, r.transform.position) < Vector3.Distance(best.transform.position, r.transform.position)))
                    best = item;
            return best;
        }

        public static Vector2? FetchMove(RaccoonController r)
        {
            var carry = CarryDirector.Instance;
            if (carry && carry.IsCarrying(r.PlayerId)) return DenTarget.HasValue ? Steer(r.transform.position, DenTarget.Value) : Vector2.zero;
            var t = Target(r);
            if (!t) return Vector2.zero;
            Vector3 flat = t.transform.position - r.transform.position;
            flat.y = 0f;
            if (flat.magnitude < 0.5f) return Vector2.zero;
            return Steer(r.transform.position, t.transform.position);
        }

        /// <summary>"heavy": raccoons 0 and 1 go to the giant gift, both grab it, and carry it toward the hedge.</summary>
        public static void HeavyTick(System.Collections.Generic.IReadOnlyList<RaccoonController> squad)
        {
            var carry = CarryDirector.Instance;
            Grabbable gift = null;
            if (carry) foreach (var g in carry.Items) if (g && g.name == "Loot_GiantGift") gift = g;
            for (int i = 0; i < squad.Count; i++)
            {
                var r = squad[i];
                if (!r) continue;
                if (i > 1 || !gift || !carry) { r.SetInput(Vector2.zero, false, false, false); continue; }
                bool holding = carry.IsCarrying(r.PlayerId);
                Vector3 side = gift.transform.position + (i == 0 ? Vector3.left : Vector3.right) * 0.75f;
                Vector3 goal = holding ? (carry.Snapshot.Lifted(r.PlayerId) ? new Vector3(i == 0 ? 2.4f : 3.6f, 0f, -6f) : r.transform.position) : side;
                Vector3 flat = goal - r.transform.position;
                flat.y = 0f;
                r.SetInput(flat.magnitude > 0.2f ? Steer(r.transform.position, goal) : Vector2.zero, false, false, false);
                if (!holding && flat.magnitude < 0.35f)
                {
                    r.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(gift.transform.position - r.transform.position, Vector3.up));
                    carry.Tap(r, carry.IndexOf(gift), r.transform.position);
                }
            }
        }

        /// <summary>"tower": raccoon 1 drops onto raccoon 0's head; 0 walks (slower), then runs (the tower falls).</summary>
        public static void TowerTick(System.Collections.Generic.IReadOnlyList<RaccoonController> squad)
        {
            if (squad.Count < 2 || !squad[0] || !squad[1]) return;
            float t = Time.timeSinceLevelLoad;
            var bottom = squad[0];
            var rider = squad[1];
            if (t > 1f && !s_dropped)
            {
                s_dropped = true;
                var cc = rider.GetComponent<CharacterController>();
                cc.enabled = false;
                rider.transform.position = bottom.HeadTop + Vector3.up * 0.4f;
                cc.enabled = true;
                rider.DroppedFromAbove = true;
            }
            for (int i = 2; i < squad.Count; i++) if (squad[i]) squad[i].SetInput(Vector2.zero, false, false, false);
            bool hop = Net.DevAutomation.TowerHop && t > 4f && !s_hopped && rider.Mount;
            if (hop) s_hopped = true;
            rider.SetInput(Vector2.zero, hop, hop, false);
            bool walk = t > 2.5f;
            bool run = t > 6f;
            bottom.SetInput(walk ? new Vector2(0f, -1f) : Vector2.zero, false, false, false, run);
            if (Time.frameCount % 30 == 0)
                Debug.Log($"[TOWER] t={t:F1} bottom={bottom.transform.position:F2} rider={rider.transform.position:F2} mounted={(rider.Mount == bottom)} riders={bottom.RidersAbove}");
        }
        static bool s_dropped, s_hopped;

        // --- Online bots (one per machine) ----------------------------------------------------------
        static bool s_towerDropped;

        /// <summary>"towerclient" drops onto the other raccoon's head; "towerhost" waits, walks (with a rider), then runs.</summary>
        public static Vector2? OnlineTowerMove(RaccoonController me, bool client)
        {
            float t = Time.timeSinceLevelLoad;
            if (client)
            {
                if (!s_towerDropped && t > 12f)
                {
                    RaccoonController other = null;
                    foreach (var r in Object.FindObjectsByType<RaccoonController>(FindObjectsSortMode.None)) if (r != me && r.PlayerId >= 0) other = r;
                    if (other)
                    {
                        s_towerDropped = true;
                        var cc = me.GetComponent<CharacterController>();
                        cc.enabled = false;
                        me.transform.position = other.HeadTop + Vector3.up * 0.4f;
                        cc.enabled = true;
                        me.DroppedFromAbove = true;
                    }
                }
                return Vector2.zero;
            }
            return t > 16f ? new Vector2(0f, 1f) : Vector2.zero;
        }

        public static int GiftIndex
        {
            get
            {
                var carry = CarryDirector.Instance;
                if (carry) for (int i = 0; i < carry.Items.Count; i++) if (carry.Items[i] && carry.Items[i].name == "Loot_GiantGift") return i;
                return -1;
            }
        }

        static Vector3? s_giftSide;
        static Vector3 GiftSide(RaccoonController me)
        {
            var carry = CarryDirector.Instance;
            int i = GiftIndex;
            if (!carry || i < 0) return me.transform.position;
            s_giftSide ??= carry.Items[i].transform.position + (me.PlayerId == 0 ? Vector3.right : Vector3.forward) * 0.75f;
            return s_giftSide.Value;
        }

        /// <summary>"heavyonline": each machine's raccoon goes to its side of the giant gift, grabs it, and walks south with it.</summary>
        public static Vector2? OnlineHeavyMove(RaccoonController me)
        {
            var carry = CarryDirector.Instance;
            if (!carry) return Vector2.zero;
            if (carry.IsCarrying(me.PlayerId))
            {
                if (!carry.Snapshot.Lifted(me.PlayerId)) return Vector2.zero;
                return Time.timeSinceLevelLoad < 40f ? new Vector2(0f, -1f) : Vector2.zero;
            }
            Vector3 flat = GiftSide(me) - me.transform.position;
            flat.y = 0f;
            return flat.magnitude > 0.25f ? Steer(me.transform.position, GiftSide(me)) : Vector2.zero;
        }

        static float s_nextHeavyTap;
        public static bool OnlineHeavyTap(RaccoonController me)
        {
            var carry = CarryDirector.Instance;
            if (!carry || carry.IsCarrying(me.PlayerId) || Time.time < s_nextHeavyTap) return false;
            Vector3 flat = GiftSide(me) - me.transform.position;
            flat.y = 0f;
            if (flat.magnitude > 0.35f) return false;
            s_nextHeavyTap = Time.time + 0.5f;
            return true;
        }

        /// <summary>"rescue": raccoon 0 stands in the open until caught; then we switch to raccoon 1 and walk it to the cage.</summary>
        public static bool RescueTick(System.Collections.Generic.IReadOnlyList<RaccoonController> squad, ref int active)
        {
            var pd = Panic.PanicDirector.Instance;
            return false; // the director hands control over automatically when raccoon 0 is caught
        }

        public static Vector2? RescueMove(RaccoonController r)
        {
            var pd = Panic.PanicDirector.Instance;
            if (!pd || r.PlayerId == 0) return Vector2.zero; // the bait just stands there
            if (pd.Phase != Panic.RoundPhase.Panic) return Vector2.zero;
            Vector3 d = pd.CagePosition - r.transform.position;
            d.y = 0f;
            return d.magnitude < 0.6f ? Vector2.zero : Steer(r.transform.position, pd.CagePosition);
        }

        static float s_nextTap;
        public static bool FetchTap(RaccoonController r)
        {
            var carry = CarryDirector.Instance;
            if (!carry || carry.IsCarrying(r.PlayerId) || Time.time < s_nextTap) return false;
            var h = GrabHighlight.Instance ? GrabHighlight.Instance.Current : null;
            if (!h || !h.GetComponent<LootItem>()) return false;
            s_nextTap = Time.time + 0.5f;
            return true;
        }
    }
}
