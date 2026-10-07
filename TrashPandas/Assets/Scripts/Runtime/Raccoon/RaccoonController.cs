using UnityEngine;

namespace TrashPandas.Runtime.Raccoon
{
    /// <summary>A loose raccoon: mouse turns, WASD moves relative to its facing; jumps, climbs, crouches.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class RaccoonController : MonoBehaviour
    {
        public float RunSpeed = 3.5f;
        public float JumpVelocity = 5.5f;
        public float Gravity = -20f;
        public float ClimbSpeed = 2.5f;
        public float StandHeight = 0.6f;
        public float CrouchHeight = 0.3f;

        CharacterController _cc;
        Vector2 _move;
        float _pendingYaw;
        bool _jumpPressed;
        bool _crouchHeld;
        float _verticalVelocity;

        /// <param name="move">WASD, x = sidestep, y = forward.</param>
        /// <param name="yawDelta">Degrees to turn this frame (mouse).</param>
        public void SetInput(Vector2 move, float yawDelta, bool jumpPressed, bool crouchHeld)
        {
            _move = move;
            _pendingYaw += yawDelta;
            _jumpPressed |= jumpPressed;   // latched until consumed in Update
            _crouchHeld = crouchHeld;
        }

        void Awake() => _cc = GetComponent<CharacterController>();

        void Update()
        {
            float height = _crouchHeld ? CrouchHeight : StandHeight;
            _cc.height = height;
            _cc.center = new Vector3(0f, height * 0.5f, 0f);

            transform.Rotate(0f, _pendingYaw, 0f);
            _pendingYaw = 0f;
            Vector3 wish = transform.forward * _move.y + transform.right * _move.x;
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            bool climbing = _move.y > 0.1f && IsFacingClimbable();
            if (climbing)
            {
                _verticalVelocity = ClimbSpeed;
            }
            else if (_cc.isGrounded)
            {
                _verticalVelocity = _jumpPressed ? JumpVelocity : -1f;
            }
            else
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
            _jumpPressed = false;

            float speed = _crouchHeld ? RunSpeed * 0.5f : RunSpeed;
            _cc.Move((wish * speed + Vector3.up * _verticalVelocity) * Time.deltaTime);
        }

        bool IsFacingClimbable()
        {
            Vector3 origin = transform.position + Vector3.up * (_cc.height * 0.5f);
            return Physics.Raycast(origin, transform.forward, out var hit, _cc.radius + 0.15f, ~0, QueryTriggerInteraction.Ignore)
                   && hit.collider.GetComponentInParent<Climbable>() != null;
        }
    }
}
