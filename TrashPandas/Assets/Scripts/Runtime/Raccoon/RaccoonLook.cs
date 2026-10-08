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
        public Transform Head;
        /// <summary>Front-left, front-right, back-left, back-right (children of the root, on the ground).</summary>
        public Transform[] Paws = new Transform[0];
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
        Vector3[] _pupilRest, _pawRest;
        Vector3 _headRest;
        float _runK, _sneakK;

        void Awake()
        {
            _raccoon = GetComponent<RaccoonController>();
            _block = new MaterialPropertyBlock();
            _lastPos = transform.position;
            _nextBlink = Time.time + Random.Range(1.5f, 4f);
            _pupilRest = new Vector3[Pupils.Length];
            for (int i = 0; i < Pupils.Length; i++) _pupilRest[i] = Pupils[i] ? Pupils[i].localPosition : Vector3.zero;
            _dust = MakeDust();
            _pawRest = new Vector3[Paws.Length];
            for (int i = 0; i < Paws.Length; i++) _pawRest[i] = Paws[i] ? Paws[i].localPosition : Vector3.zero;
            if (Head) _headRest = Head.localPosition;
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
            if (!s_dustMaterial)
            {
                s_dustMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default"));
                // A soft round puff (without a texture the particles render as squares).
                const int n = 32;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                        float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                tex.Apply();
                s_dustMaterial.mainTexture = tex;
                if (s_dustMaterial.HasProperty("_BaseMap")) s_dustMaterial.SetTexture("_BaseMap", tex);
                // Transparent blending for the URP particle shader.
                if (s_dustMaterial.HasProperty("_Surface")) s_dustMaterial.SetFloat("_Surface", 1f);
                s_dustMaterial.SetOverrideTag("RenderType", "Transparent");
                s_dustMaterial.renderQueue = 3000;
                s_dustMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                s_dustMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                s_dustMaterial.SetFloat("_ZWrite", 0f);
                s_dustMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
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

            // Poses: on all fours when running, low and tiptoeing when sneaking, upright and bouncy otherwise.
            // The owner knows the input; remote copies read it from the speed.
            bool riding = _raccoon && !ReferenceEquals(_raccoon.Mount, null);
            bool local = _raccoon && _raccoon.enabled;
            bool running = !riding && (local ? _raccoon.IsRunning : planar > 3.7f);
            bool sneaking = !riding && !running && (local ? _raccoon.IsSneaking : planar > 0.2f && planar < 2.0f);
            _runK = Mathf.MoveTowards(_runK, running ? 1f : 0f, dt * 6f);
            _sneakK = Mathf.MoveTowards(_sneakK, sneaking ? 1f : 0f, dt * 6f);
            bool moving = !riding && planar > 0.3f;

            _phase += planar * dt * Mathf.Lerp(Mathf.Lerp(4.2f, 2.6f, _sneakK), 3.4f, _runK);
            float walkBob = moving ? Mathf.Abs(Mathf.Sin(_phase)) * 0.06f : 0f;
            float gallop = moving ? Mathf.Max(0f, Mathf.Sin(_phase * 2f)) * 0.09f : 0f; // bounding leaps
            float bob = Mathf.Lerp(Mathf.Lerp(walkBob, walkBob * 0.4f, _sneakK), gallop, _runK);
            float sy = (1f - _squash + _stretch + bob) * Mathf.Lerp(1f, 0.72f, _sneakK);
            float sxz = (1f + _squash * 0.6f - _stretch * 0.35f) * Mathf.Lerp(1f, 1.08f, _sneakK);
            float pitch = Mathf.Lerp(Mathf.Lerp(Mathf.Clamp(planar * 2.2f, 0f, 12f), 22f, _sneakK), 68f, _runK);
            float sway = moving ? Mathf.Sin(_phase) * Mathf.Lerp(Mathf.Lerp(4f, 9f, _sneakK), 2f, _runK) : 0f;
            if (Visual)
            {
                Visual.localScale = new Vector3(sxz, sy, sxz);
                // Lying forward on all fours: lift and pull back the pivot so the belly clears the ground.
                Visual.localPosition = new Vector3(0f, 0.13f * _runK + 0.02f * _sneakK, -0.16f * _runK);
                Visual.localRotation = Quaternion.Euler(pitch + (moving ? Mathf.Sin(_phase * 2f) * 6f * _runK : 0f), 0f, sway);
            }
            if (Head)
            {
                // Keep looking ahead: counter the body's pitch; sneaking pokes the head out low.
                Head.localPosition = _headRest + new Vector3(0f, -0.07f * _sneakK, 0.06f * _sneakK);
                Head.localRotation = Quaternion.Euler(-pitch * 0.85f + 8f * _sneakK, Mathf.Sin(Time.time * 1.7f) * 14f * _sneakK, 0f); // sneaky glances
            }
            float tailFast = moving ? 14f : 3f;
            if (Tail) Tail.localRotation = Quaternion.Euler(Mathf.Lerp(Mathf.Lerp(-25f, 10f, _sneakK), -60f, _runK) + Mathf.Sin(Time.time * tailFast) * 8f,
                                                            Mathf.Sin(Time.time * (moving ? 9f : 2.2f)) * (moving ? Mathf.Lerp(28f, 10f, _runK) : 12f), 0f);
            UpdatePaws(moving, planar);

            UpdateEyes();
            UpdateBandana();
        }

        /// <summary>
        /// Four little paws: a trot when walking, a bound (front pair, then back pair) when running, high slow
        /// tiptoe steps when sneaking. They hang from the root, so they stay on the ground under the pose.
        /// </summary>
        void UpdatePaws(bool moving, float planar)
        {
            for (int i = 0; i < Paws.Length && i < 4; i++)
            {
                var paw = Paws[i];
                if (!paw) continue;
                bool front = i < 2, left = i % 2 == 0;
                // Trot: diagonal pairs. Gallop: front pair together, back pair together.
                float trotPhase = _phase + ((left == front) ? 0f : Mathf.PI);
                float gallopPhase = _phase * 2f + (front ? 0f : Mathf.PI * 0.8f);
                float ph = Mathf.Lerp(trotPhase, gallopPhase, _runK);
                float lift = moving ? Mathf.Max(0f, Mathf.Sin(ph)) * Mathf.Lerp(Mathf.Lerp(0.06f, 0.11f, _sneakK), 0.09f, _runK) : 0f;
                float reach = moving ? Mathf.Cos(ph) * Mathf.Lerp(Mathf.Lerp(0.06f, 0.04f, _sneakK), 0.14f, _runK) : 0f;
                Vector3 rest = _pawRest[i];
                // Running: front paws reach forward under the chest, back paws push from behind.
                rest += new Vector3(0f, 0f, (front ? 0.12f : -0.08f) * _runK);
                paw.localPosition = rest + new Vector3(0f, lift, reach);
            }
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
