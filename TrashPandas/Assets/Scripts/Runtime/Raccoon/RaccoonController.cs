using UnityEngine;

namespace TrashPandas.Runtime.Raccoon
{
    /// <summary>A loose raccoon: runs, jumps, climbs Climbable surfaces, crouches under tables.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class RaccoonController : MonoBehaviour
    {
        public Transform CameraTransform;
        public float RunSpeed = 4.5f;
        public float JumpVelocity = 5.5f;
        public float Gravity = -20f;
        public float ClimbSpeed = 2.5f;
        public float StandHeight = 0.6f;
        public float CrouchHeight = 0.3f;

        CharacterController _cc;
        Vector2 _move;
        bool _jumpPressed;
        bool _crouchHeld;
        float _verticalVelocity;

        public void SetInput(Vector2 move, bool jumpPressed, bool crouchHeld)
        {
            _move = move;
            _jumpPressed |= jumpPressed;   // latched until consumed in Update
            _crouchHeld = crouchHeld;
        }

        void Awake() => _cc = GetComponent<CharacterController>();

        void Update()
        {
            float height = _crouchHeld ? CrouchHeight : StandHeight;
            _cc.height = height;
            _cc.center = new Vector3(0f, height * 0.5f, 0f);

            Vector3 forward = CameraTransform ? Vector3.ProjectOnPlane(CameraTransform.forward, Vector3.up).normalized : transform.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 wish = (forward * _move.y + right * _move.x);
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            bool climbing = _move.y > 0.1f && IsFacingClimbable(wish);
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
            if (wish.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(wish), Time.deltaTime * 12f);
        }

        bool IsFacingClimbable(Vector3 wish)
        {
            if (wish.sqrMagnitude < 0.01f) return false;
            Vector3 origin = transform.position + Vector3.up * (_cc.height * 0.5f);
            return Physics.Raycast(origin, wish.normalized, out var hit, _cc.radius + 0.15f, ~0, QueryTriggerInteraction.Ignore)
                   && hit.collider.GetComponentInParent<Climbable>() != null;
        }
    }
}
