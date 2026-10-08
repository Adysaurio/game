using System.Collections.Generic;
using TrashPandas.Core.Panic;
using TrashPandas.Core.Raccoons;
using TrashPandas.Runtime.Net;
using TrashPandas.Runtime.Npc;
using TrashPandas.Runtime.Raccoon;
using TrashPandas.Runtime.Trenchcoat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TrashPandas.Runtime.Panic
{
    public enum RoundPhase : byte { Infiltration, Panic, Results }

    /// <summary>
    /// RUN!: when suspicion maxes out, bursts the coat, arms the humans, has them chase and whack raccoons
    /// (3 hits = caught), lets raccoons escape through the exits, and ends the round. Simulated on the host
    /// (or offline); clients get the phase and a per-player snapshot.
    /// </summary>
    public sealed class PanicDirector : NetworkBehaviour
    {
        public Vector3[] Exits = new Vector3[0];
        /// <summary>Rings + floating arrows; only visible once RUN! starts.</summary>
        public GameObject[] ExitMarkers = new GameObject[0];
        public string[] ExitNames = new string[0];
        [Tooltip("Zone of each exit: at most one open exit per zone.")]
        public int[] ExitZoneIds = new int[0];
        public int OpenExitCount = 3;
        public Vector3 GardenCenter;
        public float MinExitDistance = 12f;
        [Tooltip("Walking the coat in here with nobody missing = clean exit.")]
        public Vector3 ArchCenter;
        public float ArchRadius = 0f; // 0 = no clean exit in this scene
        public float ExitRadius = 1.3f;
        public float TimeLimit = 90f;
        public float ChaserSpeed = 3.6f;
        public float CatSpeed = 4.6f;
        public float HitImpulse = 5.5f;
        public float CatPushImpulse = 3.5f;
        public Transform Overview;

        public static PanicDirector Instance { get; private set; }

        readonly NetworkVariable<byte> _phase = new NetworkVariable<byte>();
        readonly NetworkVariable<byte> _openExits = new NetworkVariable<byte>(0xFF);
        byte _offlineOpenExits = 0xFF;
        bool _exitsPicked;
        readonly List<Vector3> _openExitPositions = new List<Vector3>();
        public readonly RoundPayout Payout = new RoundPayout();
        bool _cleanExit;
        readonly NetworkVariable<PanicSnapshot> _snapshot = new NetworkVariable<PanicSnapshot>();
        RoundPhase _offlinePhase;
        PanicSnapshot _offlineSnapshot;

        readonly HitTracker _hits = new HitTracker();
        readonly PanicGrace _grace = new PanicGrace(surpriseSeconds: 2f, noHitSeconds: 3f);
        readonly RoundOutcome _outcome = new RoundOutcome();
        readonly List<Chaser> _chasers = new List<Chaser>();
        readonly List<ChaseTarget> _targets = new List<ChaseTarget>();
        readonly Dictionary<int, RaccoonController> _raccoonOf = new Dictionary<int, RaccoonController>();
        readonly Dictionary<int, float> _missingSince = new Dictionary<int, float>();
        readonly List<int> _players = new List<int>();

        sealed class Chaser
        {
            public NpcBrain Brain;
            public ChaserMind Mind = new ChaserMind();
            public PanicWeapon Weapon;
            public bool IsCat => Brain.Pawn.Kind == NpcKind.Cat;
        }

        public PlayerOutcome StatusOf(int player) => _outcome.StatusOf(player);
        public RoundPhase Phase => SimulationAuthority.IsOnline ? (RoundPhase)_phase.Value : _offlinePhase;
        public PanicSnapshot Snapshot => SimulationAuthority.IsOnline ? _snapshot.Value : _offlineSnapshot;
        byte OpenMask => SimulationAuthority.IsOnline ? _openExits.Value : _offlineOpenExits;
        public bool ExitOpen(int i) => (OpenMask & (1 << i)) != 0;

        /// <summary>Positions of this round's open exits.</summary>
        public IReadOnlyList<Vector3> OpenExitPositions
        {
            get
            {
                _openExitPositions.Clear();
                for (int i = 0; i < Exits.Length; i++) if (ExitOpen(i)) _openExitPositions.Add(Exits[i]);
                return _openExitPositions;
            }
        }

        void PickExits()
        {
            _exitsPicked = true;
            var candidates = new TrashPandas.Core.Round.ExitCandidate[Exits.Length];
            for (int i = 0; i < Exits.Length; i++)
                candidates[i] = new TrashPandas.Core.Round.ExitCandidate { Position = Exits[i], Zone = i < ExitZoneIds.Length ? ExitZoneIds[i] : i };
            var random = new System.Random((TrashPandas.Runtime.Loot.LootDirector.SeedOverride ?? System.Environment.TickCount) + 7);
            byte mask = 0;
            foreach (int i in TrashPandas.Core.Round.RoundSetup.PickExits(candidates, GardenCenter, OpenExitCount, MinExitDistance, random)) mask |= (byte)(1 << i);
            _offlineOpenExits = mask;
            if (IsSpawned && IsServer) _openExits.Value = mask;
        }

        /// <summary>The player at this keyboard, or null if unknown.</summary>
        public int? LocalPlayer
        {
            get
            {
                if (!SimulationAuthority.IsOnline)
                    return Squad.SquadController.Instance ? Squad.SquadController.Instance.ActivePlayerId
                         : TrenchcoatController.Instance ? TrenchcoatController.Instance.LocalPlayerId : (int?)null;
                return Snapshot.PlayerOfClient(NetworkManager.Singleton.LocalClientId);
            }
        }

        void Awake() => Instance = this;

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        void SetPhase(RoundPhase p)
        {
            _offlinePhase = p;
            if (IsSpawned && IsServer) _phase.Value = (byte)p;
        }

        void Update()
        {
            bool panicking = Phase == RoundPhase.Panic || (Phase == RoundPhase.Results && !Snapshot.CleanExit);
            for (int i = 0; i < ExitMarkers.Length; i++)
            {
                var m = ExitMarkers[i];
                bool show = panicking && ExitOpen(i);
                if (m && m.activeSelf != show) m.SetActive(show);
            }
            if (!SimulationAuthority.IsSimulating) return;
            if (SimulationAuthority.IsOnline && !IsSpawned) return;
            if (!_exitsPicked) PickExits();
            var suspicion = SuspicionDirector.Instance;
            if (Phase == RoundPhase.Infiltration && suspicion && suspicion.Caught) BeginPanic(suspicion);
            else if (Phase == RoundPhase.Infiltration && suspicion && ArchRadius > 0f && suspicion.Coat && !Squad.GameMode.Raccoons)
            {
                Vector3 d = suspicion.Coat.transform.position - ArchCenter;
                d.y = 0f;
                var pocket = TrashPandas.Runtime.Loot.LootDirector.Instance;
                if (CleanExit.Qualifies(true, d.magnitude < ArchRadius, (int)suspicion.LastFrame.MissingParts, pocket ? pocket.Pocket.Total : 0)) EndWithCleanExit(suspicion);
            }
            if (Phase == RoundPhase.Panic) TickPanic();
        }

        void BeginPanic(SuspicionDirector suspicion)
        {
            if (Debug.isDebugBuild && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-slowchasers") >= 0)
            { ChaserSpeed = 1f; CatSpeed = 1f; } // dev automation: let an escape happen deterministically
            SetPhase(RoundPhase.Panic);
            suspicion.Suspended = true;
            GatherPlayers(burst: true);

            _hits.Reset();
            _grace.Begin(Time.time);
            _missingSince.Clear();
            _outcome.Begin(_players, Time.time, TimeLimit);
            // The coat's pocket is shared out now; each raccoon only keeps its share if it escapes.
            var loot = TrashPandas.Runtime.Loot.LootDirector.Instance;
            if (loot && Squad.GameMode.Raccoons)
            {
                // v2: what reached the den is yours whatever happens now.
                Payout.SharesAreSafe = true;
                var delivered = new Dictionary<int, int>();
                foreach (int p in _players) delivered[p] = loot.Ledger.Of(p);
                Payout.SetShares(delivered);
            }
            else if (loot) Payout.SetShares(loot.Pocket.Split(_players));

            _chasers.Clear();
            
            foreach (var brain in suspicion.Brains)
            {
                if (!brain.Pawn) continue;
                brain.Pawn.Panic(brain.Pawn.Kind == NpcKind.Cat ? CatSpeed : ChaserSpeed);
                brain.Pawn.SetMood((byte)Core.Npc.GuestState.Alarmed);
                _chasers.Add(new Chaser { Brain = brain });
            }

            // Everyone but the cat runs for the nearest free weapon.
            var weapons = new List<PanicWeapon>(FindObjectsByType<PanicWeapon>(FindObjectsSortMode.InstanceID));
            var humans = _chasers.FindAll(c => !c.IsCat);
            var assignment = WeaponAssigner.Assign(humans.ConvertAll(c => c.Brain.Pawn.transform.position), weapons.ConvertAll(w => w.transform.position));
            for (int i = 0; i < humans.Count; i++) if (assignment[i] >= 0) humans[i].Weapon = weapons[assignment[i]];
            Publish();
        }

        void GatherPlayers(bool burst)
        {
            _players.Clear();
            if (SimulationAuthority.IsOnline)
            {
                var roster = SessionHost.Instance ? SessionHost.Instance.Roster : null;
                if (roster != null) for (int p = 0; p < roster.Clients.Count + 5; p++) if (roster.ClientOf(p).HasValue) _players.Add(p);
                if (burst && !Squad.GameMode.Raccoons && NetworkedTrenchcoat.Instance) NetworkedTrenchcoat.Instance.BurstAll();
            }
            else if (Squad.SquadController.Instance)
            {
                // v2 debug: the raccoon you're driving decides the round (the others are parked props).
                _players.Add(Squad.SquadController.Instance.ActivePlayerId);
            }
            else if (TrenchcoatController.Instance)
            {
                // Debug mode: the other seats are virtual players with no one at the keys — their raccoons
                // pop out for the chaos, but only yours decides when the round ends.
                _players.Add(TrenchcoatController.Instance.LocalPlayerId);
                if (burst) TrenchcoatController.Instance.BurstAll();
            }
        }

        /// <summary>The coat strolled out through the arch with everyone inside: all escape, pocket ×1.5.</summary>
        void EndWithCleanExit(SuspicionDirector suspicion)
        {
            suspicion.Suspended = true;
            GatherPlayers(burst: false);
            _outcome.Begin(_players, Time.time, TimeLimit);
            var loot = TrashPandas.Runtime.Loot.LootDirector.Instance;
            if (loot) Payout.SetShares(loot.Pocket.CleanExitPayout(_players));
            foreach (int p in _players) { _outcome.MarkEscaped(p); Payout.Escaped(p); }
            _cleanExit = true;
            SetPhase(RoundPhase.Results);
            Publish();
        }

        void TickPanic()
        {
            float now = Time.time, dt = Time.deltaTime;

            _raccoonOf.Clear();
            foreach (var r in FindObjectsByType<RaccoonController>(FindObjectsSortMode.None))
                if (r && r.PlayerId >= 0) _raccoonOf[r.PlayerId] = r;

            _targets.Clear();
            foreach (int p in _players)
            {
                if (_outcome.StatusOf(p) != PlayerOutcome.Running) continue;
                if (!_raccoonOf.TryGetValue(p, out var r))
                {
                    // Disconnected (or otherwise gone) for a moment: counts as caught so the round can end.
                    if (!_missingSince.ContainsKey(p)) _missingSince[p] = now;
                    else if (now - _missingSince[p] > 1f) _outcome.MarkCaught(p);
                    continue;
                }
                _missingSince.Remove(p);
                if (TowerRules.CountsForExit(riding: r.Mount) && ExitZones.Contains(OpenExitPositions, r.transform.position, ExitRadius) >= 0)
                {
                    r.CollapseTower(); // riders fall off here and keep playing
                    _outcome.MarkEscaped(p);
                    TrashPandas.Runtime.Loot.LootDirector.Instance?.OnEscaped(p, Payout);
                    Payout.Escaped(p);
                    Remove(r);
                    continue;
                }
                _targets.Add(new ChaseTarget { Id = p, Position = r.transform.position });
            }

            bool shocked = !_grace.ChasersMayMove(now);
            foreach (var c in _chasers)
            {
                var pawn = c.Brain.Pawn;
                if (!pawn) continue;
                if (shocked)
                {
                    // "¡¡RUUUN!!" — a beat of pure shock: they freeze and stare while the raccoons scatter.
                    pawn.Stop();
                    if (_targets.Count > 0) pawn.LookAt(_targets[0].Position);
                    continue;
                }
                bool hasWeapon = c.Weapon && c.Weapon.Holder == pawn;
                if (c.Weapon && c.Weapon.Holder && c.Weapon.Holder != pawn) c.Weapon = null; // someone else got it
                Vector3? weaponPos = !c.IsCat && c.Weapon && !hasWeapon ? c.Weapon.transform.position : (Vector3?)null;

                var o = c.Mind.Update(dt, pawn.transform.position, hasWeapon, weaponPos, _targets);
                if (o.State == ChaserState.FetchWeapon && Vector3.Distance(pawn.transform.position, o.Destination) < 1.1f) c.Weapon.PickUp(pawn);
                pawn.GoTo(o.Destination);
                pawn.LookAt(o.State == ChaserState.Chase ? o.Destination : (Vector3?)null);

                if (!o.Swing || !_grace.MayHit(now) || !_raccoonOf.TryGetValue(o.TargetId, out var target)) continue;
                Vector3 to = target.transform.position - pawn.transform.position;
                to.y = 0f;
                if (to.magnitude > c.Mind.SwingRange + 0.3f) continue;
                Vector3 dir = to.sqrMagnitude > 1e-4f ? to.normalized : pawn.transform.forward;

                if (c.IsCat) { Push(target, dir * CatPushImpulse, 0.4f); continue; }
                if (c.Weapon) c.Weapon.PlaySwing();
                var result = _hits.TryHit(o.TargetId, now);
                if (result == HitResult.Ignored) continue;
                Push(target, dir * HitImpulse, _hits.StunSeconds);
                if (result == HitResult.Caught && _outcome.MarkCaught(o.TargetId))
                {
                    Freeze(target);
                    Payout.Caught(o.TargetId);
                    TrashPandas.Runtime.Loot.LootDirector.Instance?.DropMouth(o.TargetId);
                }
            }

            _outcome.Tick(now);
            if (_outcome.IsOver)
            {
                foreach (int p in _players)
                    if (_outcome.StatusOf(p) == PlayerOutcome.Caught) { Payout.Caught(p); TrashPandas.Runtime.Loot.LootDirector.Instance?.DropMouth(p); }
                SetPhase(RoundPhase.Results);
            }
            Publish();
        }

        void Publish()
        {
            var snap = new PanicSnapshot { SecondsLeft = _outcome.SecondsLeft(Time.time), CleanExit = _cleanExit };
            var roster = SimulationAuthority.IsOnline && SessionHost.Instance ? SessionHost.Instance.Roster : null;
            foreach (int p in _players) { snap.Set(p, roster?.ClientOf(p), _outcome.StatusOf(p), _hits.Hits(p)); snap.SetLoot(p, Payout.Of(p)); }
            _offlineSnapshot = snap;
            if (IsSpawned && IsServer && !snap.Equals(_snapshot.Value)) _snapshot.Value = snap;
        }

        static void Push(RaccoonController r, Vector3 impulse, float stun)
        {
            var net = r.GetComponent<NetworkedRaccoon>();
            if (SimulationAuthority.IsOnline && net && net.IsSpawned) net.HitRpc(impulse, stun);
            else r.ApplyHit(impulse, stun);
        }

        static void Freeze(RaccoonController r)
        {
            var net = r.GetComponent<NetworkedRaccoon>();
            r.Frozen = true; // on the host too, so host-side checks see it
            if (SimulationAuthority.IsOnline && net && net.IsSpawned) net.FreezeRpc();
        }

        void Remove(RaccoonController r)
        {
            var net = r.GetComponent<NetworkObject>();
            if (SimulationAuthority.IsOnline && net && net.IsSpawned) { net.Despawn(destroy: true); return; }
            if (TrenchcoatController.Instance && r.PlayerId == TrenchcoatController.Instance.LocalPlayerId) TrenchcoatController.Instance.ShowOverview(Overview);
            if (Squad.SquadController.Instance && r.PlayerId == Squad.SquadController.Instance.ActivePlayerId && Overview)
                Squad.SquadController.Instance.CameraRig.SetTarget(Overview, 16f, 0f);
            Destroy(r.gameObject);
        }

        /// <summary>Results screen button: host (online) or the single player (offline) starts a fresh round.</summary>
        public void PlayAgain()
        {
            if (SimulationAuthority.IsOnline)
            {
                if (SessionHost.Instance && SessionHost.Instance.IsHost) SessionHost.Instance.RestartRound();
                return;
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
