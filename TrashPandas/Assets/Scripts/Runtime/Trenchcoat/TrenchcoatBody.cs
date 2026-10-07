using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Runtime.Trenchcoat
{
    /// <summary>Greybox trenchcoat: a wobbly Rigidbody with procedural torso, head, legs, arms and hands.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class TrenchcoatBody : MonoBehaviour
    {
        public Transform Torso;
        public Transform Head;
        public Transform LeftHand;
        public Transform RightHand;
        /// <summary>Hip pivots; the visible leg hangs below each pivot.</summary>
        public Transform LeftLeg;
        public Transform RightLeg;
        /// <summary>Tubes stretched from shoulder to hand every frame.</summary>
        public Transform LeftArm;
        public Transform RightArm;

        public float MoveSpeed = 1.4f;
        public float StrafeFactor = 0.7f;
        public float TurnSpeed = 110f;
        public float JumpVelocity = 4.5f;
        public float JumpCooldown = 0.6f;
        public float WobbleAmount = 6f;
        public float StepSwing = 30f;
        public float HandSpeed = 8f;

        public static readonly Vector3 TorsoRest = new Vector3(0f, 1.15f, 0f);
        public static readonly Vector3 HeadRest = new Vector3(0f, 1.95f, 0f);
        public static readonly Vector3 LeftShoulder = new Vector3(-0.38f, 1.55f, 0f);
        public static readonly Vector3 RightShoulder = new Vector3(0.38f, 1.55f, 0f);
        public static readonly Vector3 LeftHip = new Vector3(-0.15f, 0.65f, 0f);
        public static readonly Vector3 RightHip = new Vector3(0.15f, 0.65f, 0f);
        const float ArmThickness = 0.09f;

        Rigidbody _rb;
        BodyIntent _intent;
        float _lastJumpTime = -10f;
        float _wobblePhase;
        float _pendingYaw;

        public void SetIntent(BodyIntent intent)
        {
            _intent = intent;
            _pendingYaw += intent.YawDelta; // mouse turns arrive per frame; physics consumes them per step
        }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void FixedUpdate()
        {
            float speed = _intent.Crouch ? MoveSpeed * 0.5f : MoveSpeed;
            Vector3 planar = (transform.forward * _intent.Forward + transform.right * (_intent.Strafe * StrafeFactor)) * speed;
            _rb.linearVelocity = new Vector3(planar.x, _rb.linearVelocity.y, planar.z);
            float yaw = _intent.Turn * TurnSpeed * Time.fixedDeltaTime + _pendingYaw;
            _pendingYaw = 0f;
            _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0f, yaw, 0f));

            if (_intent.Jump && IsGrounded() && Time.time - _lastJumpTime > JumpCooldown)
            {
                _lastJumpTime = Time.time;
                _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, JumpVelocity, _rb.linearVelocity.z);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float motion = Mathf.Clamp01(Mathf.Abs(_intent.Forward) + Mathf.Abs(_intent.Strafe));
            _wobblePhase += dt * (2f + motion * 8f);

            // Torso: collapses when no legs, wobbles side to side while walking.
            Vector3 drop = _intent.Collapsed ? Vector3.down * 0.5f
                         : _intent.Crouch ? Vector3.down * 0.25f
                         : Vector3.zero;
            float sway = Mathf.Sin(_wobblePhase) * WobbleAmount * (0.3f + motion);
            float lean = _intent.Collapsed ? 15f : _intent.Forward * 8f;
            Torso.localPosition = Vector3.Lerp(Torso.localPosition, TorsoRest + drop, dt * 6f);
            Torso.localRotation = Quaternion.Slerp(Torso.localRotation, Quaternion.Euler(lean, 0f, sway), dt * 6f);

            // Head: follows look, or slumps sideways.
            Quaternion headRot = _intent.HeadSlumped
                ? Quaternion.Euler(20f, 0f, 60f)
                : Quaternion.Euler(-_intent.HeadPitch, _intent.HeadYaw, 0f);
            Head.localPosition = Vector3.Lerp(Head.localPosition, HeadRest + drop, dt * 6f);
            Head.localRotation = Quaternion.Slerp(Head.localRotation, headRot, dt * 5f);

            // Legs: swing opposite each other while walking; a missing leg dangles; no legs = sitting.
            float step = Mathf.Sin(_wobblePhase) * StepSwing * motion;
            UpdateLeg(LeftLeg, LeftHip + drop, _intent.LeftLegLimp, step, dt);
            UpdateLeg(RightLeg, RightHip + drop, _intent.RightLegLimp, -step, dt);

            // Hands: reach for targets, or dangle like noodles. Arms stretch from shoulder to hand.
            float dangle = Mathf.Sin(_wobblePhase * 1.3f) * 0.12f;
            Vector3 leftShoulder = LeftShoulder + drop, rightShoulder = RightShoulder + drop;
            Vector3 left = _intent.LeftArmLimp ? leftShoulder + new Vector3(-0.05f, -0.75f, dangle) : _intent.LeftHandTarget;
            Vector3 right = _intent.RightArmLimp ? rightShoulder + new Vector3(0.05f, -0.75f, -dangle) : _intent.RightHandTarget;
            LeftHand.localPosition = Vector3.Lerp(LeftHand.localPosition, left, dt * HandSpeed);
            RightHand.localPosition = Vector3.Lerp(RightHand.localPosition, right, dt * HandSpeed);
            LeftHand.localScale = Vector3.one * (_intent.LeftGrab ? 0.13f : 0.18f);
            RightHand.localScale = Vector3.one * (_intent.RightGrab ? 0.13f : 0.18f);
            StretchArm(LeftArm, leftShoulder, LeftHand.localPosition);
            StretchArm(RightArm, rightShoulder, RightHand.localPosition);
        }

        void UpdateLeg(Transform leg, Vector3 hip, bool limp, float swing, float dt)
        {
            if (!leg) return;
            Quaternion target = _intent.Collapsed ? Quaternion.Euler(-80f, 0f, 0f)        // sitting: legs stick forward
                              : limp ? Quaternion.Euler(-25f + swing * 0.3f, 0f, 0f)      // dangling, dragged along
                              : Quaternion.Euler(swing, 0f, 0f);
            leg.localPosition = Vector3.Lerp(leg.localPosition, hip, dt * 6f);
            leg.localRotation = Quaternion.Slerp(leg.localRotation, target, dt * 8f);
        }

        static void StretchArm(Transform arm, Vector3 shoulder, Vector3 hand)
        {
            if (!arm) return;
            Vector3 delta = hand - shoulder;
            float length = Mathf.Max(delta.magnitude, 0.01f);
            arm.localPosition = shoulder + delta * 0.5f;
            arm.localRotation = Quaternion.FromToRotation(Vector3.up, delta / length);
            arm.localScale = new Vector3(ArmThickness, length * 0.5f, ArmThickness); // cylinder mesh is 2 units tall
        }

        bool IsGrounded() =>
            Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.2f, ~0, QueryTriggerInteraction.Ignore);
    }
}
