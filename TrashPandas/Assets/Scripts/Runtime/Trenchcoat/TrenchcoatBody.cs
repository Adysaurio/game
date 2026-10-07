using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Runtime.Trenchcoat
{
    /// <summary>Greybox trenchcoat: a wobbly Rigidbody capsule with procedural head and hands.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TrenchcoatBody : MonoBehaviour
    {
        public Transform Torso;
        public Transform Head;
        public Transform LeftHand;
        public Transform RightHand;

        public float MoveSpeed = 2.2f;
        public float TurnSpeed = 140f;
        public float JumpVelocity = 4.5f;
        public float JumpCooldown = 0.6f;
        public float WobbleAmount = 6f;
        public float HandSpeed = 8f;

        static readonly Vector3 LeftShoulder = new Vector3(-0.35f, 1.35f, 0f);
        static readonly Vector3 RightShoulder = new Vector3(0.35f, 1.35f, 0f);
        static readonly Vector3 HeadRest = new Vector3(0f, 1.75f, 0f);
        static readonly Vector3 TorsoRest = new Vector3(0f, 0.9f, 0f);

        Rigidbody _rb;
        BodyIntent _intent;
        float _lastJumpTime = -10f;
        float _wobblePhase;

        public void SetIntent(BodyIntent intent) => _intent = intent;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void FixedUpdate()
        {
            float speed = _intent.Crouch ? MoveSpeed * 0.5f : MoveSpeed;
            Vector3 planar = transform.forward * (_intent.Forward * speed);
            _rb.linearVelocity = new Vector3(planar.x, _rb.linearVelocity.y, planar.z);
            _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0f, _intent.Turn * TurnSpeed * Time.fixedDeltaTime, 0f));

            if (_intent.Jump && IsGrounded() && Time.time - _lastJumpTime > JumpCooldown)
            {
                _lastJumpTime = Time.time;
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, JumpVelocity, _rb.linearVelocity.z);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _wobblePhase += dt * (2f + Mathf.Abs(_intent.Forward) * 8f);

            // Torso: collapses when no legs, wobbles side to side while walking.
            Vector3 torsoPos = _intent.Collapsed ? TorsoRest + Vector3.down * 0.5f
                             : _intent.Crouch ? TorsoRest + Vector3.down * 0.25f
                             : TorsoRest;
            float sway = Mathf.Sin(_wobblePhase) * WobbleAmount * (0.3f + Mathf.Abs(_intent.Forward));
            float lean = _intent.Collapsed ? 25f : _intent.Forward * 8f;
            Torso.localPosition = Vector3.Lerp(Torso.localPosition, torsoPos, dt * 6f);
            Torso.localRotation = Quaternion.Slerp(Torso.localRotation, Quaternion.Euler(lean, 0f, sway), dt * 6f);

            // Head: follows look, or slumps sideways.
            Quaternion headRot = _intent.HeadSlumped
                ? Quaternion.Euler(20f, 0f, 60f)
                : Quaternion.Euler(-_intent.HeadPitch, _intent.HeadYaw, 0f);
            Head.localPosition = Vector3.Lerp(Head.localPosition, torsoPos - TorsoRest + HeadRest, dt * 6f);
            Head.localRotation = Quaternion.Slerp(Head.localRotation, headRot, dt * 5f);

            // Hands: reach for targets, or dangle like noodles.
            float dangle = Mathf.Sin(_wobblePhase * 1.3f) * 0.12f;
            Vector3 drop = torsoPos - TorsoRest;
            Vector3 left = _intent.LeftArmLimp ? LeftShoulder + drop + new Vector3(-0.05f, -0.75f, dangle) : _intent.LeftHandTarget;
            Vector3 right = _intent.RightArmLimp ? RightShoulder + drop + new Vector3(0.05f, -0.75f, -dangle) : _intent.RightHandTarget;
            LeftHand.localPosition = Vector3.Lerp(LeftHand.localPosition, left, dt * HandSpeed);
            RightHand.localPosition = Vector3.Lerp(RightHand.localPosition, right, dt * HandSpeed);
            LeftHand.localScale = Vector3.one * (_intent.LeftGrab ? 0.13f : 0.18f);
            RightHand.localScale = Vector3.one * (_intent.RightGrab ? 0.13f : 0.18f);
        }

        bool IsGrounded() =>
            Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.2f, ~0, QueryTriggerInteraction.Ignore);
    }
}
