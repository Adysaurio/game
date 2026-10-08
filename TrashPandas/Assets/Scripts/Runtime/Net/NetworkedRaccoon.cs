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

        void Awake() => Controller = GetComponent<RaccoonController>();

        public override void OnNetworkSpawn()
        {
            Controller.enabled = IsOwner; // non-owners must not run gravity/CharacterController moves
            if (IsOwner) LocalOwned = this;
        }

        /// <summary>Host → owner: you got hit (the owner moves this raccoon, so the owner applies it).</summary>
        [Rpc(SendTo.Owner)]
        public void HitRpc(UnityEngine.Vector3 impulse, float stunSeconds) => Controller.ApplyHit(impulse, stunSeconds);

        /// <summary>Owner → host: pick up / spit out loot with the mouth.</summary>
        [Rpc(SendTo.Server)]
        public void MouthRpc() { if (Loot.LootDirector.Instance) Loot.LootDirector.Instance.ToggleMouth(Controller); }

        /// <summary>Host → owner: caught, no more control.</summary>
        [Rpc(SendTo.Owner)]
        public void FreezeRpc() => Controller.Frozen = true;

        public override void OnNetworkDespawn()
        {
            if (LocalOwned == this) LocalOwned = null;
        }
    }
}
