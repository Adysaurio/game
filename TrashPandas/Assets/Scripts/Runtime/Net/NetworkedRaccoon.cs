using TrashPandas.Runtime.Raccoon;
using Unity.Netcode;

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

        void Awake() => Controller = GetComponent<RaccoonController>();

        public override void OnNetworkSpawn()
        {
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
