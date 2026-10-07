using TrashPandas.Runtime.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace TrashPandas.Runtime.Npc
{
    public enum NpcKind : byte { Guest, Waiter, Cat }

    /// <summary>
    /// A wedding NPC's body: walks with a NavMeshAgent where the simulation runs, turns its head toward what
    /// caught its attention, and replicates its mood (for the "?" / "!" icons).
    /// </summary>
    public sealed class NpcPawn : NetworkBehaviour
    {
        public NpcKind Kind;
        /// <summary>Social-event speaker id ("MotherInLaw", "Waiter", "Priest", "Bride"), empty for regular guests.</summary>
        public string SpeakerId = "";
        public Transform Head;
        public bool Seated;
        public float EyeHeight = 1.6f;
        /// <summary>Where a held weapon sits.</summary>
        public Transform Hand;

        readonly NetworkVariable<byte> _mood = new NetworkVariable<byte>();
        byte _offlineMood;
        NavMeshAgent _agent;
        Vector3? _lookAt;

        /// <summary>GuestState or CatState as a byte, for the HUD.</summary>
        public byte Mood => SimulationAuthority.IsOnline ? _mood.Value : _offlineMood;
        public Vector3 Eye => Head ? Head.position : transform.position + Vector3.up * EyeHeight;
        public Vector3 Forward => Head ? Head.forward : transform.forward;

        void Awake() => _agent = GetComponent<NavMeshAgent>();

        public override void OnNetworkSpawn()
        {
            if (!IsServer && _agent) _agent.enabled = false; // NetworkTransform moves us on clients
        }

        /// <summary>Called by the director once the NavMesh exists.</summary>
        public void EnableNavigation()
        {
            if (!_agent || Seated || !SimulationAuthority.IsSimulating) return;
            if (NavMesh.SamplePosition(transform.position, out var hit, 2f, NavMesh.AllAreas))
            {
                float moved = Vector2.Distance(new Vector2(hit.position.x, hit.position.z), new Vector2(transform.position.x, transform.position.z));
                if (Debug.isDebugBuild && moved > 0.25f) Debug.Log($"[NAV] {name} displaced {moved:F2} m onto the NavMesh");
                _agent.Warp(hit.position);
                _agent.enabled = true;
            }
        }

        /// <summary>Panic: seated guests jump to their feet and everyone runs faster.</summary>
        public void Panic(float speed)
        {
            if (Seated)
            {
                Seated = false;
                transform.position += Vector3.up * 0.2f;
                foreach (var col in GetComponents<CapsuleCollider>()) { col.height = 1.75f; col.center = new Vector3(0f, 0.875f, 0f); }
            }
            EnableNavigation();
            if (_agent) { _agent.speed = speed; _agent.acceleration = 20f; }
        }

        public void SetSpeed(float speed)
        {
            if (_agent) _agent.speed = speed;
        }

        public void Stop()
        {
            if (_agent && _agent.enabled && _agent.isOnNavMesh) _agent.ResetPath();
        }

        public void GoTo(Vector3 destination)
        {
            if (_agent && _agent.enabled && _agent.isOnNavMesh) _agent.SetDestination(destination);
        }

        public void SetMood(byte mood)
        {
            _offlineMood = mood;
            if (IsSpawned && IsServer && _mood.Value != mood) _mood.Value = mood;
        }

        public void LookAt(Vector3? point) => _lookAt = point;

        void LateUpdate()
        {
            if (!Head) return;
            Quaternion target = transform.rotation;
            if (_lookAt.HasValue)
            {
                Vector3 to = _lookAt.Value - Head.position;
                Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
                if (flat.sqrMagnitude > 1e-4f)
                {
                    float yaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, flat, Vector3.up), -80f, 80f);
                    target = transform.rotation * Quaternion.Euler(0f, yaw, 0f);
                }
            }
            Head.rotation = Quaternion.Slerp(Head.rotation, target, Time.deltaTime * 6f);
        }
    }
}
