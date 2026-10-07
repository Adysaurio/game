using TrashPandas.Core.Trenchcoat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrashPandas.Runtime.Input
{
    /// <summary>
    /// Keyboard + mouse for the single debug person. The mouse means something different per role:
    /// legs turn, arms move the hands, head looks. Replaced by per-player input in stage 2.
    /// </summary>
    public sealed class DebugInputReader
    {
        /// <summary>Mouse speed (px/s) that maps to a full-rate turn.</summary>
        public float MouseTurnFullSpeed = 900f;
        /// <summary>Degrees of head rotation per pixel of mouse movement.</summary>
        public float MouseLookSensitivity = 0.15f;

        const float ArrowLookSpeed = 90f; // degrees per second, head look when the mouse drives the hands
        float _lastJumpPressedAt = float.NegativeInfinity;
        Vector2 _look;

        public bool TogglePressed => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        public bool CyclePressed => Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
        public bool JumpPressed => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        public bool CrouchHeld => Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;

        public Vector2 Move()
        {
            var k = Keyboard.current;
            if (k == null) return Vector2.zero;
            var move = new Vector2(
                (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f),
                (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f));
            return Vector2.ClampMagnitude(move, 1f);
        }

        /// <summary>-1..1 turn rate from horizontal mouse speed (frame-rate independent).</summary>
        public float MouseTurn(float deltaTime)
        {
            if (Mouse.current == null || deltaTime <= 0f) return 0f;
            float pixelsPerSecond = Mouse.current.delta.ReadValue().x / deltaTime;
            return Mathf.Clamp(pixelsPerSecond / MouseTurnFullSpeed, -1f, 1f);
        }

        /// <summary>True when the mouse should be captured (hidden) for this role.</summary>
        public static bool WantsLockedCursor(BodyPart parts) => (parts & BodyPart.Arms) == 0;

        public SlotInput ReadSlotInput(BodyPart parts, float now, float deltaTime)
        {
            if (JumpPressed) _lastJumpPressedAt = now;

            var input = new SlotInput
            {
                Move = Move(),
                Crouch = CrouchHeld,
                JumpPressedAt = _lastJumpPressedAt,
                HandTarget = new Vector3(0f, 1.3f, 0.5f),
            };

            bool hasArms = (parts & BodyPart.Arms) != 0;
            var mouse = Mouse.current;

            if ((parts & BodyPart.Legs) != 0)
                input.Turn = MouseTurn(deltaTime);

            if (hasArms && mouse != null)
            {
                Vector2 p = mouse.position.ReadValue();
                float vx = Mathf.Clamp01(p.x / Mathf.Max(1, Screen.width));
                float vy = Mathf.Clamp01(p.y / Mathf.Max(1, Screen.height));
                input.HandTarget = new Vector3((vx - 0.5f) * 1.2f, 0.7f + vy * 1.2f, 0.5f);
                input.PrimaryGrab = mouse.leftButton.isPressed;
                input.SecondaryGrab = mouse.rightButton.isPressed;
            }

            if ((parts & BodyPart.Head) != 0)
            {
                if (!hasArms && mouse != null)
                {
                    _look += mouse.delta.ReadValue() * MouseLookSensitivity;
                }
                else if (Keyboard.current != null)
                {
                    var k = Keyboard.current;
                    _look.x += ((k.rightArrowKey.isPressed ? 1f : 0f) - (k.leftArrowKey.isPressed ? 1f : 0f)) * ArrowLookSpeed * deltaTime;
                    _look.y += ((k.upArrowKey.isPressed ? 1f : 0f) - (k.downArrowKey.isPressed ? 1f : 0f)) * ArrowLookSpeed * deltaTime;
                }
                _look.x = Mathf.Clamp(_look.x, -80f, 80f);
                _look.y = Mathf.Clamp(_look.y, -40f, 40f);
                input.Look = _look;
            }

            return input;
        }
    }
}
