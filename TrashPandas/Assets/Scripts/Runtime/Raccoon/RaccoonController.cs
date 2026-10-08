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
        void OnEnable() { All.RemoveAll(r => !r); if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); if (Mount) Dismount(Vector3.zero); }

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
            if (!Core.Raccoons.TowerRules.CanMount(top.Bottom.CarryingHeavy, top.Frozen || top.Bottom.Frozen, top.TowerSize)) return false;
            Mount = top;
            _cc.enabled = false;
            _planar = Vector3.zero;
            _verticalVelocity = 0f;
            return true;
        }

        /// <summary>Non-owner copy online: mirror who this raccoon stands on (for counting riders), no physics.</summary>
        public void SetRemoteMount(RaccoonController mount) => Mount = mount;

        public void Dismount(Vector3 push)
        {
            if (!Mount) return;
            Mount = null;
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
            if (Mount) Dismount(impulse);
            if (RidersAbove > 0 && Core.Raccoons.TowerRules.Collapses(false, bottomHit: true)) CollapseTower();
            _planar = new Vector3(impulse.x, 0f, impulse.z);
            _verticalVelocity = Mathf.Max(_verticalVelocity, 3f);
            _stunnedUntil = Mathf.Max(_stunnedUntil, Time.time + stunSeconds);
        }

        public void SetInput(Vector2 worldMove, bool jumpPressed, bool jumpHeld, bool crouchHeld, bool runHeld = false)
        {
            if (Squad.RoundIntro.Playing) { _move = Vector2.zero; _runHeld = false; return; }
            _runHeld = runHeld;
            if (Frozen || IsStunned) { _move = Vector2.zero; _jumpHeld = false; return; }
            _move = worldMove;
            if (jumpPressed) _jump.Press(Time.time);
            _jumpHeld = jumpHeld;
            _crouchHeld = crouchHeld;
        }

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            // The default (1 mm) swallows small per-frame steps at high frame rates (sneaking on a fast PC).
            _cc.minMoveDistance = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (Squad.RoundIntro.Playing) return; // the intro animates us
            if (Mount)
            {
                // Riding: stand on the head below; jump to hop off.
                if (!Mount || !Mount.isActiveAndEnabled) { Dismount(Vector3.zero); return; }
                transform.position = Mount.HeadTop;
                if (_move.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(new Vector3(_move.x, 0f, _move.y)), TurnSpeed * dt);
                if (_jump.TryConsume(Time.time)) { Dismount(transform.forward * 1.5f + Vector3.up * JumpVelocity); }
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
            float speed = _crouchHeld ? SneakSpeed : _runHeld ? RunSpeed : WalkSpeed;
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
            if (!grounded && _verticalVelocity < 0f && AutoMount) TryLandOnHead();
        }

        /// <summary>Off for raccoons this machine doesn't own (their owner decides).</summary>
        [System.NonSerialized] public bool AutoMount = true;

        void TryLandOnHead()
        {
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
