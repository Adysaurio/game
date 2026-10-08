using System.Collections.Generic;
using System.Linq;
using TrashPandas.Core.Raccoons;
using TrashPandas.Runtime.Grabbing;
using TrashPandas.Runtime.Net;
using TrashPandas.Runtime.Raccoon;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>
    /// Raccoons carrying things: small things one at a time in the mouth, heavy things (cake, giant gift) by
    /// two or more at once. The host (or offline) decides; the raccoon's owner asks with what it sees. Every
    /// machine glues carried things to the mouths it sees, with collisions off.
    /// </summary>
    public sealed class CarryDirector : NetworkBehaviour
    {
        public float HeavyCarrierSpeed = 0.7f;
        public float StrainSpeed = 0.35f;
        public float MaxReportedLag = 2f;

        public static CarryDirector Instance { get; private set; }

        readonly NetworkVariable<CarrySnapshot> _net = new NetworkVariable<CarrySnapshot>();
        CarrySnapshot _offline;
        public CarrySnapshot Snapshot => SimulationAuthority.IsOnline ? _net.Value : _offline;

        /// <summary>Every grabbable in the scene, in the same (name) order on every machine.</summary>
        public IReadOnlyList<Grabbable> Items => _items;
        readonly List<Grabbable> _items = new List<Grabbable>();
        readonly Dictionary<int, int> _carry = new Dictionary<int, int>();               // player → item index (host)
        readonly Dictionary<int, RaccoonController> _raccoons = new Dictionary<int, RaccoonController>();
        readonly HashSet<Grabbable> _gluedHere = new HashSet<Grabbable>();
        readonly HashSet<Grabbable> _gluedNow = new HashSet<Grabbable>();
        readonly List<Vector3> _mouths = new List<Vector3>();

        void Awake() => Instance = this;
        public override void OnDestroy() { if (Instance == this) Instance = null; base.OnDestroy(); }

        void Start()
        {
            _items.AddRange(FindObjectsByType<Grabbable>(FindObjectsInactive.Include, FindObjectsSortMode.None).OrderBy(g => g.name));
            foreach (var g in _items) if (!g.GetComponent<ImpactNoise>()) g.gameObject.AddComponent<ImpactNoise>();
        }

        public int IndexOf(Grabbable g) => g ? _items.IndexOf(g) : -1;
        public Grabbable ItemOf(int player) { int i = Snapshot.ItemOf(player); return i >= 0 && i < _items.Count ? _items[i] : null; }
        public bool IsCarrying(int player) => Snapshot.ItemOf(player) >= 0;

        // --- Host / offline -------------------------------------------------------------------------

        /// <summary>Quick click: drop what you carry, or pick up the highlighted thing.</summary>
        public void Tap(RaccoonController raccoon, int pickedIndex, Vector3 ownerPosition)
        {
            if (!raccoon || raccoon.Frozen) return;
            int p = raccoon.PlayerId;
            if (_carry.ContainsKey(p)) { Drop(p, Vector3.zero); return; }
            var panic = Panic.PanicDirector.Instance;
            bool infiltrating = !panic || panic.Phase == Panic.RoundPhase.Infiltration;
            if (!Core.Panic.MouthRules.MayPickUp(infiltrating, panic ? panic.StatusOf(p) : Core.Panic.PlayerOutcome.None)) return;
            if (pickedIndex < 0 || pickedIndex >= _items.Count) return;
            var g = _items[pickedIndex];
            if (!g || !g.isActiveAndEnabled) return;
            Vector3 at = Vector3.Distance(ownerPosition, raccoon.transform.position) <= MaxReportedLag ? ownerPosition : raccoon.transform.position;
            if (Vector3.Distance(g.transform.position, GrabHighlight.MouthOf(raccoon.transform, at)) > GrabPick.Reach + 0.3f) return;
            if (!g.RequiresBothHands && g.IsHeld) return;          // someone already has it in their mouth
            if (g.RequiresBothHands && CarriersOf(pickedIndex) >= TowerRules.MaxHeight) return;
            _carry[p] = pickedIndex;
            g.IsHeld = true;
            foreach (var c in g.Colliders) c.enabled = false;
            if (!g.RequiresBothHands) g.Body.isKinematic = true;
        }

        /// <summary>Hold and release: fling it where the camera looks.</summary>
        public void Throw(RaccoonController raccoon, Vector3 direction, float strength)
        {
            if (!raccoon || !_carry.ContainsKey(raccoon.PlayerId)) return;
            var g = _items[_carry[raccoon.PlayerId]];
            if (g.RequiresBothHands) { Drop(raccoon.PlayerId, Vector3.zero); return; } // you can't throw the cake
            Drop(raccoon.PlayerId, direction.normalized * Mathf.Lerp(3f, 10f, strength) + Vector3.up * 2.5f);
        }

        /// <summary>Let go (caught, a second click, pulled apart). Heavy things fall when fewer than two hold them.</summary>
        public void Drop(int player, Vector3 velocity)
        {
            if (!_carry.TryGetValue(player, out int index)) return;
            _carry.Remove(player);
            var g = _items[index];
            if (!g) return;
            if (g.RequiresBothHands && CarriersOf(index) > 0) return; // the others still hold it
            g.IsHeld = false;
            g.Body.isKinematic = false;
            foreach (var c in g.Colliders) c.enabled = true;
            g.Body.linearVelocity = velocity;
        }

        /// <summary>The item leaves the game (delivered at the den, or out through an exit with its raccoon).</summary>
        public Grabbable Consume(int player)
        {
            if (!_carry.TryGetValue(player, out int index)) return null;
            var g = _items[index];
            if (g.RequiresBothHands)
            {
                foreach (var p in _carry.Where(kv => kv.Value == index).Select(kv => kv.Key).ToList()) _carry.Remove(p);
            }
            else _carry.Remove(player);
            g.IsHeld = false;
            return g;
        }

        public int CarriersOf(int index) => _carry.Count(kv => kv.Value == index);
        public IEnumerable<int> CarriersOfItem(int index) => _carry.Where(kv => kv.Value == index).Select(kv => kv.Key);

        void Update()
        {
            if (!SimulationAuthority.IsSimulating || (SimulationAuthority.IsOnline && !IsSpawned)) { ApplySpeeds(); return; }
            _raccoons.Clear();
            foreach (var r in FindObjectsByType<RaccoonController>(FindObjectsSortMode.None)) if (r && r.PlayerId >= 0) _raccoons[r.PlayerId] = r;

            // Carriers that vanished or got caught let go.
            foreach (var p in _carry.Keys.ToList())
                if (!_raccoons.TryGetValue(p, out var r) || r.Frozen) Drop(p, Vector3.zero);

            // Heavy things: lifted by two or more, pulled apart = dropped, alone = it stays where it is.
            foreach (var index in _carry.Values.Distinct().ToList())
            {
                var g = _items[index];
                if (!g.RequiresBothHands) continue;
                var mouths = CarriersOfItem(index).Select(p => GrabHighlight.MouthOf(_raccoons[p].transform)).ToList();
                if (HeavyCarry.ShouldDrop(mouths))
                {
                    foreach (var p in CarriersOfItem(index).ToList()) Drop(p, Vector3.zero);
                    continue;
                }
                bool lifted = HeavyCarry.Lifted(mouths.Count);
                g.Body.isKinematic = true; // lifted: placed at the mouths; one alone: it stays put
                foreach (var c in g.Colliders) c.enabled = !lifted;
                if (lifted) g.transform.position = HeavyCarry.Anchor(mouths) + Vector3.up * 0.25f;
                else
                {
                    // One raccoon straining at it: if it walks off, it lets go.
                    var only = CarriersOfItem(index).First();
                    if (Vector3.Distance(GrabHighlight.MouthOf(_raccoons[only].transform), g.transform.position) > GrabPick.Reach + 0.4f) Drop(only, Vector3.zero);
                }
            }

            // Heavy things at rest don't budge (a raccoon walking into the cake must not shove it along).
            for (int i = 0; i < _items.Count; i++)
            {
                var g = _items[i];
                if (!g || !g.RequiresBothHands || CarriersOf(i) > 0 || g.Body.isKinematic) continue;
                if (g.Body.linearVelocity.sqrMagnitude < 0.01f && Time.timeSinceLevelLoad > 1f) g.Body.isKinematic = true;
            }

            var snap = new CarrySnapshot();
            for (int p = 0; p < CarrySnapshot.Max; p++)
            {
                bool has = _carry.TryGetValue(p, out int index);
                snap.Set(p, has ? index : -1, has && _items[index].RequiresBothHands && CarriersOf(index) >= HeavyCarry.Needed);
            }
            _offline = snap;
            if (IsSpawned && IsServer && !snap.Equals(_net.Value)) _net.Value = snap;
            ApplySpeeds();
        }

        /// <summary>Every machine: your own raccoon slows down under a heavy load (owners move their raccoons).</summary>
        void ApplySpeeds()
        {
            var s = Snapshot;
            foreach (var r in FindObjectsByType<RaccoonController>(FindObjectsSortMode.None))
            {
                if (!r || r.PlayerId < 0) continue;
                var g = ItemOf(r.PlayerId);
                r.CarryFactor = g && g.RequiresBothHands ? (s.Lifted(r.PlayerId) ? HeavyCarrierSpeed : StrainSpeed) : 1f;
                r.CarryingHeavy = g && g.RequiresBothHands;
            }
        }

        readonly int[] _lastCarried = { -1, -1, -1, -1, -1 };

        void PlayCarrySounds()
        {
            var s = Snapshot;
            for (int p = 0; p < CarrySnapshot.Max; p++)
            {
                int now = s.ItemOf(p);
                if (now == _lastCarried[p]) continue;
                var g = now >= 0 ? (now < _items.Count ? _items[now] : null) : (_lastCarried[p] >= 0 && _lastCarried[p] < _items.Count ? _items[_lastCarried[p]] : null);
                if (g) Ui.Sfx.Play(now >= 0 ? Ui.Sound.Grab : Ui.Sound.Throw, g.transform.position, now >= 0 ? 0.8f : 0.5f);
                _lastCarried[p] = now;
            }
        }

        /// <summary>Every machine: small things sit in the mouth as this machine sees the raccoon, no collisions.</summary>
        void LateUpdate()
        {
            PlayCarrySounds();
            _gluedNow.Clear();
            var s = Snapshot;
            foreach (var r in FindObjectsByType<RaccoonController>(FindObjectsSortMode.None))
            {
                if (!r || r.PlayerId < 0) continue;
                var g = ItemOf(r.PlayerId);
                if (!g || g.RequiresBothHands) continue;
                g.transform.SetPositionAndRotation(GrabHighlight.MouthOf(r.transform), r.transform.rotation);
                foreach (var c in g.Colliders) if (c) c.enabled = false;
                _gluedNow.Add(g);
            }
            // Heavy loads in the air: at the carriers' mouths as this machine sees them, no collisions.
            for (int index = 0; index < _items.Count; index++)
            {
                var g = _items[index];
                if (!g || !g.RequiresBothHands) continue;
                _mouths.Clear();
                bool lifted = false;
                foreach (var r in FindObjectsByType<RaccoonController>(FindObjectsSortMode.None))
                    if (r && r.PlayerId >= 0 && s.ItemOf(r.PlayerId) == index) { _mouths.Add(GrabHighlight.MouthOf(r.transform)); lifted |= s.Lifted(r.PlayerId); }
                if (!lifted || _mouths.Count == 0) continue;
                g.transform.position = HeavyCarry.Anchor(_mouths) + Vector3.up * 0.25f;
                foreach (var c in g.Colliders) if (c) c.enabled = false;
                _gluedNow.Add(g);
            }
            foreach (var g in _gluedHere)
                if (g && !_gluedNow.Contains(g) && !g.IsHeld && g.isActiveAndEnabled)
                    foreach (var c in g.Colliders) if (c) c.enabled = true;
            _gluedHere.Clear();
            foreach (var g in _gluedNow) _gluedHere.Add(g);
        }
    }

    /// <summary>Host: things that hit the ground hard make noise.</summary>
    public sealed class ImpactNoise : MonoBehaviour
    {
        float _quietUntil;
        void OnCollisionEnter(Collision c)
        {
            if (!SimulationAuthority.IsSimulating || Time.timeSinceLevelLoad < 2f || Time.time < _quietUntil) return;
            if (c.relativeVelocity.magnitude < 3f) return;
            // A plate to the face: a panicked human is dazed for a moment (the co-op answer to a chase).
            var pawn = c.collider.GetComponentInParent<Npc.NpcPawn>();
            if (pawn && Panic.PanicDirector.Instance && Panic.PanicDirector.Instance.Phase == Panic.RoundPhase.Panic)
                Panic.PanicDirector.Instance.StunChaser(pawn);
            _quietUntil = Time.time + 0.5f;
            NoiseBus.Emit(NoiseKind.Crash, transform.position);
            Ui.Sfx.Play(Ui.Sound.Land, transform.position, 0.7f);
        }
    }
}
