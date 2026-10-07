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
            if (!SimulationAuthority.IsSimulating || !Coat || Suspended) return;
            float dt = Time.deltaTime, now = Time.time;

            if (now >= _nextRaccoonScan)
            {
                _nextRaccoonScan = now + 0.25f;
                _raccoons.Clear();
                _raccoons.AddRange(FindObjectsByType<RaccoonController>(FindObjectsSortMode.None));
            }

            var intent = Coat.CurrentIntent;
            float weirdness = Weirdness.Of(intent);
            Vector3 coatTarget = Coat.ChestWorld;
            var frame = new SuspicionFrame { MissingParts = MissingParts(intent) };

            foreach (var brain in _brains)
            {
                var pawn = brain.Pawn;
                if (brain.Cat != null)
                {
                    brain.Cat.Update(dt, pawn.transform.position, Coat.transform.position);
                    pawn.GoTo(brain.Cat.Destination);
                    pawn.SetMood((byte)brain.Cat.State);
                    pawn.LookAt(brain.Cat.State == CatState.Patrol ? (Vector3?)null : coatTarget);
                    frame.CatHissing |= brain.Cat.IsHissing;
                    continue;
                }

                bool seesCoat = Sees(pawn, coatTarget, Coat.transform);
                RaccoonController seenRaccoon = null;
                foreach (var r in _raccoons)
                    if (r && Sees(pawn, r.transform.position + Vector3.up * 0.3f, r.transform)) { seenRaccoon = r; break; }

                float seen = seesCoat ? weirdness : 0f;
                var state = brain.Guest.Update(dt, seen, seenRaccoon);
                // Only once the guest has actually registered the raccoon (not on a split-second glimpse).
                if (seenRaccoon && state == GuestState.Alarmed) _model.ReportRaccoonSighting(brain.Id, now);
                if (seenRaccoon) brain.LastSeenRaccoon = seenRaccoon.transform.position;
                if (seesCoat) { frame.CoatWitnessed = true; frame.SeenWeirdness = Mathf.Max(frame.SeenWeirdness, weirdness); }

                pawn.SetMood((byte)state);
                pawn.LookAt(seenRaccoon ? seenRaccoon.transform.position : state != GuestState.Calm || seesCoat ? coatTarget : (Vector3?)null);
                brain.Stroll(dt, state);
            }

            LastFrame = frame;
            _model.Tick(dt, frame);
            Publish();
        }

        void Publish()
        {
            if (!IsSpawned || !IsServer) return;
            if (!Mathf.Approximately(_suspicion.Value, _model.Value)) _suspicion.Value = _model.Value;
            if (_caught.Value != _model.Caught) _caught.Value = _model.Caught;
        }

        /// <summary>Field of view plus a line of sight that ignores the looker and the target themselves.</summary>
        bool Sees(NpcPawn pawn, Vector3 target, Transform targetRoot)
        {
            Vector3 eye = pawn.Eye;
            if (!VisionCone.CanSee(eye, pawn.transform.forward, target, VisionRange, VisionFov, false)) return false;
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
