using TrashPandas.Core.Trenchcoat;
using TrashPandas.Runtime.Cameras;
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
        float _lastJumpPressedAt = float.NegativeInfinity;

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

        public SlotInput ReadSlotInput(PlayerCameraRig rig, float now)
        {
            if (JumpPressed) _lastJumpPressedAt = now;
            return new SlotInput
            {
                Move = CameraRelativeMove(rig),
                Crouch = CrouchHeld,
                JumpPressedAt = _lastJumpPressedAt,
                Aim = rig.AimDirection,
                PrimaryReach = M != null && M.leftButton.isPressed && !rig.CursorFreed,
                SecondaryReach = M != null && M.rightButton.isPressed && !rig.CursorFreed,
            };
        }

        /// <summary>One-line control hint for the HUD.</summary>
        public static string HintFor(BodyPart parts)
        {
            bool legs = (parts & BodyPart.Legs) != 0, arms = (parts & BodyPart.Arms) != 0, head = (parts & BodyPart.Head) != 0;
            var hint = "Mouse: camera   ";
            if (legs) hint += "WASD walk (camera-relative) · Space jump · Ctrl crouch   ";
            if (arms) hint += "Hold L/R click: reach & grab where you look   ";
            if (head) hint += "Head looks where you look";
            return hint;
        }
    }
}
