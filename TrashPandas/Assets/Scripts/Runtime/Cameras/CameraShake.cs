using UnityEngine;

namespace TrashPandas.Runtime.Cameras
{
    /// <summary>Trauma-style screen shake (decays smoothly) plus a tiny hit-stop. Runs after Cinemachine.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }
        public float MaxOffset = 0.35f, MaxRoll = 4f, Decay = 1.6f;
        float _trauma, _hitStopUntil;
        float _seed;

        void Awake() { Instance = this; _seed = Random.value * 100f; }
        void OnDestroy() { if (Instance == this) Instance = null; Time.timeScale = 1f; }

        /// <summary>Add trauma (0..1). The shake grows with trauma squared, so small kicks stay subtle.</summary>
        public void Kick(float trauma) => _trauma = Mathf.Clamp01(_trauma + trauma);

        /// <summary>Freeze time for a blink on impact (offline/clients only: the host simulates for everyone).</summary>
        public void HitStop(float seconds)
        {
            if (Net.SimulationAuthority.IsOnline && Net.SimulationAuthority.IsSimulating) return;
            _hitStopUntil = Time.unscaledTime + seconds;
            Time.timeScale = 0.05f;
        }

        void LateUpdate()
        {
            if (_hitStopUntil > 0f && Time.unscaledTime >= _hitStopUntil) { _hitStopUntil = 0f; Time.timeScale = 1f; }
            if (_trauma <= 0f) return;
            float s = _trauma * _trauma, t = Time.unscaledTime * 22f;
            transform.position += transform.right * (Mathf.PerlinNoise(_seed, t) - 0.5f) * 2f * MaxOffset * s
                                + transform.up * (Mathf.PerlinNoise(_seed + 1f, t) - 0.5f) * 2f * MaxOffset * s;
            transform.rotation *= Quaternion.Euler(0f, 0f, (Mathf.PerlinNoise(_seed + 2f, t) - 0.5f) * 2f * MaxRoll * s);
            _trauma = Mathf.Max(0f, _trauma - Decay * Time.unscaledDeltaTime);
        }
    }
}
