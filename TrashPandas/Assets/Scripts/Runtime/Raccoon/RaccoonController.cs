using TrashPandas.Core.Movement;
using UnityEngine;

namespace TrashPandas.Runtime.Raccoon
{
    /// <summary>
    /// A loose raccoon with standard third-person feel: camera-relative movement with acceleration,
    /// faces where it runs, coyote time + jump buffer, heavier fall, variable jump height, climbing, crouching.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class RaccoonController : MonoBehaviour
    {
        public float WalkSpeed = 3f;
        public float RunSpeed = 5f;
        public float SneakSpeed = 1.5f;
        [Tooltip("Towers and heavy loads slow you down (1 = normal).")]
        public float SpeedMultiplier = 1f;
        /// <summary>Set from the carry state: slower under a heavy load (1 = free).</summary>
        [System.NonSerialized] public float CarryFactor = 1f;
        [Tooltip("Falling faster than this when you land makes noise.")]
        public float HardLandingSpeed = 7f;
        public float Acceleration = 30f;
        public float Deceleration = 40f;
        public float AirControl = 0.5f;
        public float TurnSpeed = 720f;
        public float JumpVelocity = 5.5f;
        public float Gravity = -20f;
        public float FallGravityMultiplier = 2f;
        public float JumpCutMultiplier = 2.5f;
        public float ClimbSpeed = 2.5f;
        public float StandHeight = 0.6f;
        public float CrouchHeight = 0.3f;

        /// <summary>Which player this raccoon belongs to (set where the simulation runs).</summary>
        public int PlayerId = -1;
        /// <summary>Caught in the panic: no more control.</summary>
        public bool Frozen;
        public bool IsStunned => Time.time < _stunnedUntil;
        public bool IsRunning => _runHeld && !_crouchHeld && _planar.sqrMagnitude > 1f;
        public bool IsSneaking => _crouchHeld;
        public float PlanarSpeed => _planar.magnitude;
        /// <summary>Sneaking as far as this machine can tell (remote copies: moving slowly or standing still crouched is unknown, so assume yes).</summary>
        public bool IsSneakingOrRemote => enabled ? _crouchHeld : true;

        // --- Drain pipes: crawl through a real tunnel (W forward, S back) -------------------------------
        Core.Raccoons.TunnelPath _tunnel;
        float _tunnelS;
        Vector3 _tunnelFlatDir, _exitForwardEnd, _exitForwardStart;
        bool _remoteInside;
        public const float CrawlSpeed = 3f;

        /// <summary>Inside a drain pipe.</summary>
        public bool Crawling => _tunnel != null;
        /// <summary>The overall direction of the tunnel you're in (for the camera).</summary>
        public Vector3 TunnelDirection => _tunnelFlatDir;
        /// <summary>Hidden inside something (pipe or trash can) — as this machine knows it.</summary>
        public bool HiddenInside => Crawling || InCan || _remoteInside;
        public void SetRemoteInside(bool inside) => _remoteInside = inside;

        public void EnterTunnel(Core.Raccoons.TunnelPath path, Vector3 exitForwardAtEnd, Vector3 exitForwardAtStart)
        {
            if (Crawling || InCan || Frozen) return;
            if (!ReferenceEquals(Mount, null)) Dismount(Vector3.zero);
            _tunnel = path;
            _tunnelS = 0.05f;
            Vector3 flat = path.PointAt(path.Length) - path.PointAt(0f);
            flat.y = 0f;
            _tunnelFlatDir = flat.sqrMagnitude > 1e-4f ? flat.normalized : transform.forward;
            _exitForwardEnd = exitForwardAtEnd;
            _exitForwardStart = exitForwardAtStart;
            _cc.enabled = false;
            _planar = Vector3.zero;
            _verticalVelocity = 0f;
            Ui.Sfx.Play(Ui.Sound.Throw, transform.position, 0.6f);
        }

        void TickCrawl(float dt)
        {
            // W/S along the tunnel (the camera looks down the tunnel, so "forward" input means "deeper").
            float along = Vector3.Dot(new Vector3(_move.x, 0f, _move.y), _tunnelFlatDir);
            _tunnelS += along * CrawlSpeed * dt;
            Vector3 dir = _tunnel.DirectionAt(_tunnelS);
            Vector3 face = Mathf.Abs(dir.y) > 0.7f ? _tunnelFlatDir : new Vector3(dir.x, 0f, dir.z).normalized;
            if (along < -0.1f) face = -face;
            transform.SetPositionAndRotation(_tunnel.PointAt(_tunnelS) + Vector3.down * 0.2f, Quaternion.LookRotation(face));
            if (_tunnelS >= _tunnel.Length) ExitTunnel(_tunnel.PointAt(_tunnel.Length), _exitForwardEnd);
            else if (_tunnelS <= 0f) ExitTunnel(_tunnel.PointAt(0f), _exitForwardStart);
        }

        void ExitTunnel(Vector3 mouth, Vector3 outward)
        {
            _tunnel = null;
            Vector3 at = new Vector3(mouth.x, 0.05f, mouth.z) + new Vector3(outward.x, 0f, outward.z).normalized * 0.7f;
            TeleportTo(at);
            transform.rotation = Quaternion.LookRotation(new Vector3(outward.x, 0f, outward.z).normalized);
            _cc.enabled = true;
            _verticalVelocity = 2.5f; // pop out
            Ui.Sfx.Play(Ui.Sound.Grab, at, 0.8f);
            Ui.DebugChecklist.Mark("pipe");
        }

        // --- Trash cans: E to hop in (lid opens and closes), look around, E or Space to hop out ----------
        public Squad.Hideout InCan { get; private set; }

        public void EnterCan(Squad.Hideout can)
        {
            if (!can || Crawling || InCan || Frozen || can.Occupant) return;
            if (!ReferenceEquals(Mount, null)) Dismount(Vector3.zero);
            InCan = can;
            if (Net.SimulationAuthority.IsSimulating) Panic.NemesisDirector.Instance?.OnHid(can);
            _cc.enabled = false;
            _planar = Vector3.zero;
            StartCoroutine(can.HopIn(this));
            Ui.DebugChecklist.Mark("trashcan");
        }

        /// <param name="kicked">A human kicked the can: you tumble out dazed.</param>
        public void ExitCan(bool kicked = false)
        {
            var can = InCan;
            if (!can) return;
            InCan = null;
            StartCoroutine(can.HopOut(this, kicked));
        }

        /// <summary>Called by the can when the hop-out animation lands.</summary>
        public void LandedOutOfCan(bool kicked)
        {
            _cc.enabled = true;
            _verticalVelocity = kicked ? 4f : 2.5f;
            if (kicked) _stunnedUntil = Time.time + 1f;
        }


        // --- Raccoon towers ---------------------------------------------------------------------------
        /// <summary>The raccoon I'm standing on (null = on my own feet).</summary>
        public RaccoonController Mount { get; private set; }
        /// <summary>Raised on the bottom raccoon's machine when the tower should fall (it ran, or got hit).</summary>
        public event System.Action Collapsed;
        /// <summary>Who can ride this raccoon right now (set by the carry state: not while hauling something heavy).</summary>
        [System.NonSerialized] public bool CarryingHeavy;
        /// <summary>Riders seen on this machine (anyone whose Mount is me, directly or higher up).</summary>
        public int RidersAbove
        {
            get
            {
                int n = 0;
                foreach (var r in All) if (r && r != this && r.IsAbove(this)) n++;
                return n;
            }
        }
        public RaccoonController Bottom { get { var b = this; while (b.Mount) b = b.Mount; return b; } }
        public int TowerSize => 1 + Bottom.RidersAbove;
        public Vector3 HeadTop => transform.position + Vector3.up * (StandHeight + 0.02f);

        bool IsAbove(RaccoonController other) { var m = Mount; while (m) { if (m == other) return true; m = m.Mount; } return false; }

        static readonly System.Collections.Generic.List<RaccoonController> All = new System.Collections.Generic.List<RaccoonController>();
        public static System.Collections.Generic.IReadOnlyList<RaccoonController> Registered => All;
        // Registered for their whole life: other players' raccoons are disabled copies but still count (towers).
        void OnDestroy()
        {
            All.Remove(this);
            if (RidersAbove > 0) CollapseTower(); // whoever stood on me falls (escaped, despawned, disconnected)
        }

        /// <summary>Stand on <paramref name="target"/>'s head (if the tower rules allow it).</summary>
        public bool TryMount(RaccoonController target)
        {
            if (!target || target == this || target.IsAbove(this)) return false;
            // Climb to the top of that tower.
            var top = target;
            for (bool found = true; found;)
            {
                found = false;
                foreach (var r in All) if (r && r != this && r.Mount == top) { top = r; found = true; break; }
            }
            if (!Core.Raccoons.TowerRules.CanMount(top.Bottom.CarryingHeavy, top.Frozen || top.Bottom.Frozen, top.TowerSize, IsCaught(top) || IsCaught(top.Bottom))) return false;
            Mount = top;
            _hopOff = false;
            DroppedFromAbove = false;
            _cc.enabled = false;
            _planar = Vector3.zero;
            _verticalVelocity = 0f;
            return true;
        }

        // --- Ledges: jump at something just out of reach, grab the edge, Space again to climb up ----------
        public bool Hanging { get; private set; }
        public bool Climbing { get; private set; }
        Vector3 _ledgeTop, _ledgeFacing;
        bool _climbRequested;
        float _noLedgeUntil;
        public const float LedgeMin = 0.45f, LedgeMax = 1.25f;

        void TryGrabLedge()
        {
            if (Time.time < _noLedgeUntil || Frozen || !ReferenceEquals(Mount, null)) return;
            Vector3 wish = new Vector3(_move.x, 0f, _move.y);
            if (wish.sqrMagnitude < 0.2f) return;
            Vector3 fwd = transform.forward;
            Vector3 chest = transform.position + Vector3.up * 0.35f;
            if (!Physics.Raycast(chest, fwd, out var wall, _cc.radius + 0.35f, ~0, QueryTriggerInteraction.Ignore)) return;
            if (Mathf.Abs(wall.normal.y) > 0.3f || wall.collider.GetComponentInParent<RaccoonController>() || wall.collider.GetComponentInParent<Npc.NpcPawn>() || wall.collider.GetComponentInParent<Grabbing.Grabbable>()) return;
            // Is there a top we can reach from here?
            Vector3 probe = wall.point - wall.normal * 0.25f + Vector3.up * (LedgeMax + 0.3f);
            if (!Physics.Raycast(probe, Vector3.down, out var top, LedgeMax + 0.3f, ~0, QueryTriggerInteraction.Ignore)) return;
            float rise = top.point.y - transform.position.y;
            if (top.normal.y < 0.7f || rise < LedgeMin || rise > LedgeMax) return;
            // Room to stand up there?
            if (Physics.CheckCapsule(top.point + Vector3.up * 0.25f, top.point + Vector3.up * 0.5f, 0.15f, ~0, QueryTriggerInteraction.Ignore)) return;
            Hanging = true;
            _climbRequested = false;
            _ledgeTop = top.point;
            _ledgeFacing = -new Vector3(wall.normal.x, 0f, wall.normal.z).normalized;
            _cc.enabled = false;
            _planar = Vector3.zero;
            _verticalVelocity = 0f;
            transform.SetPositionAndRotation(new Vector3(wall.point.x, top.point.y - 0.55f, wall.point.z) + wall.normal * (_cc.radius + 0.05f), Quaternion.LookRotation(_ledgeFacing));
            Ui.Sfx.Play(Ui.Sound.Grab, transform.position, 0.6f);
            Ui.DebugChecklist.Mark("ledge");
        }

        void TickHang()
        {
            if (Climbing) return;
            Vector3 wish = new Vector3(_move.x, 0f, _move.y);
            if (wish.sqrMagnitude > 0.2f && Vector3.Dot(wish.normalized, _ledgeFacing) < -0.5f)
            {
                // Pull back: let go.
                Hanging = false;
                _cc.enabled = true;
                _noLedgeUntil = Time.time + 0.5f;
                return;
            }
            if (_climbRequested) StartCoroutine(ClimbUp());
        }

        System.Collections.IEnumerator ClimbUp()
        {
            Climbing = true;
            _climbRequested = false;
            Vector3 from = transform.position, to = _ledgeTop + _ledgeFacing * 0.35f + Vector3.up * 0.03f;
            Ui.Sfx.Play(Ui.Sound.Jump, from, 0.5f);
            // Up first (scramble), then over the edge.
            for (float t = 0f; t < 0.4f; t += Time.deltaTime)
            {
                float k = t / 0.4f;
                Vector3 p = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k));
                p.y = Mathf.Lerp(from.y, to.y + 0.15f, Mathf.Clamp01(k * 1.8f));
                transform.position = p;
                yield return null;
            }
            transform.position = to;
            Climbing = false;
            Hanging = false;
            _cc.enabled = true;
            _noLedgeUntil = Time.time + 0.3f;
            Ui.DebugChecklist.Mark("climb");
        }

        /// <summary>Move instantly (into the cage), off any tower.</summary>
        public void TeleportTo(Vector3 position)
        {
            if (!ReferenceEquals(Mount, null)) Dismount(Vector3.zero);
            bool was = _cc.enabled;
            _cc.enabled = false;
            var nt = GetComponent<Unity.Netcode.Components.NetworkTransform>();
            if (nt && nt.IsSpawned && nt.CanCommitToTransform) nt.Teleport(position, transform.rotation, transform.localScale); // remote copies jump, they don't slide
            else transform.position = position;
            _cc.enabled = was;
            _planar = Vector3.zero;
            _verticalVelocity = 0f;
        }

        /// <summary>Non-owner copy online: mirror who this raccoon stands on (for counting riders), no physics.</summary>
        public void SetRemoteMount(RaccoonController mount) => Mount = mount;

        /// <summary>Caught according to the replicated round state (Frozen is only set on the owner and the host).</summary>
        static bool IsCaught(RaccoonController r)
        {
            var pd = Panic.PanicDirector.Instance;
            return pd && r.PlayerId >= 0 && pd.Snapshot.OutcomeOf(r.PlayerId) == Core.Panic.PlayerOutcome.Caught;
        }

        public void Dismount(Vector3 push)
        {
            if (ReferenceEquals(Mount, null)) return;
            Mount = null;
            _noMountUntil = Time.time + 1f; // tumbling off isn't climbing back on
            if (!enabled) return;
            _cc.enabled = true;
            _planar = new Vector3(push.x, 0f, push.z);
            _verticalVelocity = Mathf.Max(push.y, 2f);
        }

        /// <summary>Owner of the bottom raccoon: everyone above falls off.</summary>
        public void CollapseTower()
        {
            Collapsed?.Invoke();
            foreach (var r in All.ToArray())
                if (r && r != this && r.IsAbove(this))
                {
                    var dir = Random.insideUnitCircle.normalized * 2.5f;
                    r.Dismount(new Vector3(dir.x, 3f, dir.y));
                }
        }
        /// <summary>Raised where the noise happens (running is reported continuously by the owner).</summary>
        public event System.Action<Core.Raccoons.NoiseKind, Vector3> Noise;
        float _stunnedUntil;

        readonly JumpAssist _jump = new JumpAssist();
        CharacterController _cc;
        Vector2 _move;
        bool _jumpHeld;
        bool _crouchHeld;
        bool _runHeld;
        bool _wasGrounded = true;
        Vector3 _planar;
        float _verticalVelocity;

        /// <param name="worldMove">World XZ direction (camera-relative), magnitude 0..1.</param>
        /// <summary>Knocked by a broom (or the cat): flung along <paramref name="impulse"/> and dizzy for a moment.</summary>
        public void ApplyHit(Vector3 impulse, float stunSeconds)
        {
            if (Frozen) return; // caged: nothing knocks you out of the carrier
            // Juice: if this is the raccoon on my screen, the camera feels it.
            var shake = Cameras.CameraShake.Instance;
            var cam = Camera.main;
            if (shake && cam && Vector3.Distance(cam.transform.position, transform.position) < 6f) { shake.Kick(0.55f); shake.HitStop(0.07f); }
            Ui.Sfx.Play(Ui.Sound.Hit, transform.position);
            if (Mount) Dismount(impulse);
            if (RidersAbove > 0 && Core.Raccoons.TowerRules.Collapses(false, bottomHit: true)) CollapseTower();
            _planar = new Vector3(impulse.x, 0f, impulse.z);
            _verticalVelocity = Mathf.Max(_verticalVelocity, 3f);
            _stunnedUntil = Mathf.Max(_stunnedUntil, Time.time + stunSeconds);
        }

        public void SetInput(Vector2 worldMove, bool jumpPressed, bool jumpHeld, bool crouchHeld, bool runHeld = false)
        {
            if (Squad.RoundIntro.Playing) { _move = Vector2.zero; _runHeld = false; return; }
            if (InCan && jumpPressed) { ExitCan(); return; }
            if (Hanging && jumpPressed) { _climbRequested = true; return; }
            _runHeld = runHeld;
            if (Frozen || IsStunned) { _move = Vector2.zero; _jumpHeld = false; return; }
            _move = worldMove;
            if (jumpPressed && !ReferenceEquals(Mount, null)) _hopOff = true; // riding: the jump rule (grounded) doesn't apply
            else if (jumpPressed) _jump.Press(Time.time);
            _jumpHeld = jumpHeld;
            _crouchHeld = crouchHeld;
        }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            All.RemoveAll(r => !r);
            if (!All.Contains(this)) All.Add(this);
            // The default (1 mm) swallows small per-frame steps at high frame rates (sneaking on a fast PC).
            _cc.minMoveDistance = 0f;
            // Low steps: walking into another raccoon mustn't carry you up onto its head (only a jump does).
            _cc.stepOffset = Mathf.Min(_cc.stepOffset, 0.12f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Squad.RoundIntro.Playing) return; // the intro animates us
            if (Crawling) { TickCrawl(dt); return; }
            if (Hanging) { TickHang(); return; }
            if (InCan) return; // tucked in a trash can
            if (!ReferenceEquals(Mount, null))
            {
                // Riding: stand on the head below; jump to hop off. (A destroyed mount compares equal to null.)
                if (!Mount || !Mount.gameObject.activeInHierarchy) { Dismount(Vector3.zero); return; }
                transform.position = Mount.HeadTop;
                if (_move.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(new Vector3(_move.x, 0f, _move.y)), TurnSpeed * dt);
                if (_hopOff) { _hopOff = false; Dismount(transform.forward * 1.5f + Vector3.up * JumpVelocity); Ui.DebugChecklist.Mark("hopoff"); }
                return;
            }
            float height = _crouchHeld ? CrouchHeight : StandHeight;
            _cc.height = height;
            _cc.center = new Vector3(0f, height * 0.5f, 0f);

            bool grounded = _cc.isGrounded;
            _jump.SetGrounded(grounded, Time.time);

            if (Frozen) { _move = Vector2.zero; }
            int riders = RidersAbove;
            if (riders > 0 && Core.Raccoons.TowerRules.Collapses(bottomRunning: _runHeld && _move.sqrMagnitude > 0.1f, bottomHit: false)) { CollapseTower(); riders = 0; }
            float speed = Pushing ? WalkSpeed * 0.45f : _crouchHeld ? SneakSpeed : _runHeld ? RunSpeed : WalkSpeed;
            Vector3 wish = new Vector3(_move.x, 0f, _move.y) * speed * SpeedMultiplier * CarryFactor * Core.Raccoons.TowerRules.SpeedFactor(riders);
            if (grounded && !_wasGrounded && _verticalVelocity < -HardLandingSpeed) Noise?.Invoke(Core.Raccoons.NoiseKind.HardLanding, transform.position);
            _wasGrounded = grounded;
            float rate = IsStunned ? Deceleration * 0.25f : wish.sqrMagnitude > _planar.sqrMagnitude ? Acceleration : Deceleration;
            _planar = Vector3.MoveTowards(_planar, wish, rate * (grounded ? 1f : AirControl) * dt);
            if (IsStunned) transform.Rotate(0f, 720f * dt, 0f); // dizzy spin
            else if (wish.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(wish), TurnSpeed * dt);

            bool climbing = wish.sqrMagnitude > 0.01f && IsFacingClimbable();
            if (climbing)
            {
                _verticalVelocity = ClimbSpeed;
            }
            else if (_jump.TryConsume(Time.time))
            {
                _verticalVelocity = JumpVelocity;
                _jumpedSinceGrounded = true;
            }
            else if (grounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }
            else
            {
                float g = Gravity;
                if (_verticalVelocity < 0f) g *= FallGravityMultiplier;
                else if (!_jumpHeld) g *= JumpCutMultiplier; // released early: shorter jump
                _verticalVelocity += g * dt;
            }

            _cc.Move((_planar + Vector3.up * _verticalVelocity) * dt);
            if (_cc.isGrounded && _verticalVelocity <= 0f) _jumpedSinceGrounded = false;
            // Only a deliberate jump lands you on someone's head (bumping into them from behind doesn't).
            if (!grounded && _verticalVelocity < 0f && AutoMount && (_jumpedSinceGrounded || DroppedFromAbove)) TryLandOnHead();
            if (!grounded && _verticalVelocity < 2f && !Hanging) TryGrabLedge();
            // Moving inside a bush shakes it and makes a little noise (humans nearby turn around).
            if (_planar.magnitude > 0.5f && Time.time >= _nextRustle)
            {
                var bush = Squad.HidingSpot.At(transform.position);
                if (bush && bush.Kind == Squad.HidingKind.Bush)
                {
                    _nextRustle = Time.time + 0.45f;
                    bush.Rustle();
                    Noise?.Invoke(Core.Raccoons.NoiseKind.Rustle, transform.position);
                    Ui.Sfx.Play(Ui.Sound.Throw, transform.position, 0.35f);
                }
            }
        }

        float _nextPush;

        /// <summary>Bumping into light things knocks them about (Astro Bot: every touch gets a reaction).</summary>
        /// <summary>Paws on something and shoving it (for the pose).</summary>
        public bool Pushing => Time.time < _pushingUntil;
        /// <summary>On its feet on the ground (not crawling, hanging, riding): banana peels get it.</summary>
        public bool IsGroundedForPeel => _cc && _cc.enabled && _cc.isGrounded && !Crawling && !Hanging && ReferenceEquals(Mount, null);
        float _pushingUntil;

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var pushable = hit.collider.GetComponentInParent<Squad.Pushable>();
            if (pushable && _cc.isGrounded && Mathf.Abs(hit.normal.y) < 0.5f)
            {
                Vector3 into = -new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;
                Vector3 wish = new Vector3(_move.x, 0f, _move.y);
                if (wish.sqrMagnitude > 0.1f && Vector3.Dot(wish.normalized, into) > 0.5f)
                {
                    _pushingUntil = Time.time + 0.2f;
                    var pusherNet = GetComponent<Net.NetworkedRaccoon>();
                    Ui.DebugChecklist.Mark("push");
                    if (pushable.name.StartsWith("Wardrobe")) Ui.DebugChecklist.Mark("secret");
                    if (pusherNet && pusherNet.IsSpawned && !pusherNet.IsServer) pusherNet.PushObjectRpc(pushable.Index, into);
                    else pushable.Push(into, Time.deltaTime);
                    return;
                }
            }
            var rb = hit.rigidbody;
            if (!rb || rb.mass > 2f || hit.moveDirection.y < -0.3f || Time.time < _nextPush) return;
            Vector3 impulse = new Vector3(hit.moveDirection.x, 0.15f, hit.moveDirection.z) * (1.2f + _planar.magnitude * 0.35f);
            if (!rb.isKinematic) { rb.AddForceAtPosition(impulse, hit.point, ForceMode.Impulse); return; }
            // Online client: props are simulated by the host; ask it to push.
            var net = GetComponent<Net.NetworkedRaccoon>();
            var g = rb.GetComponent<Grabbing.Grabbable>();
            if (net && net.IsSpawned && !net.IsServer && g && !g.IsHeld && Squad.CarryDirector.Instance)
            {
                _nextPush = Time.time + 0.15f;
                net.PushPropRpc(Squad.CarryDirector.Instance.IndexOf(g), impulse, hit.point);
            }
        }

        /// <summary>Off for raccoons this machine doesn't own (their owner decides).</summary>
        [System.NonSerialized] public bool AutoMount = true;

        float _noMountUntil;
        float _nextRustle;
        bool _hopOff;
        bool _jumpedSinceGrounded;
        /// <summary>Dev bots that place a raccoon above another one.</summary>
        [System.NonSerialized] public bool DroppedFromAbove;

        void TryLandOnHead()
        {
            if (Time.time < _noMountUntil) return;
            foreach (var r in All)
            {
                if (!r || r == this || r.IsAbove(this)) continue;
                Vector3 d = transform.position - r.HeadTop;
                if (Mathf.Abs(d.y) < 0.18f && new Vector2(d.x, d.z).magnitude < 0.3f) { TryMount(r); return; }
            }
        }

        bool IsFacingClimbable()
        {
            Vector3 origin = transform.position + Vector3.up * (_cc.height * 0.5f);
            return Physics.Raycast(origin, transform.forward, out var hit, _cc.radius + 0.15f, ~0, QueryTriggerInteraction.Ignore)
                   && hit.collider.GetComponentInParent<Climbable>() != null;
        }
    }
}
