using System.Collections.Generic;
using UnityEngine;

namespace TrashPandas.Runtime.Grabbing
{
    /// <summary>Something the coat's hands can pick up, carry and throw.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Grabbable : MonoBehaviour
    {
        static readonly List<Grabbable> s_All = new List<Grabbable>();
        public static IReadOnlyList<Grabbable> All => s_All;

        [Tooltip("Big things (the cake) need both hands.")]
        public bool RequiresBothHands;

        public Rigidbody Body { get; private set; }
        public Collider[] Colliders { get; private set; }
        public bool IsHeld { get; internal set; }

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            // If a released item ends up overlapping something, ease it apart instead of exploding.
            Body.maxDepenetrationVelocity = 2f;
            Colliders = GetComponentsInChildren<Collider>();
        }

        void OnEnable()
        {
            // With domain reload disabled, statics survive play sessions: drop destroyed leftovers.
            s_All.RemoveAll(g => !g);
            if (!s_All.Contains(this)) s_All.Add(this);
        }

        void OnDisable() => s_All.Remove(this);
    }
}
