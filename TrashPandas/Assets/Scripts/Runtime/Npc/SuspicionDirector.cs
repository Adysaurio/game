using System.Collections.Generic;
using TrashPandas.Core.Npc;
using TrashPandas.Core.Perception;
using TrashPandas.Core.Suspicion;
using TrashPandas.Core.Trenchcoat;
using TrashPandas.Runtime.Net;
using TrashPandas.Runtime.Raccoon;
using TrashPandas.Runtime.Trenchcoat;
using Unity.AI.Navigation;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.Runtime.Npc
{
    /// <summary>
    /// Runs the humans and the suspicion meter where the simulation runs (offline or host): works out what
    /// each NPC sees, updates their minds, moves them, and replicates suspicion to clients.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class SuspicionDirector : NetworkBehaviour
    {
        public TrenchcoatBody Coat;
        public NavMeshSurface Navigation;
        public float VisionRange = 9f;
        public float VisionFov = 120f;
        [Tooltip("How far a seated/standing guest strolls from where they started.")]
        public float WanderRadius = 2.5f;

        public static SuspicionDirector Instance { get; private set; }
        /// <summary>Set by the panic director: humans are no longer guests, they're chasers.</summary>
        public bool Suspended { get; set; }

        readonly NetworkVariable<float> _suspicion = new NetworkVariable<float>();
        readonly NetworkVariable<bool> _caught = new NetworkVariable<bool>();
        readonly SuspicionSettings _settings = new SuspicionSettings();
        readonly List<NpcBrain> _brains = new List<NpcBrain>();
        readonly List<RaccoonController> _raccoons = new List<RaccoonController>();
        readonly RaycastHit[] _hits = new RaycastHit[12];
        SuspicionModel _model;
        float _nextRaccoonScan;

        public float Suspicion => SimulationAuthority.IsOnline ? _suspicion.Value : (_model?.Value ?? 0f);
        public bool Caught => SimulationAuthority.IsOnline ? _caught.Value : (_model?.Caught ?? false);
        public IReadOnlyList<NpcBrain> Brains => _brains;
        /// <summary>Last perception frame fed to the meter (diagnostics).</summary>
        public SuspicionFrame LastFrame { get; private set; }

        void Awake()
        {
            Instance = this;
            _model = new SuspicionModel(_settings);
            if (Navigation) Navigation.BuildNavMesh(); // small scene: bake at load, everywhere
        }

        void Start()
        {
            if (Debug.isDebugBuild && Coat && UnityEngine.AI.NavMesh.SamplePosition(Coat.transform.position, out var hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
                Debug.Log($"[NAV] coat start is {Vector3.Distance(hit.position, Coat.transform.position):F2} m from the NavMesh");
            _brains.Clear();
            foreach (var pawn in FindObjectsByType<NpcPawn>(FindObjectsSortMode.InstanceID))
            {
                _brains.Add(new NpcBrain(pawn, _brains.Count, WanderRadius));
                pawn.EnableNavigation();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) foreach (var b in _brains) b.Pawn.EnableNavigation();
        }

        /// <summary>One-off change from a social event.</summary>
        public void AdjustSuspicion(float delta)
        {
            _model.Adjust(delta);
            Publish();
        }

        public void ResetSuspicion()
        {
            _model.Reset();
            Publish();
        }

        void Update()
        {
            bool coatInPlay = Coat && !Squad.GameMode.Raccoons;
            if (!SimulationAuthority.IsSimulating || Suspended || Squad.RoundIntro.Playing || (!coatInPlay && !Squad.GameMode.Raccoons)) return;
            float dt = Time.deltaTime, now = Time.time;

            if (now >= _nextRaccoonScan)
            {
                _nextRaccoonScan = now + 0.25f;
                _raccoons.Clear();
                _raccoons.AddRange(FindObjectsByType<RaccoonController>(FindObjectsSortMode.None));
            }

            var intent = coatInPlay ? Coat.CurrentIntent : default;
            float weirdness = coatInPlay ? Weirdness.Of(intent) : 0f;
            Vector3 coatTarget = coatInPlay ? Coat.ChestWorld : Vector3.down * 100f;
            var frame = new SuspicionFrame { MissingParts = coatInPlay ? MissingParts(intent) : BodyPart.None };
            // v2: the cat sniffs out the nearest raccoon instead of the coat.
            Vector3 catQuarry = coatInPlay ? Coat.transform.position : Vector3.one * 1e4f;

            foreach (var brain in _brains)
            {
                var pawn = brain.Pawn;
                if (brain.Cat != null)
                {
                    Vector3 quarry = catQuarry;
                    if (!coatInPlay)
                        foreach (var r in _raccoons)
                            if (r && Vector3.Distance(r.transform.position, pawn.transform.position) < Vector3.Distance(quarry, pawn.transform.position)) quarry = r.transform.position;
                    brain.Cat.Update(dt, pawn.transform.position, quarry);
                    pawn.GoTo(brain.Cat.Destination);
                    pawn.SetMood((byte)brain.Cat.State);
                    pawn.LookAt(brain.Cat.State == CatState.Patrol ? (Vector3?)null : quarry);
                    frame.CatHissing |= brain.Cat.IsHissing;
                    continue;
                }

                bool seesCoat = coatInPlay && Sees(pawn, coatTarget, Coat.transform);
                RaccoonController seenRaccoon = null;
                foreach (var r in _raccoons)
                    if (r && Sees(pawn, r.transform.position + Vector3.up * 0.3f, r.transform)) { seenRaccoon = r; break; }

                float seen = seesCoat ? weirdness : 0f;
                // A noise nearby: they turn, get curious ("?") and, if standing, go and have a look.
                bool hearing = brain.HeardNoise.HasValue && now < brain.HeardUntil;
                if (hearing) seen = Mathf.Max(seen, 0.35f);
                var state = brain.Guest.Update(dt, seen, seenRaccoon);
                // Only once the guest has actually registered the raccoon (not on a split-second glimpse).
                if (seenRaccoon && state == GuestState.Alarmed) _model.ReportRaccoonSighting(brain.Id, now);
                if (seenRaccoon) brain.LastSeenRaccoon = seenRaccoon.transform.position;
                if (seesCoat) { frame.CoatWitnessed = true; frame.SeenWeirdness = Mathf.Max(frame.SeenWeirdness, weirdness); }

                pawn.SetMood((byte)state);
                pawn.LookAt(seenRaccoon ? seenRaccoon.transform.position : hearing ? brain.HeardNoise.Value : state != GuestState.Calm || seesCoat ? coatTarget : (Vector3?)null);
                if (hearing && state != GuestState.Alarmed && !pawn.Seated && !brain.Busy) pawn.GoTo(brain.HeardNoise.Value);
                else brain.Stroll(dt, state);
            }

            LastFrame = frame;
            _model.Tick(dt, frame);
            Publish();
        }

        void OnEnable() => Squad.NoiseBus.Heard += OnNoise;
        void OnDisable() => Squad.NoiseBus.Heard -= OnNoise;

        /// <summary>Host/offline: who hears it (walls halve the distance) turns toward it for a few seconds.</summary>
        void OnNoise(Core.Raccoons.NoiseKind kind, Vector3 at)
        {
            if (!SimulationAuthority.IsSimulating || Suspended) return;
            float radius = Core.Raccoons.NoiseModel.Radius(kind);
            foreach (var brain in _brains)
            {
                if (brain.Guest == null || !brain.Pawn) continue;
                Vector3 ear = brain.Pawn.Eye;
                bool occluded = Physics.Linecast(ear, at + Vector3.up * 0.3f, out var hit, ~0, QueryTriggerInteraction.Ignore)
                                && !hit.collider.transform.IsChildOf(brain.Pawn.transform) && !hit.collider.GetComponentInParent<RaccoonController>();
                if (!Core.Raccoons.NoiseModel.Hears(ear, at, radius, occluded)) continue;
                brain.HeardNoise = at;
                Ui.DebugChecklist.Mark("heard");
                brain.HeardUntil = Time.time + 4f;
            }
        }

        void Publish()
        {
            if (!IsSpawned || !IsServer) return;
            if (!Mathf.Approximately(_suspicion.Value, _model.Value)) _suspicion.Value = _model.Value;
            if (_caught.Value != _model.Caught) _caught.Value = _model.Caught;
        }

        /// <summary>Field of view plus a line of sight that ignores the looker and the target themselves.</summary>
        bool Sees(NpcPawn pawn, Vector3 target, Transform targetRoot) => CanSee(pawn, target, targetRoot, VisionRange, VisionFov);

        /// <summary>Field of view + line of sight (tables, hedges and people block it).</summary>
        public bool CanSee(NpcPawn pawn, Vector3 target, Transform targetRoot, float range, float fov)
        {
            Vector3 eye = pawn.Eye;
            if (!VisionCone.CanSee(eye, pawn.transform.forward, target, range, fov, false)) return false;
            Vector3 to = target - eye;
            float dist = to.magnitude;
            int n = Physics.RaycastNonAlloc(eye, to / dist, _hits, dist, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var t = _hits[i].collider.transform;
                if (t.IsChildOf(pawn.transform) || t.IsChildOf(targetRoot)) continue;
                return false; // something in between (table, hedge, another person)
            }
            return true;
        }

        static BodyPart MissingParts(in BodyIntent i)
        {
            var m = BodyPart.None;
            if (i.LeftLegLimp) m |= BodyPart.LegLeft;
            if (i.RightLegLimp) m |= BodyPart.LegRight;
            if (i.LeftArmLimp) m |= BodyPart.ArmLeft;
            if (i.RightArmLimp) m |= BodyPart.ArmRight;
            if (i.HeadSlumped) m |= BodyPart.Head;
            return m;
        }
    }

    /// <summary>Simulation-side state for one NPC.</summary>
    public sealed class NpcBrain
    {
        public readonly NpcPawn Pawn;
        public readonly int Id;
        public readonly GuestMind Guest;
        public readonly CatMind Cat;
        /// <summary>Where this guest last saw a loose raccoon (alarmed guests go and look).</summary>
        public Vector3? LastSeenRaccoon;
        /// <summary>Where they last heard a raccoon noise, and until when they care.</summary>
        public Vector3? HeardNoise;
        public float HeardUntil;
        readonly Vector3 _home;
        readonly float _wander;
        readonly Vector3[] _route;
        int _routeIndex;
        float _nextStroll;

        public NpcBrain(NpcPawn pawn, int id, float wander)
        {
            Pawn = pawn;
            Id = id;
            _home = pawn.transform.position;
            _wander = wander;
            var route = pawn.GetComponent<NpcRoute>();
            _route = route ? route.Points : null;
            if (pawn.Kind == NpcKind.Cat) Cat = new CatMind(_route ?? new[] { _home });
            else Guest = new GuestMind();
        }

        /// <summary>Waiters follow their route; standing guests mill around; seated guests stay put.</summary>
        /// <summary>Taken over by a social event (walking up to the coat): don't stroll.</summary>
        public bool Busy;

        public void Stroll(float dt, GuestState state)
        {
            if (Busy) return;
            if (Pawn.Seated || Time.time < _nextStroll) return;
            if (state == GuestState.Alarmed)
            {
                // Before RUN nobody attacks, but alarmed guests go and see what that was.
                if (LastSeenRaccoon.HasValue) Pawn.GoTo(LastSeenRaccoon.Value);
                _nextStroll = Time.time + 0.5f;
                return;
            }
            if (state == GuestState.Calm) LastSeenRaccoon = null;
            if (_route != null && _route.Length > 0)
            {
                if ((new Vector2(Pawn.transform.position.x - _route[_routeIndex].x, Pawn.transform.position.z - _route[_routeIndex].z)).magnitude < 0.8f)
                    _routeIndex = (_routeIndex + 1) % _route.Length;
                Pawn.GoTo(_route[_routeIndex]);
                _nextStroll = Time.time + 0.5f;
                return;
            }
            var offset = Random.insideUnitCircle * _wander;
            Pawn.GoTo(_home + new Vector3(offset.x, 0f, offset.y));
            _nextStroll = Time.time + Random.Range(4f, 9f);
        }
    }
}
