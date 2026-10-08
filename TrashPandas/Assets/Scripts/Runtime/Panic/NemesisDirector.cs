using System.Collections;
using TrashPandas.Core.Panic;
using TrashPandas.Runtime.Net;
using TrashPandas.Runtime.Npc;
using TrashPandas.Runtime.Raccoon;
using TrashPandas.Runtime.Squad;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.Runtime.Panic
{
    /// <summary>
    /// Tonight's antagonist (one per round, picked at random): the only one who hunts. Before the RUN they
    /// patrol, go and check what witnesses report and what they hear, learn your tricks, and if they see a
    /// raccoon it's RUN. During the RUN the panic director chases with them (and their helper).
    /// </summary>
    public sealed class NemesisDirector : NetworkBehaviour
    {
        public NpcPawn Pawn;
        /// <summary>One costume per <see cref="NemesisKind"/> (only the picked one is shown).</summary>
        public GameObject[] Costumes = new GameObject[0];
        public Vector3[] Patrol = new Vector3[0];
        public float BaseSpeed = 1.5f;
        public float NoticeSeconds = 0.5f;

        public static NemesisDirector Instance { get; private set; }
        public static NemesisKind? LastRound;

        readonly NetworkVariable<byte> _kind = new NetworkVariable<byte>();
        NemesisKind _offlineKind;
        bool _picked;
        public string DebugNet => $"{_kind.Value}/{_offlineKind}/{NetworkObjectId}/online={SimulationAuthority.IsOnline}/gadgets={(GadgetDirector.Instance && GadgetDirector.Instance.IsSpawned)}/susp={(SuspicionDirector.Instance && SuspicionDirector.Instance.IsSpawned)}";
        public NemesisKind Kind => SimulationAuthority.IsOnline ? (NemesisKind)_kind.Value : _offlineKind;
        public NemesisProfile Profile => NemesisProfile.Of(Kind);
        public readonly NemesisMemory Memory = new NemesisMemory();

        /// <summary>What they're up to (for the HUD bubble): 0 patrol, 1 checking something out, 2 spotted.</summary>
        public byte Activity => Pawn ? Pawn.Mood : (byte)0;
        public const byte Patrolling = 0, Investigating = 1, Spotting = 2;

        Vector3? _investigate;
        float _stuckFor;
        float _investigateUntil, _spotFor, _nextStep, _dwellUntil;
        int _patrolIndex;
        RaccoonController _ambushTarget;

        void Awake() => Instance = this;
        public override void OnDestroy() { if (Instance == this) Instance = null; base.OnDestroy(); }

        void OnEnable()
        {
            NoiseBus.Heard += OnNoise;
            SuspicionDirector.Witnessed += OnWitness;
        }
        void OnDisable()
        {
            NoiseBus.Heard -= OnNoise;
            SuspicionDirector.Witnessed -= OnWitness;
        }

        void Pick()
        {
            _picked = true;
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-nemesis");
            NemesisKind kind;
            if (i >= 0 && i + 1 < args.Length && System.Enum.TryParse(args[i + 1], true, out NemesisKind forced)) kind = forced;
            else kind = NemesisPick.Roll(System.Environment.TickCount, LastRound);
            LastRound = kind;
            _offlineKind = kind;
            if (IsSpawned && IsServer) _kind.Value = (byte)kind;
            if (!IsSpawned || IsServer) Debug.Log($"[Nemesis] tonight: {kind}");
            if (Pawn) Pawn.SetSpeed(BaseSpeed * Profile.Speed);
            if (Debug.isDebugBuild && System.Array.IndexOf(args, "-nemesisnear") >= 0) StartCoroutine(WarpNearPlayer());
        }

        void ShowCostume()
        {
            for (int i = 0; i < Costumes.Length; i++)
                if (Costumes[i] && Costumes[i].activeSelf != (i == (int)Kind)) Costumes[i].SetActive(i == (int)Kind);
        }

        void Update()
        {
            if (SimulationAuthority.IsSimulating && (!SimulationAuthority.IsOnline || IsSpawned) && !_picked) Pick();
            // The host may have picked before the session went online: make sure clients get the pick.
            if (IsSpawned && IsServer && _picked && _kind.Value != (byte)_offlineKind) _kind.Value = (byte)_offlineKind;
            ShowCostume();
            Footsteps();
            if (!SimulationAuthority.IsSimulating || (SimulationAuthority.IsOnline && !IsSpawned) || !Pawn) return;
            var pd = PanicDirector.Instance;
            if (pd && pd.Phase != RoundPhase.Infiltration) return; // the RUN: the panic director drives them
            if (RoundIntro.Playing) return;
            var sd = SuspicionDirector.Instance;
            if (!sd || sd.Suspended) return;
            if (!_portrait) TickInfiltration(sd);
        }
        bool _portrait;

        /// <summary>You hear them before you see them (Mr. X): heels / boots / a cane, louder as they get close.</summary>
        void Footsteps()
        {
            if (!Pawn || Time.time < _nextStep) return;
            var cam = Camera.main;
            if (!cam) return;
            float d = Vector3.Distance(cam.transform.position, Pawn.transform.position);
            bool moving = Pawn.Velocity.sqrMagnitude > 0.2f;
            _nextStep = Time.time + (Kind == NemesisKind.Granny ? 0.7f : Kind == NemesisKind.PestControl ? 0.55f : 0.32f);
            if (!moving || d > 22f) return;
            Ui.Sfx.Play(Ui.Sound.Land, Pawn.transform.position, Mathf.Lerp(0.5f, 0.05f, d / 22f));
        }

        void TickInfiltration(SuspicionDirector sd)
        {
            var p = Profile;
            // Do they see a raccoon? (A short beat to notice, then it's RUN.)
            RaccoonController seen = null;
            foreach (var r in RaccoonController.Registered)
                if (r && r.PlayerId >= 0 && !r.Frozen && Sees(sd, r)) { seen = r; break; }
            if (seen)
            {
                Pawn.Stop();
                Pawn.LookAt(seen.transform.position);
                Pawn.SetMood(Spotting);
                _spotFor += Time.deltaTime;
                if (_spotFor >= NoticeSeconds)
                {
                    if (seen.InCan) KickOut(seen);
                    sd.Alarm();
                    Ui.DebugChecklist.Mark("nemesis");
                }
                return;
            }
            _spotFor = Mathf.Max(0f, _spotFor - Time.deltaTime);

            // The same hideout twice: she remembers. (Suspected and occupied, nearby: go and check it.)
            foreach (var h in Hideout.All)
                if (h && h.Occupant && Memory.Suspects(h.Id) && Flat(h.transform.position, Pawn.transform.position) < 14f)
                    Investigate(h.transform.position, 8f);
            if (Memory.Ambush.HasValue) { Investigate(Memory.Ambush.Value, 7f); Memory.AmbushDone(); }

            if (_investigate.HasValue && Time.time < _investigateUntil)
            {
                Pawn.SetMood(Investigating);
                Pawn.LookAt(_investigate.Value + Vector3.up * 0.5f);
                if (Flat(Pawn.transform.position, _investigate.Value) > 1.2f && Pawn.Velocity.sqrMagnitude > 0.04f || Time.time < _investigateUntil - 4f) Pawn.GoTo(_investigate.Value);
                else
                {
                    Pawn.Stop();
                    // Right there: kick the can / shake the bush someone is hiding in.
                    foreach (var h in Hideout.All)
                        if (h && h.Occupant && Flat(h.transform.position, Pawn.transform.position) < 1.8f) { var victim = h.Occupant; KickOut(victim); sd.Alarm(); return; }
                }
                return;
            }
            _investigate = null;
            Pawn.SetMood(Patrolling);
            Pawn.LookAt(null);
            if (Patrol.Length == 0 || Time.time < _dwellUntil) return;
            // Stuck (can't reach that point): move on to the next one.
            _stuckFor = Pawn.Velocity.sqrMagnitude < 0.04f ? _stuckFor + Time.deltaTime : 0f;
            if (Flat(Pawn.transform.position, Patrol[_patrolIndex]) < 1.5f || _stuckFor > 2f)
            {
                _stuckFor = 0f;
                _patrolIndex = (_patrolIndex + 1) % Patrol.Length;
                _dwellUntil = Time.time + 2.5f; // pause: checks the list, looks around
                return;
            }
            Pawn.GoTo(Patrol[_patrolIndex]);
        }

        /// <summary>Dev: put the nemesis in front of the camera for a portrait.</summary>
        IEnumerator WarpNearPlayer()
        {
            yield return new WaitForSeconds(1f);
            var cam = Camera.main;
            if (!cam || !Pawn) yield break;
            Vector3 f = cam.transform.forward; f.y = 0f; f.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, f);
            Vector3 at = cam.transform.position + f * 7f + right * 2.2f; at.y = 0f;
            _portrait = true;
            var agent = Pawn.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent && agent.enabled) agent.Warp(at); else Pawn.transform.position = at;
            Pawn.transform.rotation = Quaternion.LookRotation(-f);
            Patrol = new[] { at };
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        void Investigate(Vector3 at, float seconds)
        {
            _investigate = at;
            _investigateUntil = Time.time + seconds;
        }

        /// <summary>Line of sight with this nemesis's eyes (the pest control flashlight sees into hideouts up close).</summary>
        public bool Sees(SuspicionDirector sd, RaccoonController r)
        {
            var p = Profile;
            float range = r.IsSneaking ? p.SightRange * 0.5f : p.SightRange;
            float d = Vector3.Distance(Pawn.transform.position, r.transform.position);
            if (HidingSpot.Hides(r))
            {
                if (HidingSpot.DebugParked(r) || GadgetDirector.InSmoke(r.transform.position)) return false;
                if (p.SeesIntoHideoutsWithin <= 0f || d > p.SeesIntoHideoutsWithin) return false;
                Vector3 to = r.transform.position - Pawn.Eye; to.y = 0f;
                return Vector3.Angle(Pawn.transform.forward, to) < p.Fov * 0.5f; // the flashlight beam
            }
            return sd.CanSee(Pawn, r.transform.position + Vector3.up * 0.3f, r.transform, range, p.Fov);
        }

        public static void KickOut(RaccoonController victim)
        {
            if (!victim || !victim.InCan) return;
            var vnet = victim.GetComponent<NetworkedRaccoon>();
            if (SimulationAuthority.IsOnline && vnet && vnet.IsSpawned) vnet.KickOutRpc(); else victim.ExitCan(kicked: true);
        }

        void OnWitness(Vector3 at)
        {
            if (!Pawn || !SimulationAuthority.IsSimulating) return;
            var p = Profile;
            if (p.Radio || Flat(at, Pawn.transform.position) < p.ReportRange) Investigate(at, 9f);
        }

        void OnNoise(Core.Raccoons.NoiseKind kind, Vector3 at)
        {
            if (!Pawn || !SimulationAuthority.IsSimulating) return;
            if (GadgetDirector.EmittingPebble && !Memory.FallsForPebble()) return; // "not falling for that again"
            float radius = Core.Raccoons.NoiseModel.Radius(kind) * Profile.Hearing;
            if (Flat(at, Pawn.transform.position) > radius) return;
            Investigate(at, 6f);
            PanicDirector.Instance?.NemesisHeard(at);
        }

        /// <summary>A raccoon went into a hideout (host/offline): she remembers if it's a habit.</summary>
        public void OnHid(Hideout h) { if (h) Memory.HidIn(h.Id); }

        /// <summary>A raccoon crawled into a pipe; if she saw it, she waits at the other end.</summary>
        public void OnPipe(RaccoonController r, Vector3 otherEnd)
        {
            var sd = SuspicionDirector.Instance;
            if (Pawn && sd && sd.CanSee(Pawn, r.transform.position + Vector3.up * 0.3f, r.transform, Profile.SightRange, Profile.Fov))
            {
                Memory.SawEnterPipe(otherEnd);
                PanicDirector.Instance?.NemesisHeard(otherEnd);
            }
        }

        // --- Granny's slipper ------------------------------------------------------------------------------
        public void ThrowSlipper(Vector3 from, Vector3 to)
        {
            if (SimulationAuthority.IsOnline && IsSpawned) SlipperRpc(from, to); else StartCoroutine(Slipper(from, to));
        }

        [Rpc(SendTo.Everyone)]
        void SlipperRpc(Vector3 from, Vector3 to) => StartCoroutine(Slipper(from, to));

        static Material s_slipper;
        IEnumerator Slipper(Vector3 from, Vector3 to)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(0.12f, 0.05f, 0.28f);
            s_slipper ??= new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = new Color(0.9f, 0.3f, 0.5f) };
            go.GetComponent<Renderer>().sharedMaterial = s_slipper;
            Ui.Sfx.Play(Ui.Sound.Throw, from, 0.9f);
            const float T = 0.35f;
            for (float t = 0f; t < T; t += Time.deltaTime)
            {
                float k = t / T;
                go.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.8f;
                go.transform.rotation = Quaternion.Euler(0f, 0f, k * 900f);
                yield return null;
            }
            Destroy(go, 1.5f);
        }
    }
}
