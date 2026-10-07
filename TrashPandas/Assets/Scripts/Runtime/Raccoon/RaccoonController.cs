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
        public float RunSpeed = 4f;
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

        readonly JumpAssist _jump = new JumpAssist();
        CharacterController _cc;
        Vector2 _move;
        bool _jumpHeld;
        bool _crouchHeld;
        Vector3 _planar;
        float _verticalVelocity;

        /// <param name="worldMove">World XZ direction (camera-relative), magnitude 0..1.</param>
        public void SetInput(Vector2 worldMove, bool jumpPressed, bool jumpHeld, bool crouchHeld)
        {
            _move = worldMove;
            if (jumpPressed) _jump.Press(Time.time);
            _jumpHeld = jumpHeld;
            _crouchHeld = crouchHeld;
        }

        void Awake() => _cc = GetComponent<CharacterController>();

        void Update()
        {
            float dt = Time.deltaTime;
            float height = _crouchHeld ? CrouchHeight : StandHeight;
            _cc.height = height;
            _cc.center = new Vector3(0f, height * 0.5f, 0f);

            bool grounded = _cc.isGrounded;
            _jump.SetGrounded(grounded, Time.time);

            Vector3 wish = new Vector3(_move.x, 0f, _move.y) * (_crouchHeld ? RunSpeed * 0.5f : RunSpeed);
            float rate = wish.sqrMagnitude > _planar.sqrMagnitude ? Acceleration : Deceleration;
            _planar = Vector3.MoveTowards(_planar, wish, rate * (grounded ? 1f : AirControl) * dt);
            if (wish.sqrMagnitude > 0.01f)
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
        }

        bool IsFacingClimbable()
        {
            Vector3 origin = transform.position + Vector3.up * (_cc.height * 0.5f);
            return Physics.Raycast(origin, transform.forward, out var hit, _cc.radius + 0.15f, ~0, QueryTriggerInteraction.Ignore)
                   && hit.collider.GetComponentInParent<Climbable>() != null;
        }
    }
}
