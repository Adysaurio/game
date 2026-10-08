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
        [Tooltip("A player whose input stops arriving for this long is treated as idle (crash, Wi-Fi drop).")]
        public float InputTimeout = 0.5f;
        public float ReturnDistance = 1.6f;
        public NetworkObject RaccoonPrefab;

        public static NetworkedTrenchcoat Instance { get; private set; }

        readonly NetworkVariable<BodyVisualState> _visual = new NetworkVariable<BodyVisualState>();
        readonly NetworkVariable<SlotsSnapshot> _slots = new NetworkVariable<SlotsSnapshot>();
        readonly NetworkVariable<bool> _burst = new NetworkVariable<bool>();
        readonly Dictionary<int, SlotInput> _latestInput = new Dictionary<int, SlotInput>();
        readonly InputSequencer _sequencer = new InputSequencer();
        readonly JumpStampRebaser _jumpStamps = new JumpStampRebaser();
        readonly Dictionary<int, SlotInput> _freshInput = new Dictionary<int, SlotInput>();
        readonly MixerSettings _mixer = new MixerSettings();
        readonly Dictionary<int, NetworkObject> _raccoons = new Dictionary<int, NetworkObject>();
        float _nextVisualSend;

        public TrenchcoatBody Body { get; private set; }
        public SlotsSnapshot Slots => _slots.Value;

        void Awake() => Body = GetComponent<TrenchcoatBody>();

        public override void OnNetworkSpawn()
        {
            Instance = this;
            _burst.OnValueChanged += (_, burst) => { if (burst) Body.Explode(); };
            if (_burst.Value) Body.Explode();
            if (Squad.GameMode.Raccoons)
            {
                // Concept v2: everyone starts as a raccoon; the coat waits (parked) for the events.
                Squad.GameMode.ParkCoat(Body);
                if (IsServer)
                {
                    // Wait until every client has loaded the scene, or their raccoons never reach them.
                    _spawnSquadAt = Time.realtimeSinceStartup + 60f; // fallback only; the load event is the real trigger
                    NetworkManager.SceneManager.OnLoadEventCompleted += OnAllLoaded;
                }
            }
            if (!IsServer)
            {
                Body.VisualOnly = true;
                var grabber = GetComponent<HandGrabber>();
                if (grabber) grabber.enabled = false; // grabbing is decided by the host
            }
        }

        float _spawnSquadAt = float.MaxValue;
        bool _squadSpawned;

        void OnAllLoaded(string scene, UnityEngine.SceneManagement.LoadSceneMode mode, System.Collections.Generic.List<ulong> done, System.Collections.Generic.List<ulong> timedOut) => SpawnSquad();

        void LateUpdate()
        {
            if (IsServer && !_squadSpawned && Time.realtimeSinceStartup >= _spawnSquadAt) SpawnSquad(); // fallback
        }

        /// <summary>Host, concept v2: a raccoon per player, popping out at the manhole by the den.</summary>
        void SpawnSquad()
        {
            if (_squadSpawned || !IsServer) return;
            _squadSpawned = true;
            var roster = SessionHost.Instance ? SessionHost.Instance.Roster : null;
            if (roster == null) return;
            int n = 0;
            for (int p = 0; p < 8; p++)
            {
                var client = roster.ClientOf(p);
                if (!client.HasValue) continue;
                roster.Slots?.Leave(p);
                SpawnRaccoon(p, client.Value, Squad.GameMode.SpawnPoint(n++), Quaternion.Euler(0f, 180f, 0f));
            }
            if (Squad.RoundIntro.Instance) Squad.RoundIntro.Instance.Begin(n);
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager && NetworkManager.SceneManager != null) NetworkManager.SceneManager.OnLoadEventCompleted -= OnAllLoaded;
            if (Instance == this) Instance = null;
        }

        /// <summary>Client → host, many times a second; late or duplicated packets are dropped.</summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        public void SubmitInputRpc(NetSlotInput input, RpcParams rpcParams = default)
        {
            var roster = SessionHost.Instance ? SessionHost.Instance.Roster : null;
            int? player = roster?.PlayerIdOf(rpcParams.Receive.SenderClientId);
            if (!player.HasValue || roster.Slots == null || !roster.Slots.SlotOf(player.Value).HasValue) return; // not seated
            float now = NetworkManager.ServerTime.TimeAsFloat;
            if (!_sequencer.Accept(player.Value, input.Sequence, now)) return;
            var slotInput = input.Input;
            slotInput.JumpPressedAt = _jumpStamps.Rebase(player.Value, slotInput.JumpPressedAt, now);
            _latestInput[player.Value] = slotInput;
        }

        /// <summary>Hop out of the coat: the seat empties and the host spawns a raccoon you control.</summary>
        [Rpc(SendTo.Server)]
        public void RequestLeaveRpc(RpcParams rpcParams = default)
        {
            ulong client = rpcParams.Receive.SenderClientId;
            var roster = SessionHost.Instance ? SessionHost.Instance.Roster : null;
            int? player = roster?.PlayerIdOf(client);
            if (_burst.Value || !player.HasValue || roster.Slots == null || !roster.Slots.Leave(player.Value)) return;
            _latestInput.Remove(player.Value);
            SpawnRaccoon(player.Value, client, Core.Trenchcoat.CoatSeating.SpawnBeside(transform.position, transform.rotation), transform.rotation);
        }

        NetworkObject SpawnRaccoon(int player, ulong owner, Vector3 position, Quaternion rotation)
        {
            var raccoon = Instantiate(RaccoonPrefab, position, rotation);
            raccoon.GetComponent<Raccoon.RaccoonController>().PlayerId = player;
            raccoon.SpawnWithOwnership(owner, destroyWithScene: true);
            _raccoons[player] = raccoon;
            return raccoon;
        }

        /// <summary>Host, RUN!: every seated player pops out as a raccoon, flung outward; the coat disappears.</summary>
        public void BurstAll()
        {
            if (!IsServer || _burst.Value) return;
            var roster = SessionHost.Instance ? SessionHost.Instance.Roster : null;
            var slots = roster?.Slots;
            if (slots != null)
            {
                for (int seat = 0; seat < slots.SlotCount; seat++)
                {
                    var player = slots.OccupantOf(seat);
                    if (!player.HasValue) continue;
                    var client = roster.ClientOf(player.Value);
                    slots.Leave(player.Value);
                    _latestInput.Remove(player.Value);
                    if (!client.HasValue) continue;
                    float angle = seat * Mathf.PI * 2f / slots.SlotCount;
                    var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    var raccoon = SpawnRaccoon(player.Value, client.Value, transform.position + dir * 0.8f + Vector3.up * 0.5f, Quaternion.LookRotation(dir));
                    raccoon.GetComponent<NetworkedRaccoon>().HitRpc(dir * 5f, 0.6f);
                }
            }
            _burst.Value = true;
        }

        /// <summary>Hop back in if your raccoon is next to the coat and a seat is free.</summary>
        [Rpc(SendTo.Server)]
        public void RequestReturnRpc(RpcParams rpcParams = default)
        {
            var roster = SessionHost.Instance ? SessionHost.Instance.Roster : null;
            int? player = roster?.PlayerIdOf(rpcParams.Receive.SenderClientId);
            if (_burst.Value || !player.HasValue || roster.Slots == null || !_raccoons.TryGetValue(player.Value, out var raccoon) || !raccoon) return;
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
            _freshInput.Clear();
            foreach (var pair in _latestInput)
                if (_sequencer.IsFresh(pair.Key, now, InputTimeout)) _freshInput[pair.Key] = pair.Value;
            var intent = TrenchcoatIntentMixer.Mix(SlotInputRouter.Route(slots, _freshInput), slots.ControlledParts, now, _mixer);
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
