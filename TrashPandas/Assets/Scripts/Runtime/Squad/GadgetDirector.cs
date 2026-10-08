using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TrashPandas.Core.Raccoons;
using TrashPandas.Runtime.Net;
using TrashPandas.Runtime.Npc;
using TrashPandas.Runtime.Raccoon;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>Everyone's tool counts (5 players × 3 tools, a byte each) and which pickups are on the map.</summary>
    public struct GadgetSnapshot : INetworkSerializable, IEquatable<GadgetSnapshot>
    {
        ulong _a, _b;
        public ushort Pickups;

        public int Count(int player, Gadget g)
        {
            int i = player * 3 + (int)g;
            if (player < 0 || player > 4) return 0;
            return (int)(((i < 8 ? _a : _b) >> ((i % 8) * 8)) & 0xFF);
        }

        public void Set(int player, Gadget g, int n)
        {
            if (player < 0 || player > 4) return;
            int i = player * 3 + (int)g, shift = (i % 8) * 8;
            ulong v = (ulong)Mathf.Clamp(n, 0, 255) << shift, mask = ~(0xFFUL << shift);
            if (i < 8) _a = (_a & mask) | v; else _b = (_b & mask) | v;
        }

        public bool PickupUp(int i) => (Pickups & (1 << i)) != 0;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref _a); s.SerializeValue(ref _b); s.SerializeValue(ref Pickups);
        }
        public bool Equals(GadgetSnapshot o) => _a == o._a && _b == o._b && Pickups == o.Pickups;
    }

    /// <summary>
    /// The raccoons' tools — pebbles (bonk a human, or clack somewhere to lure them), smoke bombs (nobody sees in or
    /// out), banana peels (whoever steps on one goes flying, raccoons included) — and the pickups that refill them.
    /// The host keeps the kits and the peels; every machine shows the effects.
    /// </summary>
    public sealed class GadgetDirector : NetworkBehaviour
    {
        public float SmokeRadius = 3f, SmokeSeconds = 7f, BonkSeconds = 1.6f, PickupRespawn = 40f;
        public static GadgetDirector Instance { get; private set; }
        /// <summary>True while a pebble's clack is being heard (the nemesis learns to ignore them).</summary>
        public static bool EmittingPebble { get; private set; }

        readonly NetworkVariable<GadgetSnapshot> _net = new NetworkVariable<GadgetSnapshot>();
        GadgetSnapshot _offline;
        public GadgetSnapshot Snapshot => SimulationAuthority.IsOnline ? _net.Value : _offline;
        readonly Dictionary<int, GadgetKit> _kits = new Dictionary<int, GadgetKit>();
        static readonly List<SmokeCloud> s_clouds = new List<SmokeCloud>();

        // Host: peels on the ground. Everyone: their visuals.
        readonly Dictionary<int, Vector3> _peels = new Dictionary<int, Vector3>();
        readonly Dictionary<int, GameObject> _peelFx = new Dictionary<int, GameObject>();
        int _nextPeel;
        List<GadgetPickup> _pickups;
        float[] _pickupBackAt;
        NpcPawn[] _pawns = new NpcPawn[0];
        float _nextPawnScan;

        void Awake() { Instance = this; s_clouds.Clear(); }
        public override void OnDestroy() { if (Instance == this) Instance = null; s_clouds.Clear(); base.OnDestroy(); }

        public static bool InSmoke(Vector3 p)
        {
            float now = Time.time;
            foreach (var c in s_clouds) if (c.Hides(p, now)) return true;
            return false;
        }

        GadgetKit KitOf(int player)
        {
            if (!_kits.TryGetValue(player, out var k)) _kits[player] = k = GadgetKit.Starting();
            return k;
        }

        List<GadgetPickup> Pickups => _pickups ??= FindObjectsByType<GadgetPickup>(FindObjectsSortMode.None).OrderBy(p => p.name, StringComparer.Ordinal).Take(16).ToList();

        void Update()
        {
            // Everyone: show the pickups that are up.
            var snap0 = Snapshot;
            for (int i = 0; i < Pickups.Count; i++) if (Pickups[i]) Pickups[i].SetAvailable(SimulationAuthority.IsSimulating ? PickupAvailableHost(i) : snap0.PickupUp(i));

            if (!SimulationAuthority.IsSimulating || (SimulationAuthority.IsOnline && !IsSpawned)) return;
            Net.DevAutomation.GadgetTests();
            Net.DevAutomation.Gallery();
            float now = Time.time;
            if (now >= _nextPawnScan) { _nextPawnScan = now + 1f; _pawns = FindObjectsByType<NpcPawn>(FindObjectsSortMode.None); }

            var snap = new GadgetSnapshot();
            foreach (var r in RaccoonController.Registered)
            {
                if (!r || r.PlayerId < 0) continue;
                var k = KitOf(r.PlayerId);
                for (int g = 0; g < 3; g++) snap.Set(r.PlayerId, (Gadget)g, k.Count((Gadget)g));
                TickPickups(r);
                TickPeels(r);
            }
            foreach (var pawn in _pawns) if (pawn) TickPeels(pawn);
            for (int i = 0; i < Pickups.Count; i++) if (PickupAvailableHost(i)) snap.Pickups |= (ushort)(1 << i);
            _offline = snap;
            if (IsSpawned && IsServer && !snap.Equals(_net.Value)) _net.Value = snap;
            s_clouds.RemoveAll(c => now > c.Until);
        }

        bool PickupAvailableHost(int i)
        {
            if (_pickupBackAt == null || _pickupBackAt.Length != Pickups.Count) _pickupBackAt = new float[Pickups.Count];
            return Time.time >= _pickupBackAt[i];
        }

        void TickPickups(RaccoonController r)
        {
            if (r.Frozen || r.HiddenInside) return;
            for (int i = 0; i < Pickups.Count; i++)
            {
                var p = Pickups[i];
                if (!p || !PickupAvailableHost(i)) continue;
                Vector3 d = p.transform.position - r.transform.position; d.y = 0f;
                if (d.magnitude > 1f) continue;
                var kit = KitOf(r.PlayerId);
                if (kit.Count(p.Kind) >= GadgetKit.MaxPerKind) continue;
                kit.Add(p.Kind, p.Amount);
                _pickupBackAt[i] = Time.time + PickupRespawn;
                Debug.Log($"[Gadget] player {r.PlayerId} picked up {p.Amount} {p.Kind}");
                Ui.DebugChecklist.Mark("pickup");
                PickupFx(p.transform.position);
            }
        }

        void PickupFx(Vector3 at) { if (SimulationAuthority.IsOnline && IsSpawned) PickupFxRpc(at); else Ui.Sfx.Play(Ui.Sound.Grab, at, 0.8f); }
        [Rpc(SendTo.Everyone)] void PickupFxRpc(Vector3 at) => Ui.Sfx.Play(Ui.Sound.Grab, at, 0.8f);

        /// <summary>Owner side: use the selected tool toward where the camera looks.</summary>
        public void Use(RaccoonController r, Gadget g, Vector3 look)
        {
            if (!r || r.Frozen || r.HiddenInside) return;
            var net = r.GetComponent<NetworkedRaccoon>();
            Vector3 origin = Origin(r);
            if (SimulationAuthority.IsOnline && net && net.IsSpawned && !IsServer) net.UseGadgetRpc((byte)g, origin, look);
            else HostUse(r.PlayerId, g, origin, look);
        }

        public static Vector3 Origin(RaccoonController r) => r.transform.position + Vector3.up * 0.6f + r.transform.forward * 0.3f;

        /// <summary>Host/offline.</summary>
        public void HostUse(int player, Gadget g, Vector3 origin, Vector3 look)
        {
            if (!KitOf(player).TryUse(g)) return;
            Vector3 v = GadgetThrow.Velocity(g, look);
            var (landing, flight, hitPawn) = Ballistic(origin, v);
            Debug.Log($"[Gadget] player {player} threw {g} → {landing:F1}{(hitPawn ? " hitting " + hitPawn.name : "")}");
            if (SimulationAuthority.IsOnline && IsSpawned) ThrowFxRpc((byte)g, origin, v, flight); else StartCoroutine(ThrowFx(g, origin, v, flight));
            StartCoroutine(Land(g, landing, flight, hitPawn));
            Ui.DebugChecklist.Mark(g == Gadget.Pebble ? "pebble" : g == Gadget.SmokeBomb ? "smoke" : "banana");
        }

        /// <summary>Where it lands (and whom it hits on the way).</summary>
        public static (Vector3 landing, float flight, NpcPawn hit) Ballistic(Vector3 origin, Vector3 v)
        {
            Vector3 p = origin;
            for (float t = 0f; t < 3f; t += 0.03f)
            {
                float tn = t + 0.03f;
                Vector3 next = origin + v * tn + 0.5f * Physics.gravity * tn * tn;
                if (Physics.Linecast(p, next, out var hit, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.GetComponentInParent<RaccoonController>())
                    return (hit.point, tn, hit.collider.GetComponentInParent<NpcPawn>());
                p = next;
            }
            return (p, 3f, null);
        }

        IEnumerator Land(Gadget g, Vector3 at, float flight, NpcPawn hitPawn)
        {
            yield return new WaitForSeconds(flight);
            switch (g)
            {
                case Gadget.Pebble:
                    if (hitPawn) Daze(hitPawn, BonkSeconds, bonk: true);
                    EmittingPebble = !hitPawn; // a clack somewhere = a lure (a bonk on the head isn't)
                    try { NoiseBus.Emit(NoiseKind.Crash, at); } finally { EmittingPebble = false; }
                    break;
                case Gadget.SmokeBomb:
                    if (SimulationAuthority.IsOnline && IsSpawned) SmokeFxRpc(at); else Smoke(at);
                    break;
                case Gadget.Banana:
                    Vector3 ground = Physics.Raycast(at + Vector3.up * 0.5f, Vector3.down, out var gh, 3f, ~0, QueryTriggerInteraction.Ignore) ? gh.point : at;
                    int id = _nextPeel++;
                    _peels[id] = ground;
                    if (SimulationAuthority.IsOnline && IsSpawned) PeelRpc(id, ground, true); else ShowPeel(id, ground, true);
                    break;
            }
        }

        // --- Banana peels --------------------------------------------------------------------------------------
        static readonly List<int> s_slipped = new List<int>();

        void TickPeels(NpcPawn pawn)
        {
            if (_peels.Count == 0) return;
            bool moving = pawn.Velocity.sqrMagnitude > 0.1f;
            foreach (var kv in _peels) if (BananaPeel.Slips(kv.Value, pawn.transform.position, moving)) { s_slipped.Add(kv.Key); Daze(pawn, BananaPeel.SlipSeconds, bonk: false); break; }
            ClearSlipped();
        }

        void TickPeels(RaccoonController r)
        {
            if (_peels.Count == 0 || r.Frozen || r.HiddenInside || !r.IsGroundedForPeel) return;
            foreach (var kv in _peels)
                if (BananaPeel.Slips(kv.Value, r.transform.position, r.PlanarSpeed > 0.5f))
                {
                    s_slipped.Add(kv.Key);
                    Debug.Log($"[Gadget] raccoon {r.PlayerId} slipped on a banana");
                    Panic.PanicDirector.SlipRaccoon(r);
                    SlipFx(r.transform.position);
                    break;
                }
            ClearSlipped();
        }

        void ClearSlipped()
        {
            foreach (int id in s_slipped)
            {
                _peels.Remove(id);
                if (SimulationAuthority.IsOnline && IsSpawned) PeelRpc(id, Vector3.zero, false); else ShowPeel(id, Vector3.zero, false);
            }
            s_slipped.Clear();
        }

        /// <summary>A human got bonked by a pebble or went flying on a peel: dazed for a bit (the nemesis too).</summary>
        void Daze(NpcPawn pawn, float seconds, bool bonk)
        {
            Debug.Log($"[Gadget] {pawn.name} {(bonk ? "bonked" : "slipped")}");
            Ui.DebugChecklist.Mark(bonk ? "bonk" : "slip");
            var pd = Panic.PanicDirector.Instance;
            if (pd && pd.Phase == Panic.RoundPhase.Panic) pd.StunChaser(pawn, seconds);
            var nd = Panic.NemesisDirector.Instance;
            if (nd && nd.Pawn == pawn) nd.Stun(seconds);
            if (!bonk) pawn.Slip(seconds);
            if (bonk) BonkFx(pawn.Eye); else SlipFx(pawn.transform.position);
        }

        void BonkFx(Vector3 at) { if (SimulationAuthority.IsOnline && IsSpawned) BonkFxRpc(at); else Ui.Sfx.Play(Ui.Sound.Hit, at, 1f); }
        [Rpc(SendTo.Everyone)] void BonkFxRpc(Vector3 at) => Ui.Sfx.Play(Ui.Sound.Hit, at, 1f);
        void SlipFx(Vector3 at) { if (SimulationAuthority.IsOnline && IsSpawned) SlipFxRpc(at); else Ui.Sfx.Play(Ui.Sound.Jump, at, 1f); }
        [Rpc(SendTo.Everyone)] void SlipFxRpc(Vector3 at) => Ui.Sfx.Play(Ui.Sound.Jump, at, 1f);

        [Rpc(SendTo.Everyone)] void PeelRpc(int id, Vector3 at, bool on) => ShowPeel(id, at, on);

        static Material s_banana, s_pebble, s_smokeBall;
        static Material Lit(Color c) => new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = c };

        void ShowPeel(int id, Vector3 at, bool on)
        {
            if (!on)
            {
                if (_peelFx.TryGetValue(id, out var go) && go) Destroy(go);
                _peelFx.Remove(id);
                return;
            }
            var peel = new GameObject("BananaPeel");
            peel.transform.position = at + Vector3.up * 0.03f;
            peel.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);
            s_banana ??= Lit(new Color(1f, 0.85f, 0.15f));
            for (int i = 0; i < 4; i++)
            {
                var leaf = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Destroy(leaf.GetComponent<Collider>());
                leaf.transform.SetParent(peel.transform, false);
                leaf.transform.localRotation = Quaternion.Euler(90f, i * 90f + 20f, 0f);
                leaf.transform.localPosition = leaf.transform.localRotation * new Vector3(0f, 0.1f, 0f);
                leaf.transform.localScale = new Vector3(0.09f, 0.11f, 0.03f);
                leaf.GetComponent<Renderer>().sharedMaterial = s_banana;
            }
            _peelFx[id] = peel;
        }

        [Rpc(SendTo.Everyone)]
        void ThrowFxRpc(byte g, Vector3 origin, Vector3 v, float flight) => StartCoroutine(ThrowFx((Gadget)g, origin, v, flight));

        IEnumerator ThrowFx(Gadget g, Vector3 origin, Vector3 v, float flight)
        {
            var go = GameObject.CreatePrimitive(g == Gadget.Banana ? PrimitiveType.Capsule : PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = g == Gadget.Pebble ? Vector3.one * 0.1f : g == Gadget.Banana ? new Vector3(0.08f, 0.14f, 0.08f) : Vector3.one * 0.2f;
            s_pebble ??= Lit(new Color(0.6f, 0.6f, 0.62f));
            s_banana ??= Lit(new Color(1f, 0.85f, 0.15f));
            s_smokeBall ??= Lit(new Color(0.35f, 0.3f, 0.45f));
            go.GetComponent<Renderer>().sharedMaterial = g == Gadget.Pebble ? s_pebble : g == Gadget.Banana ? s_banana : s_smokeBall;
            Ui.Sfx.Play(Ui.Sound.Throw, origin, 0.6f);
            for (float t = 0f; t < flight; t += Time.deltaTime)
            {
                go.transform.position = origin + v * t + 0.5f * Physics.gravity * t * t;
                go.transform.rotation = Quaternion.Euler(t * 720f, t * 360f, 0f);
                yield return null;
            }
            Ui.Sfx.Play(Ui.Sound.Land, go.transform.position, 1f);
            Destroy(go);
        }

        [Rpc(SendTo.Everyone)]
        void SmokeFxRpc(Vector3 center) => Smoke(center);

        static Material s_smokeMat;
        void Smoke(Vector3 center)
        {
            s_clouds.Add(new SmokeCloud(new Vector3(center.x, 0f, center.z), SmokeRadius, Time.time + SmokeSeconds));
            Ui.Sfx.Play(Ui.Sound.Throw, center, 1f);
            var go = new GameObject("Smoke");
            go.transform.position = new Vector3(center.x, center.y + 0.6f, center.z);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = SmokeSeconds;
            main.loop = false;
            main.startLifetime = 2.5f;
            main.startSpeed = 0.6f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
            main.startColor = new Color(0.85f, 0.85f, 0.88f, 0.55f);
            main.maxParticles = 400;
            var emission = ps.emission;
            emission.rateOverTime = 45f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = SmokeRadius * 0.75f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (!s_smokeMat)
            {
                s_smokeMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
                const int n = 32;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f, a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
                tex.Apply();
                s_smokeMat.mainTexture = tex;
                if (s_smokeMat.HasProperty("_BaseMap")) s_smokeMat.SetTexture("_BaseMap", tex);
                if (s_smokeMat.HasProperty("_Surface")) s_smokeMat.SetFloat("_Surface", 1f);
                s_smokeMat.SetOverrideTag("RenderType", "Transparent");
                s_smokeMat.renderQueue = 3000;
                s_smokeMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                s_smokeMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                s_smokeMat.SetFloat("_ZWrite", 0f);
                s_smokeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            r.sharedMaterial = s_smokeMat;
            ps.Play();
            Destroy(go, SmokeSeconds + 3f);
        }

        /// <summary>Dev: a peel right on the nemesis' patrol route (slip test).</summary>
        public void DevPeelAt(Vector3 at)
        {
            int id = _nextPeel++;
            _peels[id] = at;
            if (SimulationAuthority.IsOnline && IsSpawned) PeelRpc(id, at, true); else ShowPeel(id, at, true);
        }
    }
}
