using System;
using System.Collections;
using System.Collections.Generic;
using TrashPandas.Core.Raccoons;
using TrashPandas.Runtime.Net;
using TrashPandas.Runtime.Raccoon;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    public struct GadgetSnapshot : INetworkSerializable, IEquatable<GadgetSnapshot>
    {
        byte _p0, _p1, _p2, _p3, _p4, _s0, _s1, _s2, _s3, _s4;
        public int Count(int player, Gadget g) => g == Gadget.Pebble
            ? player switch { 0 => _p0, 1 => _p1, 2 => _p2, 3 => _p3, 4 => _p4, _ => 0 }
            : player switch { 0 => _s0, 1 => _s1, 2 => _s2, 3 => _s3, 4 => _s4, _ => 0 };
        public void Set(int player, int pebbles, int smokes)
        {
            byte p = (byte)pebbles, s = (byte)smokes;
            switch (player) { case 0: _p0 = p; _s0 = s; break; case 1: _p1 = p; _s1 = s; break; case 2: _p2 = p; _s2 = s; break; case 3: _p3 = p; _s3 = s; break; case 4: _p4 = p; _s4 = s; break; }
        }
        public void NetworkSerialize<T>(BufferSerializer<T> b) where T : IReaderWriter
        {
            b.SerializeValue(ref _p0); b.SerializeValue(ref _p1); b.SerializeValue(ref _p2); b.SerializeValue(ref _p3); b.SerializeValue(ref _p4);
            b.SerializeValue(ref _s0); b.SerializeValue(ref _s1); b.SerializeValue(ref _s2); b.SerializeValue(ref _s3); b.SerializeValue(ref _s4);
        }
        public bool Equals(GadgetSnapshot o) => _p0 == o._p0 && _p1 == o._p1 && _p2 == o._p2 && _p3 == o._p3 && _p4 == o._p4 && _s0 == o._s0 && _s1 == o._s1 && _s2 == o._s2 && _s3 == o._s3 && _s4 == o._s4;
    }

    /// <summary>
    /// Pebbles (throw: the clack where it lands lures humans over) and smoke bombs (a cloud nobody sees
    /// into or out of). The host keeps everyone's kit; every machine shows the effects.
    /// </summary>
    public sealed class GadgetDirector : NetworkBehaviour
    {
        public float ThrowSpeed = 11f, ThrowUp = 4.5f, SmokeRadius = 3f, SmokeSeconds = 7f;
        public static GadgetDirector Instance { get; private set; }
        /// <summary>True while a pebble's clack is being heard (the nemesis learns to ignore them).</summary>
        public static bool EmittingPebble { get; private set; }

        readonly NetworkVariable<GadgetSnapshot> _net = new NetworkVariable<GadgetSnapshot>();
        GadgetSnapshot _offline;
        public GadgetSnapshot Snapshot => SimulationAuthority.IsOnline ? _net.Value : _offline;
        readonly Dictionary<int, GadgetKit> _kits = new Dictionary<int, GadgetKit>();
        static readonly List<SmokeCloud> s_clouds = new List<SmokeCloud>();

        void Awake() { Instance = this; s_clouds.Clear(); }
        public override void OnDestroy() { if (Instance == this) Instance = null; s_clouds.Clear(); base.OnDestroy(); }

        /// <summary>Is this point inside a smoke cloud right now?</summary>
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

        void Update()
        {
            if (!SimulationAuthority.IsSimulating || (SimulationAuthority.IsOnline && !IsSpawned)) return;
            var snap = new GadgetSnapshot();
            foreach (var r in FindObjectsByType<RaccoonController>(FindObjectsSortMode.None))
                if (r && r.PlayerId >= 0) { var k = KitOf(r.PlayerId); snap.Set(r.PlayerId, k.Count(Gadget.Pebble), k.Count(Gadget.SmokeBomb)); }
            _offline = snap;
            if (IsSpawned && IsServer && !snap.Equals(_net.Value)) _net.Value = snap;
            s_clouds.RemoveAll(c => Time.time > c.Until);
        }

        /// <summary>Owner side: use a gadget (offline directly; online asks the host).</summary>
        public void Use(RaccoonController r, Gadget g, Vector3 aim)
        {
            if (!r || r.Frozen || r.HiddenInside) return;
            var net = r.GetComponent<NetworkedRaccoon>();
            Vector3 origin = r.transform.position + Vector3.up * 0.5f;
            if (SimulationAuthority.IsOnline && net && net.IsSpawned && !IsServer) net.UseGadgetRpc((byte)g, origin, aim);
            else HostUse(r.PlayerId, g, origin, aim);
        }

        /// <summary>Host/offline.</summary>
        public void HostUse(int player, Gadget g, Vector3 origin, Vector3 aim)
        {
            if (!KitOf(player).TryUse(g)) return;
            Debug.Log($"[Gadget] player {player} used {g} at {origin:F1}");
            if (g == Gadget.Pebble)
            {
                Vector3 v = new Vector3(aim.x, 0f, aim.z).normalized * ThrowSpeed + Vector3.up * ThrowUp;
                var (landing, flight) = Ballistic(origin, v);
                if (SimulationAuthority.IsOnline && IsSpawned) PebbleFxRpc(origin, v, flight); else StartCoroutine(PebbleFx(origin, v, flight));
                StartCoroutine(PebbleNoise(landing, flight));
                Ui.DebugChecklist.Mark("pebble");
            }
            else
            {
                if (SimulationAuthority.IsOnline && IsSpawned) SmokeFxRpc(origin); else Smoke(origin);
                Ui.DebugChecklist.Mark("smoke");
            }
        }

        static (Vector3 landing, float flight) Ballistic(Vector3 origin, Vector3 v)
        {
            Vector3 p = origin;
            for (float t = 0f; t < 2.5f; t += 0.04f)
            {
                Vector3 next = origin + v * (t + 0.04f) + 0.5f * Physics.gravity * (t + 0.04f) * (t + 0.04f);
                if (Physics.Linecast(p, next, out var hit, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.GetComponentInParent<RaccoonController>())
                    return (hit.point, t);
                p = next;
            }
            return (p, 2.5f);
        }

        IEnumerator PebbleNoise(Vector3 landing, float flight)
        {
            yield return new WaitForSeconds(flight);
            EmittingPebble = true;
            try { NoiseBus.Emit(NoiseKind.Crash, landing); } finally { EmittingPebble = false; } // "what was that?" — and off they go to look
        }

        [Rpc(SendTo.Everyone)]
        void PebbleFxRpc(Vector3 origin, Vector3 v, float flight) => StartCoroutine(PebbleFx(origin, v, flight));

        static Material s_pebbleMat;
        IEnumerator PebbleFx(Vector3 origin, Vector3 v, float flight)
        {
            var pebble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(pebble.GetComponent<Collider>());
            pebble.transform.localScale = Vector3.one * 0.09f;
            s_pebbleMat ??= new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")) { color = new Color(0.6f, 0.6f, 0.62f) };
            pebble.GetComponent<Renderer>().sharedMaterial = s_pebbleMat;
            Ui.Sfx.Play(Ui.Sound.Throw, origin, 0.6f);
            for (float t = 0f; t < flight; t += Time.deltaTime)
            {
                pebble.transform.position = origin + v * t + 0.5f * Physics.gravity * t * t;
                yield return null;
            }
            Ui.Sfx.Play(Ui.Sound.Land, pebble.transform.position, 1f);
            Destroy(pebble, 2f);
        }

        [Rpc(SendTo.Everyone)]
        void SmokeFxRpc(Vector3 center) => Smoke(center);

        static Material s_smokeMat;
        void Smoke(Vector3 center)
        {
            s_clouds.Add(new SmokeCloud(new Vector3(center.x, 0f, center.z), SmokeRadius, Time.time + SmokeSeconds));
            Ui.Sfx.Play(Ui.Sound.Throw, center, 1f);
            var go = new GameObject("Smoke");
            go.transform.position = new Vector3(center.x, 0.6f, center.z);
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
    }
}
