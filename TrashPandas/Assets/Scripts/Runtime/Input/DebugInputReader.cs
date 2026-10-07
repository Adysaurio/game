using TrashPandas.Core.Trenchcoat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrashPandas.Runtime.Input
{
    /// <summary>
    /// Keyboard + mouse for the single debug person. The cursor stays captured and the mouse's movement
    /// means something different per role: legs turn, arms move the hands, head looks.
    /// Replaced by per-player input in stage 2.
    /// </summary>
    public sealed class DebugInputReader
    {
        /// <summary>Degrees the body turns per pixel of horizontal mouse movement.</summary>
        public float TurnSensitivity = 0.25f;
        /// <summary>Meters the hands move per pixel of mouse movement.</summary>
        public float HandSensitivity = 0.004f;
        /// <summary>Degrees of head rotation per pixel of mouse movement.</summary>
        public float LookSensitivity = 0.15f;

        const float ArrowLookSpeed = 90f; // degrees per second, head look when the mouse drives the hands
        static readonly Vector3 HandMin = new Vector3(-0.8f, 0.4f, 0.2f);
        static readonly Vector3 HandMax = new Vector3(0.8f, 2.2f, 0.9f);

        float _lastJumpPressedAt = float.NegativeInfinity;
        Vector2 _look;
        Vector3 _hand = new Vector3(0f, 1.3f, 0.5f);

        public bool TogglePressed => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        public bool CyclePressed => Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
        public bool JumpPressed => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        public bool EscapePressed => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        public bool ClickPressed => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public bool CrouchHeld => Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;

        /// <summary>Player index 0-4 if a number key 1-5 was pressed this frame, else -1.</summary>
        public int SelectPressed()
        {
            var k = Keyboard.current;
            if (k == null) return -1;
            if (k.digit1Key.wasPressedThisFrame) return 0;
            if (k.digit2Key.wasPressedThisFrame) return 1;
            if (k.digit3Key.wasPressedThisFrame) return 2;
            if (k.digit4Key.wasPressedThisFrame) return 3;
            if (k.digit5Key.wasPressedThisFrame) return 4;
            return -1;
        }

        public Vector2 Move()
        {
            var k = Keyboard.current;
            if (k == null) return Vector2.zero;
            var move = new Vector2(
                (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f),
                (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f));
            return Vector2.ClampMagnitude(move, 1f);
        }

        public Vector2 MouseDelta => Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;

        /// <summary>Degrees to turn this frame from horizontal mouse movement.</summary>
        public float MouseYaw() => MouseDelta.x * TurnSensitivity;

        public SlotInput ReadSlotInput(BodyPart parts, float now, float deltaTime)
        {
            if (JumpPressed) _lastJumpPressedAt = now;

            bool hasArms = (parts & BodyPart.Arms) != 0;
            Vector2 delta = MouseDelta;
            var input = new SlotInput
            {
                Move = Move(),
                Crouch = CrouchHeld,
                JumpPressedAt = _lastJumpPressedAt,
            };

            if ((parts & BodyPart.Legs) != 0)
                input.YawDelta = delta.x * TurnSensitivity;

            if (hasArms)
            {
                // The mouse pushes a virtual hand target around; the scroll wheel reaches forward/back.
                float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
                _hand += new Vector3(delta.x, delta.y, Mathf.Sign(scroll) * (scroll != 0f ? 25f : 0f)) * HandSensitivity;
                _hand = Vector3.Max(HandMin, Vector3.Min(HandMax, _hand));
                var mouse = Mouse.current;
                input.PrimaryGrab = mouse != null && mouse.leftButton.isPressed;
                input.SecondaryGrab = mouse != null && mouse.rightButton.isPressed;
            }
            input.HandTarget = _hand;

            if ((parts & BodyPart.Head) != 0)
            {
                if (!hasArms)
                {
                    _look += delta * LookSensitivity;
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

        /// <summary>One-line control hint for the HUD.</summary>
        public static string HintFor(BodyPart parts)
        {
            bool legs = (parts & BodyPart.Legs) != 0, arms = (parts & BodyPart.Arms) != 0, head = (parts & BodyPart.Head) != 0;
            var hint = "";
            if (legs) hint += "W/S walk · A/D sidestep · Mouse turn · Space jump · Ctrl crouch   ";
            if (arms) hint += "Mouse move hands · Wheel reach · L/R click grab   ";
            if (head) hint += arms ? "Arrows look" : "Mouse look";
            return hint;
        }
    }
}
