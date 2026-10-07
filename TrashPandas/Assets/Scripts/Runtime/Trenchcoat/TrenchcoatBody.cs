using TrashPandas.Core.Movement;
using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Runtime.Trenchcoat
{
    /// <summary>
    /// Greybox trenchcoat: responsive Rigidbody movement (accelerates, turns toward where it walks) with
    /// clumsiness kept visual — wobble, swinging legs, reaching or dangling arms.
    /// </summary>
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

        [Header("Movement")]
        public float MoveSpeed = 1.8f;
        public float Acceleration = 10f;
        public float Deceleration = 14f;
        public float TurnSpeed = 300f;
        public float JumpVelocity = 4.5f;
        public float FallGravityMultiplier = 1.6f;
        [Tooltip("Random twisting (deg/s) when the legs disagree.")]
        public float DiscordTwist = 90f;

        [Header("Visuals")]
        public float WobbleAmount = 6f;
        public float StepSwing = 30f;
        public float ReachLength = 1.0f;
        public float HandSpeed = 10f;

        public static readonly Vector3 TorsoRest = new Vector3(0f, 1.15f, 0f);
        public static readonly Vector3 HeadRest = new Vector3(0f, 1.95f, 0f);
        public static readonly Vector3 LeftShoulder = new Vector3(-0.38f, 1.55f, 0f);
        public static readonly Vector3 RightShoulder = new Vector3(0.38f, 1.55f, 0f);
        public static readonly Vector3 LeftHip = new Vector3(-0.15f, 0.65f, 0f);
        public static readonly Vector3 RightHip = new Vector3(0.15f, 0.65f, 0f);
        const float ArmThickness = 0.09f;

        readonly JumpAssist _jump = new JumpAssist();
        Rigidbody _rb;
        BodyIntent _intent;
        bool _jumpWasRequested;
        float _wobblePhase;
        Vector3 _lastPosition;

        /// <summary>Online clients only animate: the host simulates, NetworkTransform moves us.</summary>
        public bool VisualOnly { get; set; }

        public bool LeftReachActive => _intent.LeftReach && !_intent.LeftArmLimp;
        public bool RightReachActive => _intent.RightReach && !_intent.RightArmLimp;

        public Vector3 ShoulderWorld(bool left) => transform.TransformPoint(left ? LeftShoulder : RightShoulder);
        public Vector3 ChestWorld => transform.TransformPoint(new Vector3(0f, 1.3f, 0f));

        public void SetIntent(BodyIntent intent)
        {
            if (intent.Jump && !_jumpWasRequested && !VisualOnly) _jump.Press(Time.time);
            _jumpWasRequested = intent.Jump;
            _intent = intent;
        }

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void FixedUpdate()
        {
            if (VisualOnly || _rb.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            _jump.SetGrounded(IsGrounded(), Time.time);

            // Planar velocity eases toward the target instead of snapping.
            float speed = _intent.Crouch ? MoveSpeed * 0.5f : MoveSpeed;
            Vector3 wish = _intent.Collapsed ? Vector3.zero : new Vector3(_intent.Move.x, 0f, _intent.Move.y) * speed;
            Vector3 v = _rb.linearVelocity;
            Vector3 planar = new Vector3(v.x, 0f, v.z);
            float rate = wish.sqrMagnitude > planar.sqrMagnitude ? Acceleration : Deceleration;
            planar = Vector3.MoveTowards(planar, wish, rate * dt);

            float vy = v.y;
            if (_jump.TryConsume(Time.time)) vy = JumpVelocity;
            if (vy < 0f) vy += Physics.gravity.y * (FallGravityMultiplier - 1f) * dt;
            _rb.linearVelocity = new Vector3(planar.x, vy, planar.z);

            // Face where it walks; disagreeing legs add a twist.
            Quaternion rotation = _rb.rotation;
            if (wish.sqrMagnitude > 0.01f)
                rotation = Quaternion.RotateTowards(rotation, Quaternion.LookRotation(wish), TurnSpeed * (1f - 0.6f * _intent.Discord) * dt);
            if (_intent.Discord > 0f)
                rotation *= Quaternion.Euler(0f, Mathf.Sin(Time.time * 7f) * DiscordTwist * _intent.Discord * dt, 0f);
            _rb.MoveRotation(rotation);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            // Online clients have a kinematic body moved by NetworkTransform: derive speed from movement.
            Vector3 v = _rb && !_rb.isKinematic ? _rb.linearVelocity : (transform.position - _lastPosition) / Mathf.Max(dt, 1e-4f);
            _lastPosition = transform.position;
            float motion = Mathf.Clamp01(new Vector2(v.x, v.z).magnitude / Mathf.Max(0.01f, MoveSpeed));
            _wobblePhase += dt * (2f + motion * 8f);

            // Torso: sits when no legs, sways more when the legs disagree.
            Vector3 drop = _intent.Collapsed ? Vector3.down * 0.5f
                         : _intent.Crouch ? Vector3.down * 0.25f
                         : Vector3.zero;
            float sway = Mathf.Sin(_wobblePhase) * WobbleAmount * (0.3f + motion + 2f * _intent.Discord);
            float lean = _intent.Collapsed ? 15f : motion * 8f;
            Torso.localPosition = Vector3.Lerp(Torso.localPosition, TorsoRest + drop, dt * 6f);
            Torso.localRotation = Quaternion.Slerp(Torso.localRotation, Quaternion.Euler(lean, 0f, sway), dt * 6f);

            // Head: looks where its player looks (within a natural range), or slumps.
            Quaternion headRot = Quaternion.Euler(20f, 0f, 60f);
            if (!_intent.HeadSlumped)
            {
                Vector3 local = _intent.HeadAim == Vector3.zero ? Vector3.forward : transform.InverseTransformDirection(_intent.HeadAim);
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -75f, 75f);
                float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -40f, 40f);
                headRot = Quaternion.Euler(-pitch, yaw, 0f);
            }
            Head.localPosition = Vector3.Lerp(Head.localPosition, HeadRest + drop, dt * 6f);
            Head.localRotation = Quaternion.Slerp(Head.localRotation, headRot, dt * 8f);

            // Legs: swing while walking; a missing leg dangles; no legs = sitting.
            float step = Mathf.Sin(_wobblePhase) * StepSwing * motion;
            UpdateLeg(LeftLeg, LeftHip + drop, _intent.LeftLegLimp, step, dt);
            UpdateLeg(RightLeg, RightHip + drop, _intent.RightLegLimp, -step, dt);

            // Arms: reach where the player looks, rest at the sides, or dangle when nobody controls them.
            float dangle = Mathf.Sin(_wobblePhase * 1.3f) * 0.12f;
            Vector3 leftShoulder = LeftShoulder + drop, rightShoulder = RightShoulder + drop;
            Vector3 left = HandTarget(leftShoulder, _intent.LeftArmLimp, _intent.LeftReach, _intent.HasLeftPoint, _intent.LeftPoint, _intent.LeftAim, dangle, -1f);
            Vector3 right = HandTarget(rightShoulder, _intent.RightArmLimp, _intent.RightReach, _intent.HasRightPoint, _intent.RightPoint, _intent.RightAim, -dangle, 1f);
            LeftHand.localPosition = Vector3.Lerp(LeftHand.localPosition, left, dt * HandSpeed);
            RightHand.localPosition = Vector3.Lerp(RightHand.localPosition, right, dt * HandSpeed);
            LeftHand.localScale = Vector3.one * (_intent.LeftReach ? 0.14f : 0.18f);
            RightHand.localScale = Vector3.one * (_intent.RightReach ? 0.14f : 0.18f);
            StretchArm(LeftArm, leftShoulder, LeftHand.localPosition);
            StretchArm(RightArm, rightShoulder, RightHand.localPosition);
        }

        Vector3 HandTarget(Vector3 shoulder, bool limp, bool reach, bool hasPoint, Vector3 pointWorld, Vector3 aimWorld, float dangle, float side)
        {
            if (limp) return shoulder + new Vector3(0.05f * side, -0.75f, dangle);
            if (reach && hasPoint)
                return shoulder + Vector3.ClampMagnitude(transform.InverseTransformPoint(pointWorld) - shoulder, ReachLength);
            if (reach && aimWorld != Vector3.zero) return shoulder + transform.InverseTransformDirection(aimWorld) * ReachLength;
            return shoulder + new Vector3(0.08f * side, -0.6f, 0.15f + dangle * 0.3f);
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
