using System.Collections.Generic;
using System.Linq;
using TrashPandas.Runtime.Net;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>
    /// Walk into it and the raccoon puts its paws on it and shoves: the giant gift, crates, a wardrobe hiding a
    /// pipe. The host moves it (sliding, never through walls); clients ask with an RPC.
    /// </summary>
    public sealed class Pushable : MonoBehaviour
    {
        public float Speed = 1.2f;
        static readonly List<Pushable> s_all = new List<Pushable>();
        static List<Pushable> s_sorted;
        Rigidbody _rb;
        Collider[] _cols;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _cols = GetComponentsInChildren<Collider>();
        }
        void OnEnable() { s_all.RemoveAll(p => !p); if (!s_all.Contains(this)) s_all.Add(this); s_sorted = null; }
        void OnDisable() { s_all.Remove(this); s_sorted = null; }

        /// <summary>Stable index on every machine (scene names).</summary>
        public static IReadOnlyList<Pushable> Sorted => s_sorted ??= s_all.Where(p => p).OrderBy(p => p.name, System.StringComparer.Ordinal).ToList();
        public int Index => Sorted is List<Pushable> l ? l.IndexOf(this) : -1;

        /// <summary>Host/offline: slide along the ground in <paramref name="direction"/> for this frame.</summary>
        public void Push(Vector3 direction, float dt)
        {
            var g = GetComponent<Grabbing.Grabbable>();
            if (g && g.IsHeld) return; // being carried: not a push
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            if (flat.sqrMagnitude < 1e-4f) return;
            Vector3 delta = flat.normalized * Speed * dt;
            // Don't shove it through walls or other furniture.
            var bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var c in _cols) if (c && c.enabled) bounds.Encapsulate(c.bounds);
            var hits = Physics.BoxCastAll(bounds.center + Vector3.up * 0.05f, bounds.extents * 0.95f, delta.normalized, Quaternion.identity, delta.magnitude + 0.03f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(transform)) continue;
                if (h.collider.GetComponentInParent<Raccoon.RaccoonController>()) continue;
                if (h.normal.y > 0.6f) continue; // the floor
                return;
            }
            if (_rb && !_rb.isKinematic) { _rb.MovePosition(_rb.position + delta); return; }
            transform.position += delta;
            if (_rb) _rb.position = transform.position;
        }
    }
}
