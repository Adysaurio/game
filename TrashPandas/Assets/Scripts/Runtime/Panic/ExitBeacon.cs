using UnityEngine;

namespace TrashPandas.Runtime.Panic
{
    /// <summary>A floating, bobbing, spinning arrow over an exit. Hidden until RUN!</summary>
    public sealed class ExitBeacon : MonoBehaviour
    {
        public Transform Arrow;
        public float BobHeight = 0.25f, BobSpeed = 2.5f, SpinSpeed = 90f;
        Vector3 _base;

        void Awake() { if (Arrow) _base = Arrow.localPosition; }

        void Update()
        {
            if (!Arrow) return;
            Arrow.localPosition = _base + Vector3.up * Mathf.Sin(Time.time * BobSpeed) * BobHeight;
            Arrow.Rotate(Vector3.up, SpinSpeed * Time.deltaTime, Space.World);
        }
    }
}
