using System.Collections.Generic;
using System.Linq;
using TrashPandas.Core.Loot;
using TrashPandas.Core.Round;
using TrashPandas.Runtime.Grabbing;
using TrashPandas.Runtime.Net;
using TrashPandas.Runtime.Npc;
using TrashPandas.Runtime.Panic;
using TrashPandas.Runtime.Raccoon;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.Runtime.Loot
{
    /// <summary>
    /// Sets up each wedding (where the loot lies, which 3 objectives), watches the arms put things in the coat's
    /// pocket, and runs the infiltration clock. Simulated on the host (or offline); clients get a snapshot.
    /// </summary>
    public sealed class LootDirector : NetworkBehaviour
    {
        [Tooltip("Where loose loot can appear (more spots than items).")]
        public Vector3[] LootSpots = new Vector3[0];
        [Tooltip("Possible spots per objective, flattened: ObjectiveSpotIds[i] is the objective of ObjectiveSpotPositions[i].")]
        public ObjectiveId[] ObjectiveSpotIds = new ObjectiveId[0];
        public Vector3[] ObjectiveSpotPositions = new Vector3[0];
        public int ObjectiveCount = 3;
        public float InfiltrationSeconds = 480f;

        public static LootDirector Instance { get; private set; }
        public static int? SeedOverride; // dev automation

        readonly NetworkVariable<LootSnapshot> _net = new NetworkVariable<LootSnapshot>();
        LootSnapshot _offline;
        public LootSnapshot Snapshot => SimulationAuthority.IsOnline ? _net.Value : _offline;

        public readonly LootPocket Pocket = new LootPocket();
        public IReadOnlyList<LootItem> Items => _items;
        public System.Random Random { get; private set; }

        readonly List<LootItem> _items = new List<LootItem>();
        readonly Dictionary<LootItem, StashGesture> _gestures = new Dictionary<LootItem, StashGesture>();
        InfiltrationClock _clock;
        bool _setUp;

        void OnEnable() => Instance = this;
        void OnDisable() { if (Instance == this) Instance = null; }

        void Start()
        {
            _items.AddRange(FindObjectsByType<LootItem>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(i => i.name));
        }

        public int IdOf(LootItem item) => _items.IndexOf(item);

        void SetUpRound()
        {
            _setUp = true;
            Random = new System.Random(SeedOverride ?? System.Environment.TickCount);
            Pocket.Reset();
            _clock = new InfiltrationClock(InfiltrationSeconds);
            _clock.Begin(Time.time);

            var loose = _items.Where(i => !i.IsObjective).ToList();
            var spots = RoundSetup.PickLootSpots(LootSpots, loose.Count, Random);
            for (int i = 0; i < loose.Count; i++)
            {
                if (i < spots.Count) { loose[i].PlaceAt(spots[i]); loose[i].SetState(LootState.Active); }
                else loose[i].SetState(LootState.Unused);
            }

            var candidates = new List<ObjectiveSpots>();
            foreach (var info in LootCatalog.Objectives)
            {
                var mine = new List<Vector3>();
                for (int i = 0; i < ObjectiveSpotIds.Length && i < ObjectiveSpotPositions.Length; i++)
                    if (ObjectiveSpotIds[i] == info.Id) mine.Add(ObjectiveSpotPositions[i]);
                if (mine.Count > 0 && _items.Any(it => it.IsObjective && it.Objective == info.Id))
                    candidates.Add(new ObjectiveSpots { Id = info.Id, Spots = mine.ToArray() });
            }
            var picked = RoundSetup.PickObjectives(candidates, ObjectiveCount, Random);
            byte mask = 0;
            foreach (var item in _items.Where(i => i.IsObjective))
            {
                var p = picked.FirstOrDefault(x => x.Id == item.Objective);
                bool on = picked.Any(x => x.Id == item.Objective);
                if (on) { item.PlaceAt(p.Spot); item.SetState(LootState.Active); mask |= (byte)(1 << (int)item.Objective); }
                else item.SetState(LootState.Unused);
            }
            _offline.ObjectivesPicked = mask;
        }

        void Update()
        {
            if (!SimulationAuthority.IsSimulating) return;
            if (SimulationAuthority.IsOnline && !IsSpawned) return;
            if (!_setUp) { if (_items.Count == 0) return; SetUpRound(); }

            float now = Time.time;
            var panic = PanicDirector.Instance;
            bool infiltrating = !panic || panic.Phase == RoundPhase.Infiltration;
            if (infiltrating)
            {
                _offline.SecondsLeft = _clock.SecondsLeft(now);
                float push = _clock.OvertimeSuspicion(now, Time.deltaTime);
                if (push > 0f && SuspicionDirector.Instance) SuspicionDirector.Instance.AdjustSuspicion(push);
                WatchStashing();
            }
            UpdateMouths(infiltrating);
            Publish();
        }

        /// <summary>Arms holding loot against the chest for a moment put it in the pocket.</summary>
        void WatchStashing()
        {
            var coat = SuspicionDirector.Instance ? SuspicionDirector.Instance.Coat : null;
            if (!coat || !coat.gameObject.activeInHierarchy) return;
            var grabber = coat.GetComponent<HandGrabber>();
            if (!grabber) return;

            Vector3 chest = coat.ChestWorld;
            foreach (var g in new[] { grabber.HeldLeft, grabber.HeldRight, grabber.HeldBoth })
            {
                if (!g) continue;
                var item = g.GetComponent<LootItem>();
                if (!item || item.State != LootState.Active) continue;
                if (!_gestures.TryGetValue(item, out var gesture)) _gestures[item] = gesture = new StashGesture();
                if (!gesture.Tick(Vector3.Distance(g.transform.position, chest), Time.deltaTime)) continue;
                grabber.Drop(g);
                Stash(item, chest);
            }
            // Anything not being held starts its gesture over.
            foreach (var pair in _gestures)
                if (pair.Key && pair.Key.Grabbable != grabber.HeldLeft && pair.Key.Grabbable != grabber.HeldRight && pair.Key.Grabbable != grabber.HeldBoth)
                    pair.Value.Reset();
        }

        /// <summary>Host/offline: an item goes in the pocket (from the arms, or a raccoon climbing back in with it).</summary>
        public bool Stash(LootItem item, Vector3 at)
        {
            if (!Pocket.Add(IdOf(item), item.Value, item.IsObjective ? item.Objective : (ObjectiveId?)null)) return false;
            item.SetState(LootState.Stashed);
            _offline.Total = Pocket.Total;
            if (item.IsObjective) _offline.ObjectivesDone |= (byte)(1 << (int)item.Objective);
            _offline.StashSerial++;
            _offline.LastStashValue = (short)item.Value;
            _offline.LastStashAt = at;
            return true;
        }

        // --- The raccoon's mouth: one item at a time --------------------------------------------------
        public const float MouthReach = 0.9f;
        public const float MaxReportedLag = 2f;
        readonly Dictionary<int, LootItem> _mouth = new Dictionary<int, LootItem>();
        readonly Dictionary<int, RaccoonController> _raccoons = new Dictionary<int, RaccoonController>();

        public LootItem MouthOf(int player) => _mouth.TryGetValue(player, out var i) ? i : null;

        /// <summary>Host/offline: click as a raccoon — pick up the nearest loot, or spit out what you carry.</summary>
        public void ToggleMouth(RaccoonController raccoon) => ToggleMouth(raccoon, raccoon ? raccoon.transform.position : Vector3.zero);

        /// <param name="reportedPosition">Where the raccoon's owner sees it. Online the host's copy lags behind,
        /// so the owner's position is trusted if it's plausibly close to what the host sees.</param>
        public void ToggleMouth(RaccoonController raccoon, Vector3 reportedPosition)
        {
            if (!raccoon || raccoon.Frozen) return;
            int p = raccoon.PlayerId;
            // Online the host never sees a client's raccoon as Frozen: ask the round instead.
            var panic = PanicDirector.Instance;
            bool infiltrating = !panic || panic.Phase == RoundPhase.Infiltration;
            if (!Core.Panic.MouthRules.MayPickUp(infiltrating, panic ? panic.StatusOf(p) : Core.Panic.PlayerOutcome.None) && !_mouth.ContainsKey(p)) return;
            if (_mouth.ContainsKey(p)) { DropMouth(p); return; }
            Vector3 at = Vector3.Distance(reportedPosition, raccoon.transform.position) <= MaxReportedLag ? reportedPosition : raccoon.transform.position;
            LootItem best = null;
            float bestD = MouthReach;
            foreach (var item in _items)
            {
                if (item.State != LootState.Active || item.Grabbable.IsHeld) continue;
                float d = Vector3.Distance(item.transform.position, at + Vector3.up * 0.3f);
                if (d < bestD) { bestD = d; best = item; }
            }
            if (!best) return;
            var g = best.Grabbable;
            g.IsHeld = true;
            g.Body.isKinematic = true;
            foreach (var c in g.Colliders) c.enabled = false;
            _mouth[p] = best;
        }

        /// <summary>Host/offline: spit it out where the raccoon stands (caught, or a second click).</summary>
        public void DropMouth(int player)
        {
            if (!_mouth.TryGetValue(player, out var item)) return;
            _mouth.Remove(player);
            if (!item) return;
            var g = item.Grabbable;
            g.IsHeld = false;
            foreach (var c in g.Colliders) c.enabled = true;
            g.Body.isKinematic = false;
        }

        /// <summary>Host/offline: the raccoon got out through an exit with something in its mouth.</summary>
        public void OnEscaped(int player, Core.Panic.RoundPayout payout)
        {
            if (!_mouth.TryGetValue(player, out var item)) return;
            _mouth.Remove(player);
            if (!item) return;
            payout.Carry(player, item.Value, item.IsObjective ? item.Objective : (ObjectiveId?)null);
            if (item.IsObjective) _offline.ObjectivesDone |= (byte)(1 << (int)item.Objective);
            item.Grabbable.IsHeld = false;
            item.SetState(LootState.Stashed); // it left with the raccoon
        }

        void UpdateMouths(bool infiltrating)
        {
            if (_mouth.Count == 0) { ClearMouthSnapshot(); return; }
            _raccoons.Clear();
            foreach (var r in FindObjectsByType<RaccoonController>(FindObjectsSortMode.None)) if (r && r.PlayerId >= 0) _raccoons[r.PlayerId] = r;
            foreach (var p in _mouth.Keys.ToList())
            {
                var item = _mouth[p];
                if (!item) { _mouth.Remove(p); continue; }
                if (_raccoons.TryGetValue(p, out var r))
                {
                    var t = r.transform;
                    item.transform.SetPositionAndRotation(t.position + t.forward * 0.28f + Vector3.up * 0.32f, t.rotation);
                    continue;
                }
                // The raccoon is gone. During the infiltration that means it climbed back into the coat: pocket it.
                _mouth.Remove(p);
                item.Grabbable.IsHeld = false;
                if (infiltrating) Stash(item, item.transform.position);
                else item.SetState(LootState.Stashed);
            }
            ClearMouthSnapshot();
            foreach (var pair in _mouth) _offline.SetMouth(pair.Key, _items.IndexOf(pair.Value));
        }

        readonly HashSet<LootItem> _carriedHere = new HashSet<LootItem>();
        readonly HashSet<LootItem> _carriedNow = new HashSet<LootItem>();

        /// <summary>
        /// On every machine (host and clients): carried items sit in their raccoon's mouth as this machine sees
        /// the raccoon, with no collisions. Without this a client sees the item trail behind (two network hops)
        /// and its still-solid collider can shove the raccoon.
        /// </summary>
        void LateUpdate()
        {
            _carriedNow.Clear();
            var s = Snapshot;
            foreach (var r in FindObjectsByType<RaccoonController>(FindObjectsSortMode.None))
            {
                if (!r || r.PlayerId < 0) continue;
                int index = s.MouthItemOf(r.PlayerId);
                if (index < 0 || index >= _items.Count || !_items[index]) continue;
                var item = _items[index];
                var t = r.transform;
                item.transform.SetPositionAndRotation(t.position + t.forward * 0.28f + Vector3.up * 0.32f, t.rotation);
                foreach (var c in item.Grabbable.Colliders) if (c) c.enabled = false;
                _carriedNow.Add(item);
            }
            // Items that just left a mouth get their collisions back (if they're still in play).
            foreach (var item in _carriedHere)
                if (item && !_carriedNow.Contains(item) && item.State == LootState.Active && !item.Grabbable.IsHeld)
                    foreach (var c in item.Grabbable.Colliders) if (c) c.enabled = true;
            _carriedHere.Clear();
            foreach (var item in _carriedNow) _carriedHere.Add(item);
        }

        void ClearMouthSnapshot() { for (int p = 0; p < 5; p++) _offline.SetMouth(p, -1); }

        void Publish()
        {
            if (IsSpawned && IsServer && !_offline.Equals(_net.Value)) _net.Value = _offline;
        }
    }
}
