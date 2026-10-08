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

            var loose = _items.Where(i => !i.IsObjective && i.Kind != LootKind.GiantGift).ToList();
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

        // --- Carrying is handled by the CarryDirector (v2); here only what loot means for the round ----

        /// <summary>Host/offline: the raccoon got out through an exit carrying something: it counts.</summary>
        public void OnEscaped(int player, Core.Panic.RoundPayout payout)
        {
            var carry = Squad.CarryDirector.Instance;
            var g = carry ? carry.Consume(player) : null;
            var item = g ? g.GetComponent<LootItem>() : null;
            if (!item) { if (g) { g.Body.isKinematic = false; foreach (var c in g.Colliders) c.enabled = true; } return; }
            payout.Carry(player, item.Value, item.IsObjective ? item.Objective : (ObjectiveId?)null);
            if (item.IsObjective) _offline.ObjectivesDone |= (byte)(1 << (int)item.Objective);
            item.SetState(LootState.Stashed); // it left with the raccoon
        }

        /// <summary>Host/offline: caught — whatever it carried falls to the ground.</summary>
        public void DropMouth(int player) => Squad.CarryDirector.Instance?.Drop(player, Vector3.zero);

        void Publish()
        {
            if (IsSpawned && IsServer && !_offline.Equals(_net.Value)) _net.Value = _offline;
        }
    }
}
