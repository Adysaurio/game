using System.Collections.Generic;
using TrashPandas.Core.Session;
using TrashPandas.Core.Trenchcoat;
using TrashPandas.Runtime.Grabbing;
using TrashPandas.Runtime.Trenchcoat;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.Runtime.Net
{
    /// <summary>
    /// The trenchcoat online. The host mixes every player's latest input and simulates the body; clients send
    /// their input and animate limbs from the replicated <see cref="BodyVisualState"/>. Position and rotation
    /// travel through NetworkTransform.
    /// </summary>
    [RequireComponent(typeof(TrenchcoatBody))]
    public sealed class NetworkedTrenchcoat : NetworkBehaviour
    {
        public float VisualSendRate = 20f;
        public float ReturnDistance = 1.6f;
        public NetworkObject RaccoonPrefab;

        public static NetworkedTrenchcoat Instance { get; private set; }

        readonly NetworkVariable<BodyVisualState> _visual = new NetworkVariable<BodyVisualState>();
        readonly NetworkVariable<SlotsSnapshot> _slots = new NetworkVariable<SlotsSnapshot>();
        readonly Dictionary<int, SlotInput> _latestInput = new Dictionary<int, SlotInput>();
        readonly InputSequencer _sequencer = new InputSequencer();
        readonly MixerSettings _mixer = new MixerSettings();
        readonly Dictionary<int, NetworkObject> _raccoons = new Dictionary<int, NetworkObject>();
        float _nextVisualSend;

        public TrenchcoatBody Body { get; private set; }
        public SlotsSnapshot Slots => _slots.Value;

        void Awake() => Body = GetComponent<TrenchcoatBody>();

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer)
            {
                Body.VisualOnly = true;
                var grabber = GetComponent<HandGrabber>();
                if (grabber) grabber.enabled = false; // grabbing is decided by the host
            }
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Client → host, many times a second; late or duplicated packets are dropped.</summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        public void SubmitInputRpc(NetSlotInput input, RpcParams rpcParams = default)
        {
            var roster = SessionHost.Instance ? SessionHost.Instance.Roster : null;
            int? player = roster?.PlayerIdOf(rpcParams.Receive.SenderClientId);
            if (!player.HasValue || !_sequencer.Accept(player.Value, input.Sequence)) return;
            _latestInput[player.Value] = input.Input;
        }

        /// <summary>Hop out of the coat: the seat empties and the host spawns a raccoon you control.</summary>
        [Rpc(SendTo.Server)]
        public void RequestLeaveRpc(RpcParams rpcParams = default)
        {
            ulong client = rpcParams.Receive.SenderClientId;
            var roster = SessionHost.Instance ? SessionHost.Instance.Roster : null;
            int? player = roster?.PlayerIdOf(client);
            if (!player.HasValue || roster.Slots == null || !roster.Slots.Leave(player.Value)) return;
            _latestInput.Remove(player.Value);

            var raccoon = Instantiate(RaccoonPrefab, Core.Trenchcoat.CoatSeating.SpawnBeside(transform.position, transform.rotation), transform.rotation);
            raccoon.SpawnWithOwnership(client, destroyWithScene: true);
            _raccoons[player.Value] = raccoon;
        }

        /// <summary>Hop back in if your raccoon is next to the coat and a seat is free.</summary>
        [Rpc(SendTo.Server)]
        public void RequestReturnRpc(RpcParams rpcParams = default)
        {
            var roster = SessionHost.Instance ? SessionHost.Instance.Roster : null;
            int? player = roster?.PlayerIdOf(rpcParams.Receive.SenderClientId);
            if (!player.HasValue || roster.Slots == null || !_raccoons.TryGetValue(player.Value, out var raccoon) || !raccoon) return;
            if (!Core.Trenchcoat.CoatSeating.TryReturn(roster.Slots, player.Value, raccoon.transform.position, transform.position, ReturnDistance, out _)) return;
            _raccoons.Remove(player.Value);
            raccoon.Despawn(destroy: true);
        }

        void Update()
        {
            if (!IsSpawned) return;
            if (!IsServer)
            {
                Body.SetIntent(_visual.Value.ToIntent());
                return;
            }

            var roster = SessionHost.Instance ? SessionHost.Instance.Roster : null;
            var slots = roster?.Slots;
            if (slots == null) return;

            float now = NetworkManager.ServerTime.TimeAsFloat;
            var intent = TrenchcoatIntentMixer.Mix(SlotInputRouter.Route(slots, _latestInput), slots.ControlledParts, now, _mixer);
            Body.SetIntent(intent);

            if (Time.unscaledTime >= _nextVisualSend)
            {
                _nextVisualSend = Time.unscaledTime + 1f / VisualSendRate;
                var visual = BodyVisualState.From(intent);
                if (!visual.Equals(_visual.Value)) _visual.Value = visual;
            }
            var snapshot = SlotsSnapshot.From(roster);
            if (!snapshot.Equals(_slots.Value)) _slots.Value = snapshot;
        }
    }
}
