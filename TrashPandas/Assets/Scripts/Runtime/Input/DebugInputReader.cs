using System.Collections.Generic;
using TrashPandas.Core.Grabbing;
using TrashPandas.Core.Trenchcoat;
using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Grabbing;
using TrashPandas.Runtime.Trenchcoat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrashPandas.Runtime.Input
{
    /// <summary>
    /// Keyboard + mouse for the single debug person. Movement is relative to the player's camera and
    /// arms/head aim where that camera looks. Replaced by per-player input in stage 2.
    /// </summary>
    public sealed class DebugInputReader
    {
        public float AssistConeDegrees = 12f;
        public float AssistReach = 1.35f;
        public float MaxAimDistance = 6f;

        float _lastJumpPressedAt = float.NegativeInfinity;
        readonly List<GrabCandidate> _candidates = new List<GrabCandidate>();
        readonly RaycastHit[] _rayHits = new RaycastHit[16];

        /// <summary>The grabbable the crosshair is locked onto this frame (aim assist), if any.</summary>
        public Grabbable AssistTarget { get; private set; }
        /// <summary>True when the crosshair points at something within reach (or an assist target).</summary>
        public bool AimInReach { get; private set; }

        static Keyboard K => Keyboard.current;
        static Mouse M => Mouse.current;

        public bool TogglePressed => K != null && K.eKey.wasPressedThisFrame;
        public bool CyclePressed => K != null && K.tabKey.wasPressedThisFrame;
        public bool RecordPressed => K != null && K.rKey.wasPressedThisFrame;
        public bool ClearGhostsPressed => K != null && K.backspaceKey.wasPressedThisFrame;
        public bool JumpPressed => K != null && K.spaceKey.wasPressedThisFrame;
        public bool JumpHeld => K != null && K.spaceKey.isPressed;
        public bool CrouchHeld => K != null && K.leftCtrlKey.isPressed;

        /// <summary>Player index 0-4 if a number key 1-5 was pressed this frame, else -1.</summary>
        public int SelectPressed()
        {
            if (K == null) return -1;
            if (K.digit1Key.wasPressedThisFrame) return 0;
            if (K.digit2Key.wasPressedThisFrame) return 1;
            if (K.digit3Key.wasPressedThisFrame) return 2;
            if (K.digit4Key.wasPressedThisFrame) return 3;
            if (K.digit5Key.wasPressedThisFrame) return 4;
            return -1;
        }

        /// <summary>Raw WASD (x = right, y = forward), magnitude 0..1.</summary>
        public Vector2 Move()
        {
            if (K == null) return Vector2.zero;
            float x = (K.dKey.isPressed ? 1f : 0f) - (K.aKey.isPressed ? 1f : 0f);
            float y = (K.wKey.isPressed ? 1f : 0f) - (K.sKey.isPressed ? 1f : 0f);
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }

        /// <summary>WASD turned into a world XZ direction relative to the camera, magnitude 0..1.</summary>
        public Vector2 CameraRelativeMove(PlayerCameraRig rig)
        {
            if (K == null) return Vector2.zero;
            float x = (K.dKey.isPressed ? 1f : 0f) - (K.aKey.isPressed ? 1f : 0f);
            float y = (K.wKey.isPressed ? 1f : 0f) - (K.sKey.isPressed ? 1f : 0f);
            rig.GroundAxes(out var forward, out var right);
            Vector3 world = Vector3.ClampMagnitude(right * x + forward * y, 1f);
            return new Vector2(world.x, world.z);
        }

        public SlotInput ReadSlotInput(PlayerCameraRig rig, TrenchcoatBody body, float now)
        {
            if (JumpPressed) _lastJumpPressedAt = now;
            var input = new SlotInput
            {
                Move = CameraRelativeMove(rig),
                Crouch = CrouchHeld,
                JumpPressedAt = _lastJumpPressedAt,
                Aim = rig.AimDirection,
                GrabOne = M != null && M.leftButton.isPressed && !rig.CursorFreed,
                GrabBoth = M != null && M.rightButton.isPressed && !rig.CursorFreed,
            };

            ResolveAimPoint(rig, body, out input.AimPoint, out input.HasAimPoint);
            input.PreferLeftHand = input.HasAimPoint &&
                GrabTargeting.LeftHandCloser(input.AimPoint, body.ShoulderWorld(true), body.ShoulderWorld(false));
            return input;
        }

        /// <summary>Crosshair target: the assisted grabbable if one is near the crosshair, else what the crosshair hits.</summary>
        void ResolveAimPoint(PlayerCameraRig rig, TrenchcoatBody body, out Vector3 point, out bool hasPoint)
        {
            var cam = rig.OutputCamera.transform;
            _candidates.Clear();
            var all = Grabbable.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] && !all[i].IsHeld) _candidates.Add(new GrabCandidate { Id = i, Position = all[i].transform.position });

            int? pick = GrabTargeting.Pick(cam.position, cam.forward, _candidates, body.ChestWorld, AssistReach, AssistConeDegrees);
            AssistTarget = pick.HasValue ? all[pick.Value] : null;
            if (AssistTarget)
            {
                point = AssistTarget.transform.position;
                hasPoint = true;
                AimInReach = true;
                return;
            }

            // Nearest hit along the crosshair that isn't the coat itself.
            int count = Physics.RaycastNonAlloc(cam.position, cam.forward, _rayHits, 50f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            point = Vector3.zero;
            hasPoint = false;
            for (int i = 0; i < count; i++)
            {
                var h = _rayHits[i];
                if (h.collider.transform.IsChildOf(body.transform) || h.distance >= best) continue;
                best = h.distance;
                point = h.point;
                hasPoint = Vector3.Distance(h.point, body.ChestWorld) <= MaxAimDistance;
            }
            AimInReach = hasPoint && Vector3.Distance(point, body.ChestWorld) <= AssistReach;
        }

        /// <summary>One-line control hint for the HUD.</summary>
        public static string HintFor(BodyPart parts)
        {
            bool legs = (parts & BodyPart.Legs) != 0, arms = (parts & BodyPart.Arms) != 0, head = (parts & BodyPart.Head) != 0;
            var hint = "Mouse: camera   ";
            if (legs) hint += "WASD walk (camera-relative) · Space jump · Ctrl crouch   ";
            if (arms) hint += "Aim with the crosshair · hold Left click: grab (closest hand) · Right click: both hands (big things) · release to throw   ";
            if (head) hint += "Head looks where you look";
            return hint;
        }
    }
}
