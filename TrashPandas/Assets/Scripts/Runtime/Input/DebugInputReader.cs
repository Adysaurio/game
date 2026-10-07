using TrashPandas.Core.Trenchcoat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrashPandas.Runtime.Input
{
    /// <summary>Keyboard + mouse for the single debug person. Replaced by per-player input in stage 2.</summary>
    public sealed class DebugInputReader
    {
        const float LookSpeed = 90f; // degrees per second with arrow keys
        float _lastJumpPressedAt = float.NegativeInfinity;
        Vector2 _look;

        public bool TogglePressed => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        public bool CyclePressed => Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;

        public Vector2 Move()
        {
            var k = Keyboard.current;
            if (k == null) return Vector2.zero;
            var move = new Vector2(
                (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f),
                (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f));
            return Vector2.ClampMagnitude(move, 1f);
        }

        public bool JumpPressed => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        public bool CrouchHeld => Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;

        public SlotInput ReadSlotInput(float now, float deltaTime)
        {
            if (JumpPressed) _lastJumpPressedAt = now;

            var k = Keyboard.current;
            if (k != null)
            {
                _look.x += ((k.rightArrowKey.isPressed ? 1f : 0f) - (k.leftArrowKey.isPressed ? 1f : 0f)) * LookSpeed * deltaTime;
                _look.y += ((k.upArrowKey.isPressed ? 1f : 0f) - (k.downArrowKey.isPressed ? 1f : 0f)) * LookSpeed * deltaTime;
                _look.x = Mathf.Clamp(_look.x, -80f, 80f);
                _look.y = Mathf.Clamp(_look.y, -40f, 40f);
            }

            var mouse = Mouse.current;
            Vector3 hand = new Vector3(0f, 1.2f, 0.5f);
            bool primary = false, secondary = false;
            if (mouse != null)
            {
                Vector2 p = mouse.position.ReadValue();
                float vx = Mathf.Clamp01(p.x / Mathf.Max(1, Screen.width));
                float vy = Mathf.Clamp01(p.y / Mathf.Max(1, Screen.height));
                hand = new Vector3((vx - 0.5f) * 1.2f, 0.6f + vy * 1.2f, 0.5f);
                primary = mouse.leftButton.isPressed;
                secondary = mouse.rightButton.isPressed;
            }

            return new SlotInput
            {
                Move = Move(),
                Crouch = CrouchHeld,
                JumpPressedAt = _lastJumpPressedAt,
                HandTarget = hand,
                PrimaryGrab = primary,
                SecondaryGrab = secondary,
                Look = _look,
            };
        }
    }
}
