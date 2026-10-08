using TrashPandas.Runtime.Grabbing;
using UnityEngine;

namespace TrashPandas.Runtime.Raccoon
{
    /// <summary>
    /// Appeal and juice, on every machine (driven by how the raccoon actually moves, so remote copies look
    /// alive too): squash on landing, stretch on take-off, a bouncy walk, a wagging ringed tail, dust, eyes
    /// that look at whatever is interesting and blink, and a bandana in the player's color.
    /// </summary>
    public sealed class RaccoonLook : MonoBehaviour
    {
        public Transform Visual;     // pivot at the feet: squash & stretch scale from here
        public Transform Tail;
        public Transform[] Pupils = new Transform[0];
        public Transform[] Lids = new Transform[0];
        public Renderer Bandana;

        public static readonly Color[] PlayerColors =
        {
            new Color(0.9f, 0.25f, 0.25f), new Color(0.25f, 0.5f, 0.95f), new Color(0.98f, 0.8f, 0.2f),
            new Color(0.3f, 0.8f, 0.35f), new Color(0.7f, 0.35f, 0.9f),
        };

        static Material s_dustMaterial;
        RaccoonController _raccoon;
        Vector3 _lastPos;
        float _vy, _lastVy, _squash, _stretch, _phase, _nextBlink, _blinkT = -1f, _dustCooldown;
        int _colorFor = -999;
        ParticleSystem _dust;
        MaterialPropertyBlock _block;
        Vector3[] _pupilRest;

        void Awake()
        {
            _raccoon = GetComponent<RaccoonController>();
            _block = new MaterialPropertyBlock();
            _lastPos = transform.position;
            _nextBlink = Time.time + Random.Range(1.5f, 4f);
            _pupilRest = new Vector3[Pupils.Length];
            for (int i = 0; i < Pupils.Length; i++) _pupilRest[i] = Pupils[i] ? Pupils[i].localPosition : Vector3.zero;
            _dust = MakeDust();
        }

        ParticleSystem MakeDust()
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.startLifetime = 0.45f;
            main.startSpeed = 1.2f;
            main.startSize = 0.16f;
            main.startColor = new Color(0.85f, 0.8f, 0.7f, 0.8f);
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.18f;
            shape.rotation = new Vector3(90f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.6f));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            color.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            s_dustMaterial ??= new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
            r.sharedMaterial = s_dustMaterial;
            return ps;
        }

        void Puff(int count)
        {
            if (!_dust || _dustCooldown > 0f) return;
            _dustCooldown = 0.12f;
            _dust.transform.position = transform.position + Vector3.up * 0.03f;
            _dust.Emit(count);
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 pos = transform.position;
            Vector3 v = (pos - _lastPos) / dt;
            _lastPos = pos;
            _lastVy = _vy;
            _vy = Mathf.Lerp(_vy, v.y, 0.5f);
            float planar = new Vector2(v.x, v.z).magnitude;
            _dustCooldown -= dt;

            // Take-off: stretch. Landing: squash, harder the faster you fell, plus a puff of dust.
            if (_vy > 2f && _lastVy <= 2f) { _stretch = 0.35f; Puff(6); Ui.Sfx.Play(Ui.Sound.Jump, pos, 0.6f); }
            if (_lastVy < -2.5f && _vy > -0.6f) { _squash = Mathf.Clamp01(-_lastVy / 9f) * 0.6f + 0.15f; Puff(10); Ui.Sfx.Play(Ui.Sound.Land, pos, Mathf.Clamp01(-_lastVy / 8f)); }
            if (planar > 4.2f && Random.value < dt * 8f) Puff(2); // running kicks up dust
            _squash = Mathf.MoveTowards(_squash, 0f, dt * 2.2f);
            _stretch = Mathf.MoveTowards(_stretch, 0f, dt * 1.8f);

            // A bouncy little walk.
            bool riding = _raccoon && !ReferenceEquals(_raccoon.Mount, null);
            _phase += planar * dt * 4.2f;
            float bob = !riding && planar > 0.3f ? Mathf.Abs(Mathf.Sin(_phase)) * 0.06f : 0f;
            float sy = 1f - _squash + _stretch + bob, sxz = 1f + _squash * 0.6f - _stretch * 0.35f;
            if (Visual)
            {
                Visual.localScale = new Vector3(sxz, sy, sxz);
                Visual.localRotation = Quaternion.Euler(Mathf.Clamp(planar * 2.2f, 0f, 12f), 0f, Mathf.Sin(_phase) * (planar > 0.3f ? 4f : 0f));
            }
            if (Tail) Tail.localRotation = Quaternion.Euler(-25f + Mathf.Sin(Time.time * (planar > 0.3f ? 14f : 3f)) * 8f, Mathf.Sin(Time.time * (planar > 0.3f ? 9f : 2.2f)) * (planar > 0.3f ? 28f : 12f), 0f);

            UpdateEyes();
            UpdateBandana();
        }

        /// <summary>Pupils look at the nearest interesting thing (a grabbable, a raccoon friend); they blink now and then.</summary>
        void UpdateEyes()
        {
            Vector3 head = transform.position + Vector3.up * 0.45f;
            Vector3? look = null;
            float best = 3.5f;
            var all = Grabbable.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!all[i]) continue;
                float d = Vector3.Distance(all[i].transform.position, head);
                if (d < best && Vector3.Dot(all[i].transform.position - head, transform.forward) > 0f) { best = d; look = all[i].transform.position; }
            }
            for (int i = 0; i < Pupils.Length; i++)
            {
                if (!Pupils[i]) continue;
                Vector3 offset = Vector3.zero;
                if (look.HasValue)
                {
                    Vector3 local = transform.InverseTransformDirection((look.Value - head).normalized);
                    offset = new Vector3(Mathf.Clamp(local.x, -1f, 1f) * 0.018f, Mathf.Clamp(local.y, -1f, 1f) * 0.014f, 0f);
                }
                Pupils[i].localPosition = Vector3.Lerp(Pupils[i].localPosition, _pupilRest[i] + offset, Time.deltaTime * 12f);
            }
            if (Time.time >= _nextBlink) { _blinkT = 0f; _nextBlink = Time.time + Random.Range(2f, 5f); }
            float lid = 0f;
            if (_blinkT >= 0f) { _blinkT += Time.deltaTime; lid = Mathf.Sin(Mathf.Clamp01(_blinkT / 0.14f) * Mathf.PI); if (_blinkT > 0.14f) _blinkT = -1f; }
            if (_raccoon && (_raccoon.IsStunned || _raccoon.Frozen)) lid = 0.7f; // dizzy, half-shut
            foreach (var l in Lids) if (l) l.localScale = new Vector3(l.localScale.x, Mathf.Max(0.001f, lid) * 0.11f, l.localScale.z);
        }

        void UpdateBandana()
        {
            if (!Bandana || !_raccoon || _raccoon.PlayerId == _colorFor) return;
            _colorFor = _raccoon.PlayerId;
            Bandana.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", PlayerColors[Mathf.Abs(_colorFor) % PlayerColors.Length]);
            Bandana.SetPropertyBlock(_block);
        }
    }
}
