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
        readonly NetworkVariable<bool> _crawling = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<int> _mountedOn = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        bool _remoteInsideShown;

        void Awake() => Controller = GetComponent<RaccoonController>();

        /// <summary>Host → owner: a human kicked the trash can you were hiding in.</summary>
        [Rpc(SendTo.Owner)]
        public void KickOutRpc() => Controller.ExitCan(kicked: true);

        void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner)
            {
                int m = Controller.Mount ? Controller.Mount.PlayerId : -1;
                if (_mountedOn.Value != m) _mountedOn.Value = m;
                bool inside = Controller.Crawling || Controller.InCan;
                if (_crawling.Value != inside) _crawling.Value = inside;
                return;
            }
            if (_remoteInsideShown != _crawling.Value)
            {
                _remoteInsideShown = _crawling.Value;
                Controller.SetRemoteInside(_crawling.Value);
                foreach (var rend in GetComponentsInChildren<Renderer>()) if (!(rend is ParticleSystemRenderer)) rend.enabled = !_crawling.Value;
            }
            RaccoonController mount = null;
            if (_mountedOn.Value >= 0)
                foreach (var r in FindObjectsByType<RaccoonController>(FindObjectsSortMode.None)) if (r.PlayerId == _mountedOn.Value) { mount = r; break; }
            if (Controller.Mount != mount) Controller.SetRemoteMount(mount);
        }

        /// <summary>Other players' riders: stand exactly on the head as this machine sees it (no double lag).</summary>
        void LateUpdate()
        {
            if (IsSpawned && !IsOwner && Controller.Mount) transform.position = Controller.Mount.HeadTop;
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
            if (IsOwner)
            {
                Controller.Collapsed += () => { if (IsSpawned) CollapseRpc(); };
                Controller.Noise += (kind, at) => MakeNoise(kind, at);
            }
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

        /// <summary>Owner: a noise this raccoon made (the host's humans listen).</summary>
        public void MakeNoise(Core.Raccoons.NoiseKind kind, Vector3 at)
        {
            if (IsServer) Squad.NoiseBus.Emit(kind, at);
            else NoiseRpc(kind, at);
        }

        [Rpc(SendTo.Server)]
        void NoiseRpc(Core.Raccoons.NoiseKind kind, Vector3 at, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId == OwnerClientId) Squad.NoiseBus.Emit(kind, at);
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
        public void FreezeRpc(Vector3 jail) { Controller.Frozen = true; Controller.TeleportTo(jail); }

        /// <summary>Owner → host: my raccoon bumped a light prop (props are host-simulated).</summary>
        [Rpc(SendTo.Server)]
        public void PushPropRpc(int itemIndex, Vector3 impulse, Vector3 at, RpcParams rpc = default)
        {
            var carry = Squad.CarryDirector.Instance;
            if (rpc.Receive.SenderClientId != OwnerClientId || !carry || itemIndex < 0 || itemIndex >= carry.Items.Count) return;
            var g = carry.Items[itemIndex];
            if (!g || g.IsHeld || !g.Body || g.Body.isKinematic || g.Body.mass > 2f) return;
            if (Vector3.Distance(g.transform.position, transform.position) > 2.5f) return; // sanity
            g.Body.AddForceAtPosition(Vector3.ClampMagnitude(impulse, 4f), at, ForceMode.Impulse);
        }

        /// <summary>Owner → host: my raccoon shoves a pushable (the host moves it).</summary>
        [Rpc(SendTo.Server)]
        public void PushObjectRpc(int index, Vector3 direction, RpcParams rpc = default)
        {
            var list = Squad.Pushable.Sorted;
            if (rpc.Receive.SenderClientId != OwnerClientId || index < 0 || index >= list.Count || !list[index]) return;
            if (Vector3.Distance(list[index].transform.position, transform.position) > 3f) return;
            list[index].Push(direction, Time.deltaTime * 3f); // RPCs arrive at tick rate, not frame rate
        }

        /// <summary>Host → owner: a friend opened the cage.</summary>
        [Rpc(SendTo.Owner)]
        public void UnfreezeRpc() => Controller.Frozen = false;

        public override void OnNetworkDespawn()
        {
            if (LocalOwned == this) LocalOwned = null;
        }
    }
}
