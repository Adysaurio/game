using TrashPandas.Runtime.Raccoon;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.Runtime.Net
{
    /// <summary>
    /// A loose raccoon online. Its owner moves it locally (owner-authoritative NetworkTransform) so it feels
    /// instant; everyone else just sees it interpolated.
    /// </summary>
    public sealed class NetworkedRaccoon : NetworkBehaviour
    {
        /// <summary>The raccoon this machine controls, if any.</summary>
        public static NetworkedRaccoon LocalOwned { get; private set; }

        public RaccoonController Controller { get; private set; }

        /// <summary>Which player this raccoon is, for every machine (the host sets it before spawning).</summary>
        readonly NetworkVariable<int> _playerId = new NetworkVariable<int>(-1);

        /// <summary>Player id of the raccoon I'm standing on (-1 = none). The owner writes it.</summary>
        readonly NetworkVariable<int> _mountedOn = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        void Awake() => Controller = GetComponent<RaccoonController>();

        void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner)
            {
                int m = Controller.Mount ? Controller.Mount.PlayerId : -1;
                if (_mountedOn.Value != m) _mountedOn.Value = m;
                return;
            }
            RaccoonController mount = null;
            if (_mountedOn.Value >= 0)
                foreach (var r in FindObjectsByType<RaccoonController>(FindObjectsSortMode.None)) if (r.PlayerId == _mountedOn.Value) { mount = r; break; }
            if (Controller.Mount != mount) Controller.SetRemoteMount(mount);
        }

        /// <summary>Owner of the bottom raccoon → everyone: the tower falls; each owner knocks its own riders off.</summary>
        [Rpc(SendTo.NotOwner)]
        void CollapseRpc()
        {
            foreach (var r in FindObjectsByType<RaccoonController>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r == Controller) continue; // only raccoons this machine moves
                var m = r.Mount;
                while (m && m != Controller) m = m.Mount;
                if (!m) continue;
                var dir = Random.insideUnitCircle.normalized * 2.5f;
                r.Dismount(new Vector3(dir.x, 3f, dir.y));
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner) Controller.Collapsed += () => { if (IsSpawned) CollapseRpc(); };
            if (IsServer) _playerId.Value = Controller.PlayerId;
            else Controller.PlayerId = _playerId.Value;
            _playerId.OnValueChanged += (_, v) => Controller.PlayerId = v;
            Controller.enabled = IsOwner; // non-owners must not run gravity/CharacterController moves
            if (IsOwner) LocalOwned = this;
        }

        /// <summary>Host → owner: you got hit (the owner moves this raccoon, so the owner applies it).</summary>
        [Rpc(SendTo.Owner)]
        public void HitRpc(UnityEngine.Vector3 impulse, float stunSeconds) => Controller.ApplyHit(impulse, stunSeconds);

        /// <summary>Owner → host: pick up / spit out loot with the mouth.</summary>
        /// <summary>Owner → host: quick click (grab the highlighted thing, or drop what you carry).</summary>
        [Rpc(SendTo.Server)]
        public void TapRpc(int pickedIndex, UnityEngine.Vector3 ownerPosition, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId) return;
            Squad.CarryDirector.Instance?.Tap(Controller, pickedIndex, ownerPosition);
        }

        /// <summary>Owner → host: hold and release (throw).</summary>
        [Rpc(SendTo.Server)]
        public void ThrowRpc(UnityEngine.Vector3 direction, float strength, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId) return;
            Squad.CarryDirector.Instance?.Throw(Controller, direction, strength);
        }

        /// <summary>Host → owner: caught, no more control.</summary>
        [Rpc(SendTo.Owner)]
        public void FreezeRpc() => Controller.Frozen = true;

        public override void OnNetworkDespawn()
        {
            if (LocalOwned == this) LocalOwned = null;
        }
    }
}
