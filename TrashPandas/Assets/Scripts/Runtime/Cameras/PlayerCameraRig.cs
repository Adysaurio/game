using TrashPandas.Core.Input;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrashPandas.Runtime.Cameras
{
    /// <summary>
    /// One player's orbit camera: raw mouse movement orbits a Cinemachine OrbitalFollow around the current
    /// target. Owns the cursor lock (Esc frees it, a click captures it again).
    /// </summary>
    public sealed class PlayerCameraRig : MonoBehaviour
    {
        public CinemachineCamera VirtualCamera;
        public CinemachineOrbitalFollow Orbit;
        public CinemachineRotationComposer Composer;
        public Camera OutputCamera;

        [Tooltip("Degrees of orbit per pixel of mouse movement. [ and ] adjust it in play; saved between sessions.")]
        public float Sensitivity = LookSensitivity.Default;
        [Tooltip("Degrees per second when orbiting with the arrow keys.")]
        public float KeyOrbitSpeed = 140f;
        public bool InvertY;
        public Vector2 PitchRange = new Vector2(-20f, 70f);

        const string SensitivityPrefKey = "TrashPandas.LookSensitivity";
        readonly MouseLookFilter _filter = new MouseLookFilter();
        LookSensitivity _sensitivity;
        bool _locked;

        public bool CursorFreed { get; private set; }

        /// <summary>Where the player's camera looks, in world space.</summary>
        public Vector3 AimDirection => OutputCamera ? OutputCamera.transform.forward : Vector3.forward;

        /// <summary>The camera's forward and right flattened onto the ground, for camera-relative movement.</summary>
        public void GroundAxes(out Vector3 forward, out Vector3 right)
        {
            var t = OutputCamera ? OutputCamera.transform : transform;
            forward = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(t.up, Vector3.up).normalized;
            right = Vector3.Cross(Vector3.up, forward);
        }

        public void SetTarget(Transform target, float radius, float lookHeight)
        {
            VirtualCamera.Follow = target;
            VirtualCamera.LookAt = target;
            Orbit.Radius = radius;
            Orbit.TargetOffset = new Vector3(0f, lookHeight, 0f);
            if (Composer) Composer.TargetOffset = new Vector3(0f, lookHeight, 0f);
        }

        void Awake()
        {
            _sensitivity = new LookSensitivity(PlayerPrefs.GetFloat(SensitivityPrefKey, Sensitivity));
            Sensitivity = _sensitivity.Value;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;

            if (keyboard != null && (keyboard.rightBracketKey.wasPressedThisFrame || keyboard.leftBracketKey.wasPressedThisFrame))
            {
                _sensitivity = new LookSensitivity(Sensitivity); // pick up inspector edits
                if (keyboard.rightBracketKey.wasPressedThisFrame) _sensitivity.Increase();
                else _sensitivity.Decrease();
                Sensitivity = _sensitivity.Value;
                PlayerPrefs.SetFloat(SensitivityPrefKey, Sensitivity);
            }

            if (keyboard != null)
            {
                float keyOrbit = (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f);
                float keyPitch = (keyboard.downArrowKey.isPressed ? 1f : 0f) - (keyboard.upArrowKey.isPressed ? 1f : 0f);
                Orbit.HorizontalAxis.Value = Mathf.Repeat(Orbit.HorizontalAxis.Value + keyOrbit * KeyOrbitSpeed * Time.deltaTime + 180f, 360f) - 180f;
                Orbit.VerticalAxis.Value = Mathf.Clamp(Orbit.VerticalAxis.Value + keyPitch * KeyOrbitSpeed * 0.5f * Time.deltaTime, PitchRange.x, PitchRange.y);
            }
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CursorFreed = true;
            else if (CursorFreed && mouse != null && mouse.leftButton.wasPressedThisFrame) CursorFreed = false;

            bool wantLocked = !CursorFreed && Application.isFocused;
            if (wantLocked != _locked)
            {
                _locked = wantLocked;
                Cursor.lockState = wantLocked ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !wantLocked;
                _filter.NotifyLockChanged();
            }

            if (!_locked || mouse == null) return;
            Vector2 delta = _filter.Filter(mouse.delta.ReadValue());
            Orbit.HorizontalAxis.Value = Mathf.Repeat(Orbit.HorizontalAxis.Value + delta.x * Sensitivity + 180f, 360f) - 180f;
            float pitch = Orbit.VerticalAxis.Value + (InvertY ? delta.y : -delta.y) * Sensitivity;
            Orbit.VerticalAxis.Value = Mathf.Clamp(pitch, PitchRange.x, PitchRange.y);
        }

        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _locked = false;
        }
    }
}
