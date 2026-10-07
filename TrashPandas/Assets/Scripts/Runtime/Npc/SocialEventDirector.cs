using System.Collections.Generic;
using TrashPandas.Core.Events;
using TrashPandas.Core.Trenchcoat;
using TrashPandas.Runtime.Net;
using TrashPandas.Runtime.Raccoon;
using TrashPandas.Runtime.Trenchcoat;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.Runtime.Npc
{
    /// <summary>
    /// Social events (spec §6b): every so often a human walks up to the coat; everyone must be back inside;
    /// then each role does its part within a few seconds. Simulated on the host (or offline).
    /// </summary>
    [DefaultExecutionOrder(-50)] // before PanicDirector, so cancelling on RUN never undoes its panic speed
    public sealed class SocialEventDirector : NetworkBehaviour
    {
        public float ResponseWindow = 8f;
        public float ResultDisplay = 3f;
        [Tooltip("Dev/testing: start the first event this many seconds into the round instead of 35-50 s (0 = normal).")]
        public float FirstEventOverride;

        public static SocialEventDirector Instance { get; private set; }

        readonly NetworkVariable<EventSnapshot> _net = new NetworkVariable<EventSnapshot>();
        EventSnapshot _offline;
        EventScheduler _scheduler;
        readonly Dictionary<int, EventResponse> _responses = new Dictionary<int, EventResponse>();
        float _phaseEndsAt;
        BodyPart _presentAtStart;
        NpcPawn _speaker;
        NpcBrain _speakerBrain;
        byte _serial;
        float _speakerBaseSpeed;
        readonly System.Random _random = new System.Random(System.Environment.TickCount);

        public EventSnapshot Snapshot => SimulationAuthority.IsOnline ? _net.Value : _offline;
        public EventPhase Phase => (EventPhase)Snapshot.Phase;
        /// <summary>The current conversation with this occurrence's rolled tasks and answer order.</summary>
        public SocialEvent Current
        {
            get
            {
                var s = Snapshot;
                if (_cached == null || _cachedKey != (s.Serial, s.EventIndex, s.Arms, s.Legs, s.Order))
                {
                    var template = SocialEventCatalog.All[Mathf.Clamp(s.EventIndex, 0, SocialEventCatalog.All.Count - 1)];
                    _cached = EventRoll.Apply(template, new RolledTasks { Arms = (ArmsTask)s.Arms, Legs = (LegsTask)s.Legs, Order = s.Order });
                    _cachedKey = (s.Serial, s.EventIndex, s.Arms, s.Legs, s.Order);
                }
                return _cached;
            }
        }
        SocialEvent _cached;
        (byte, byte, byte, byte, byte) _cachedKey;
        /// <summary>Where the speaker is (for the HUD arrow), if visible on this machine.</summary>
        public Vector3? SpeakerPosition
        {
            get
            {
                if (Phase == EventPhase.Idle) return null;
                foreach (var p in FindObjectsByType<NpcPawn>(FindObjectsSortMode.None))
                    if (p.SpeakerId == Current.Speaker) return p.transform.position;
                return null;
            }
        }

        /// <summary>The speaker's transform on this machine (for the conversation camera), if any.</summary>
        public Transform SpeakerTransform
        {
            get
            {
                if (Phase == EventPhase.Idle) return null;
                foreach (var p in FindObjectsByType<NpcPawn>(FindObjectsSortMode.None))
                    if (p.SpeakerId == Current.Speaker) return p.transform;
                return null;
            }
        }

        void Awake()
        {
            Instance = this;
            _scheduler = new EventScheduler(System.Environment.TickCount);
            if (Debug.isDebugBuild)
            {
                var args = System.Environment.GetCommandLineArgs();
                int i = System.Array.IndexOf(args, "-eventat");
                if (i >= 0 && i + 1 < args.Length) float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out FirstEventOverride);
            }
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            base.OnDestroy();
        }

        /// <summary>Client → host: what this player did during the response window.</summary>
        [Rpc(SendTo.Server)]
        public void SubmitResponseRpc(NetEventResponse r, RpcParams rpcParams = default)
        {
            var roster = SessionHost.Instance ? SessionHost.Instance.Roster : null;
            int? player = roster?.PlayerIdOf(rpcParams.Receive.SenderClientId);
            if (player.HasValue) SubmitResponse(player.Value, r.EventSerial, r.Response);
        }

        /// <summary>Record a player's latest response for the current event (stale serials are ignored).</summary>
        public void SubmitResponse(int player, byte serial, EventResponse response)
        {
            if (serial != _serial || (EventPhase)_offline.Phase != EventPhase.Engaged) return;
            _responses[player] = response;
        }

        void Update()
        {
            if (!SimulationAuthority.IsSimulating) return;
            var suspicion = SuspicionDirector.Instance;
            var coat = suspicion ? suspicion.Coat : null;
            if (!coat) return;
            float now = Time.timeSinceLevelLoad;

            // RUN! (or any time outside infiltration) cancels events cleanly; the speaker joins the panic.
            var panic = TrashPandas.Runtime.Panic.PanicDirector.Instance;
            bool infiltrating = !suspicion.Caught && (!panic || panic.Phase == TrashPandas.Runtime.Panic.RoundPhase.Infiltration);
            if (!infiltrating)
            {
                if (_offline.Phase != (byte)EventPhase.Idle)
                {
                    ReleaseSpeaker(restoreSpeed: false); // the panic owns their speed now
                    _scheduler.EventFinished(now);
                    SetPhase(EventPhase.Idle, 0f);
                    Publish();
                }
                return;
            }

            var phase = (EventPhase)_offline.Phase;
            switch (phase)
            {
                case EventPhase.Idle:
                    bool due = FirstEventOverride > 0f && _serial == 0 ? now >= FirstEventOverride : _scheduler.IsDue(now);
                    if (due) StartWarning(coat, now);
                    break;
                case EventPhase.Warning:
                    if (_speaker) _speaker.GoTo(MeetingPoint(coat));
                    if (now >= _phaseEndsAt) StartEngaged(coat, now);
                    break;
                case EventPhase.Engaged:
                    if (_speaker) { _speaker.Stop(); _speaker.LookAt(coat.ChestWorld); }
                    if (now >= _phaseEndsAt) Resolve(suspicion, now);
                    break;
                case EventPhase.Resolved:
                    if (now >= _phaseEndsAt) { ReleaseSpeaker(restoreSpeed: true); _scheduler.EventFinished(now); SetPhase(EventPhase.Idle, 0f); }
                    break;
            }
            _offline.SecondsLeft = Mathf.Max(0f, _phaseEndsAt - now);
            Publish();
        }

        static Vector3 MeetingPoint(TrenchcoatBody coat) => coat.transform.position + coat.transform.forward * 1.6f;

        void StartWarning(TrenchcoatBody coat, float now)
        {
            // Who comes over (never the same person twice in a row), among the speakers in this scene.
            var pawns = FindObjectsByType<NpcPawn>(FindObjectsSortMode.InstanceID);
            var available = new HashSet<string>();
            foreach (var p in pawns) if (!string.IsNullOrEmpty(p.SpeakerId)) available.Add(p.SpeakerId);
            int index = EventPicker.Pick(SocialEventCatalog.All, available, _scheduler, _random);
            if (index < 0) { _scheduler.EventFinished(now); return; }
            var ev = SocialEventCatalog.All[index];
            foreach (var p in pawns) if (p.SpeakerId == ev.Speaker) { _speaker = p; break; }

            float farthest = 0f;
            foreach (var r in FindObjectsByType<RaccoonController>(FindObjectsSortMode.None))
                farthest = Mathf.Max(farthest, Vector3.Distance(r.transform.position, coat.transform.position));
            float warning = WarningTime.Compute(farthest);

            // Walk at whatever pace makes them arrive about when the warning ends.
            _speakerBaseSpeed = _speaker.Speed;
            float walk = Vector3.Distance(_speaker.transform.position, MeetingPoint(coat));
            _speaker.SetSpeed(Mathf.Clamp(walk / warning, 0.8f, 3f));
            _speakerBrain = null;
            if (SuspicionDirector.Instance)
                foreach (var b in SuspicionDirector.Instance.Brains) if (b.Pawn == _speaker) { _speakerBrain = b; b.Busy = true; }

            _serial++;
            _offline.EventIndex = (byte)index;
            _offline.Serial = _serial;
            _offline.HeadOutcome = _offline.ArmsOutcome = _offline.LegsOutcome = 0;
            _offline.Arms = _offline.Legs = 0; // nobody knows what the body will have to do yet
            _offline.ResultDelta = 0f;
            SetPhase(EventPhase.Warning, now + warning);
        }

        void StartEngaged(TrenchcoatBody coat, float now)
        {
            // Who is in the coat is decided the moment the conversation starts (late arrivals don't count).
            _presentAtStart = PresentParts();
            _responses.Clear();
            // The surprise: what arms and legs must do (and the answer order) is decided right now.
            var roll = EventRoll.Roll(_random);
            _offline.Arms = (byte)roll.Arms;
            _offline.Legs = (byte)roll.Legs;
            _offline.Order = roll.Order;
            SetPhase(EventPhase.Engaged, now + ResponseWindow);
        }

        void Resolve(SuspicionDirector suspicion, float now)
        {
            var ev = Current;
            var result = EventResolver.Resolve(ev, _presentAtStart, role =>
            {
                int? player = PlayerFor(role);
                return player.HasValue && _responses.TryGetValue(player.Value, out var r) ? r : (EventResponse?)null;
            });
            suspicion.AdjustSuspicion(result.Delta);
            _offline.ResultDelta = result.Delta;
            foreach (var part in result.Parts)
            {
                byte code = (byte)(part.Outcome + 1);
                if (part.Role == EventRole.Head) _offline.HeadOutcome = code;
                else if (part.Role == EventRole.Arms) _offline.ArmsOutcome = code;
                else _offline.LegsOutcome = code;
            }
            SetPhase(EventPhase.Resolved, now + ResultDisplay);
        }

        static BodyPart PresentParts()
        {
            if (SimulationAuthority.IsOnline)
            {
                var slots = SessionHost.Instance ? SessionHost.Instance.Roster.Slots : null;
                return slots?.ControlledParts ?? BodyPart.None;
            }
            return TrenchcoatController.Instance ? TrenchcoatController.Instance.ControlledParts : BodyPart.None;
        }

        /// <summary>The player whose seat covers this role (arms → the left arm's player, legs → the left leg's).</summary>
        static int? PlayerFor(EventRole role)
        {
            BodyPart part = role == EventRole.Head ? BodyPart.Head : role == EventRole.Arms ? BodyPart.ArmLeft : BodyPart.LegLeft;
            if (SimulationAuthority.IsOnline)
            {
                var slots = SessionHost.Instance ? SessionHost.Instance.Roster.Slots : null;
                if (slots == null) return null;
                for (int i = 0; i < slots.SlotCount; i++) if ((slots.PartsOf(i) & part) != 0) return slots.OccupantOf(i);
                return null;
            }
            return 0; // offline: the person at the keyboard answers for every role (see TrenchcoatController)
        }

        void ReleaseSpeaker(bool restoreSpeed)
        {
            if (_speakerBrain != null) _speakerBrain.Busy = false;
            if (_speaker && restoreSpeed) _speaker.SetSpeed(_speakerBaseSpeed);
            _speaker = null;
            _speakerBrain = null;
        }

        void SetPhase(EventPhase p, float endsAt)
        {
            _offline.Phase = (byte)p;
            _phaseEndsAt = endsAt;
        }

        void Publish()
        {
            if (IsSpawned && IsServer && !_offline.Equals(_net.Value)) _net.Value = _offline;
        }
    }
}
