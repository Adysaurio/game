using UnityEngine;
using UnityEngine.InputSystem;

namespace TrashPandas.Runtime.Input
{
    /// <summary>
    /// The Xbox / any gamepad, alongside keyboard + mouse. LS move · RS camera · A jump · B sneak · LT/L3 run · X use ·
    /// RB grab (hold + release = throw) · RT aim/throw tool · D-pad ←/→ tool · Y dance · D-pad ↑ cheer · View switch.
    /// </summary>
    public static class Pad
    {
        public static Gamepad G => Gamepad.current;
        public const float Dead = 0.2f;

        public static Vector2 Stick(Vector2 v) => v.magnitude < Dead ? Vector2.zero : v.normalized * Mathf.InverseLerp(Dead, 1f, Mathf.Min(1f, v.magnitude));
        public static Vector2 Move => G != null ? Stick(G.leftStick.ReadValue()) : Vector2.zero;
        public static Vector2 Look => G != null ? Stick(G.rightStick.ReadValue()) : Vector2.zero;

        public static bool JumpPressed => G != null && G.buttonSouth.wasPressedThisFrame;
        public static bool JumpHeld => G != null && G.buttonSouth.isPressed;
        public static bool SneakHeld => G != null && G.buttonEast.isPressed;
        public static bool RunHeld => G != null && (G.leftTrigger.ReadValue() > 0.4f || G.leftStickButton.isPressed);
        public static bool UsePressed => G != null && G.buttonWest.wasPressedThisFrame;
        public static bool GrabPressed => G != null && G.rightShoulder.wasPressedThisFrame;
        public static bool GrabReleased => G != null && G.rightShoulder.wasReleasedThisFrame;
        public static bool AimHeld => G != null && G.rightTrigger.ReadValue() > 0.4f;
        public static bool ToolNext => G != null && G.dpad.right.wasPressedThisFrame;
        public static bool ToolPrev => G != null && G.dpad.left.wasPressedThisFrame;
        public static bool DancePressed => G != null && G.buttonNorth.wasPressedThisFrame;
        public static bool CheerPressed => G != null && G.dpad.up.wasPressedThisFrame;
        public static bool SwitchPressed => G != null && G.selectButton.wasPressedThisFrame;
        public static bool HelpPressed => G != null && G.startButton.wasPressedThisFrame;
        public static bool ChecklistPressed => G != null && G.dpad.down.wasPressedThisFrame;
    }
}
