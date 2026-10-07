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
            Colliders = GetComponentsInChildren<Collider>();
        }

        void OnEnable() => s_All.Add(this);
        void OnDisable() => s_All.Remove(this);
    }
}
