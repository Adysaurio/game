using TrashPandas.Core.Raccoons;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>A pile of pebbles / a bunch of bananas / a smoke bomb lying around: walk over it to take it (it comes back later).</summary>
    public sealed class GadgetPickup : MonoBehaviour
    {
        public Gadget Kind;
        public int Amount = 2;
        public Transform Visual;
        bool _shown = true;
        Vector3 _rest;

        void Awake() { if (Visual) _rest = Visual.localPosition; }

        public void SetAvailable(bool on)
        {
            if (on == _shown) return;
            _shown = on;
            if (Visual) Visual.gameObject.SetActive(on);
        }

        void Update()
        {
            if (!_shown || !Visual) return;
            Visual.localPosition = _rest + Vector3.up * (0.08f * Mathf.Sin(Time.time * 2.5f));
            Visual.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
        }
    }
}
