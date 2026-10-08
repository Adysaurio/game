using TrashPandas.Core.Loot;
using TrashPandas.Runtime.Grabbing;
using TrashPandas.Runtime.Net;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.Runtime.Loot
{
    public enum LootState : byte { Active, Stashed, Unused }

    /// <summary>A grabbable thing worth money. Stashed or unused items are hidden and parked under the map.</summary>
    [RequireComponent(typeof(Grabbable))]
    public sealed class LootItem : NetworkBehaviour
    {
        public LootKind Kind;
        public ObjectiveId Objective;

        readonly NetworkVariable<byte> _state = new NetworkVariable<byte>();
        LootState _offlineState;
        Grabbable _grabbable;
        Renderer[] _renderers;

        public bool IsObjective => Kind == LootKind.Objective;
        public int Value => IsObjective ? LootCatalog.Objective(Objective).Value : LootCatalog.ValueOf(Kind);
        public LootState State => SimulationAuthority.IsOnline ? (LootState)_state.Value : _offlineState;
        public Grabbable Grabbable => _grabbable ? _grabbable : _grabbable = GetComponent<Grabbable>();
        public string Label => IsObjective ? LootCatalog.Objective(Objective).Name : Kind.ToString();

        void Awake() => _renderers = GetComponentsInChildren<Renderer>();

        public override void OnNetworkSpawn()
        {
            _state.OnValueChanged += (_, __) => Apply();
            Apply();
        }

        /// <summary>Host/offline only.</summary>
        public void SetState(LootState state)
        {
            if (SimulationAuthority.IsOnline) { if (IsServer) _state.Value = (byte)state; }
            else _offlineState = state;
            Apply();
        }

        /// <summary>Host/offline only: put it at a spot, at rest.</summary>
        public void PlaceAt(Vector3 position)
        {
            var body = Grabbable.Body;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            if (body) { body.position = position; if (!body.isKinematic) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; } }
        }

        void Apply()
        {
            bool active = State == LootState.Active;
            if (_renderers != null) foreach (var r in _renderers) if (r) r.enabled = active;
            var g = Grabbable;
            if (!g) return;
            g.enabled = active; // hidden items drop out of Grabbable.All (no aim assist on them)
            foreach (var c in g.Colliders ?? GetComponentsInChildren<Collider>()) if (c) c.enabled = active && !g.IsHeld;
            if (!active && g.Body)
            {
                g.Body.isKinematic = true;
                transform.position = new Vector3(transform.position.x, -30f, transform.position.z);
            }
            else if (active && g.Body && !g.IsHeld && SimulationAuthority.IsSimulating) g.Body.isKinematic = false;
        }
    }
}
