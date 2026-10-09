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
        /// <summary>The 3D raccoon (Meshy, rigged): when set, its clips replace the greybox poses.</summary>
        public Animator Model;
        /// <summary>The model's material per player (its bandana recolored).</summary>
        public Material[] Skins = new Material[0];
        string _clip;
        float _jitter = 1f, _gaitPhase, _gaitK, _gallopK, _tiptoeK, _crouchK;
        Transform _hips, _spine, _head, _armL, _armR, _foreL, _foreR, _thighL, _thighR, _shinL, _shinR;
        public string CurrentClip => _clip;
        public float Lean => _leanK;
        float _leanK;
        float _clipSince;

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
        float _runK, _sneakK, _pushK, _hangK;

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
            bool sneaking = !riding && !running && (_raccoon && _raccoon.Crawling || (local ? _raccoon.IsSneaking : planar > 0.2f && planar < 2.0f));
            _runK = Mathf.MoveTowards(_runK, running ? 1f : 0f, dt * 6f);
            _sneakK = Mathf.MoveTowards(_sneakK, sneaking ? 1f : 0f, dt * 6f);
            bool moving = !riding && (planar > 0.3f || (_raccoon && _raccoon.Crawling && v.magnitude > 0.3f));

            _phase += planar * dt * Mathf.Lerp(Mathf.Lerp(4.2f, 2.6f, _sneakK), 3.4f, _runK);
            float walkBob = moving ? Mathf.Abs(Mathf.Sin(_phase)) * 0.06f : 0f;
            float gallop = moving ? Mathf.Max(0f, Mathf.Sin(_phase * 2f)) * 0.09f : 0f; // bounding leaps
            float bob = Mathf.Lerp(Mathf.Lerp(walkBob, walkBob * 0.4f, _sneakK), gallop, _runK);
            float sy = (1f - _squash + _stretch + bob) * Mathf.Lerp(1f, 0.72f, _sneakK);
            float sxz = (1f + _squash * 0.6f - _stretch * 0.35f) * Mathf.Lerp(1f, 1.08f, _sneakK);
            _pushK = Mathf.MoveTowards(_pushK, _raccoon && _raccoon.Pushing ? 1f : 0f, dt * 8f);
            _hangK = Mathf.MoveTowards(_hangK, _raccoon && _raccoon.Hanging ? 1f : 0f, dt * 10f);
            float pitch = Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(Mathf.Lerp(Mathf.Clamp(planar * 2.2f, 0f, 12f), 22f, _sneakK), 68f, _runK), 28f, _pushK), -8f, _hangK);
            sy *= 1f + 0.12f * _hangK; // stretched up, hanging by the paws
            float sway = moving ? Mathf.Sin(_phase) * Mathf.Lerp(Mathf.Lerp(4f, 9f, _sneakK), 2f, _runK) : 0f;
            if (Model)
            {
                // The model animates itself; keep the squash & stretch (juice) and a little sway.
                if (Visual)
                {
                    // No squashing a real model: only a subtle settle on landing (crouching is a pose, below).
                    float mSy = 1f - _squash * 0.18f + _stretch * 0.1f;
                    float mSxz = 1f + _squash * 0.08f - _stretch * 0.04f;
                    Visual.localScale = new Vector3(mSxz, mSy, mSxz);
                    // Running: a low forward lean, paws reaching for the ground (the gallop).
                    float lean = 40f * _runK * (moving ? 1f : 0f);
                    _leanK = Mathf.MoveTowards(_leanK, lean, dt * 160f);
                    _crouchK = Mathf.MoveTowards(_crouchK, !riding && _sneakK > 0.5f && _runK < 0.5f ? 1f : 0f, dt * 6f);
                    Visual.localPosition = new Vector3(0f, -0.07f * _crouchK, -0.05f * _leanK / 42f);
                    Visual.localRotation = Quaternion.Euler(_leanK + (moving ? Mathf.Sin(_phase * 2f) * 5f * _runK : 0f), 0f, sway * 0.4f);
                }
                PlayModelClip(planar, moving, riding);
                BuildGait(planar, moving && _clip == "Move", dt);
                Crouch();
                UpdateBandana();
                return;
            }
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
                // Pushing: front paws up on the thing. Hanging: front paws on the ledge, back paws dangling.
                if (front) rest += new Vector3(0f, 0.32f, 0.16f) * _pushK + new Vector3(0f, 0.62f, 0.12f) * _hangK;
                else rest += new Vector3(0f, -0.02f, -0.04f) * _hangK;
                paw.localPosition = rest + new Vector3(0f, lift * (1f - _hangK), reach * (1f - _pushK * 0.6f));
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

        static Cameras.PlayerCameraRig s_rig;
        GUIStyle _tag;

        /// <summary>On your own raccoon: "HIDDEN" when nobody can see you, "E: crawl" next to a pipe.</summary>
        void OnGUI()
        {
            if (Squad.RoundIntro.Playing) return;
            if (!s_rig) s_rig = FindFirstObjectByType<Cameras.PlayerCameraRig>();
            if (!s_rig || !s_rig.VirtualCamera || !_raccoon || _raccoon.Crawling) return;
            if (s_rig.VirtualCamera.Follow != transform && !(_raccoon.InCan && s_rig.VirtualCamera.Follow == _raccoon.InCan.transform)) return;
            var cam = Camera.main;
            if (!cam) return;
            string text = null;
            Color color = Color.white;
            var spot = Squad.HidingSpot.SpotOf(_raccoon);
            if (_raccoon.InCan) { text = "HIDDEN — E: hop out"; color = new Color(0.5f, 1f, 0.6f); Ui.DebugChecklist.Mark("hide"); }
            else if (Squad.Hideout.Near(transform.position) is Squad.Hideout near) { text = near.Prompt; color = new Color(1f, 0.9f, 0.5f); }
            else if (Squad.RaccoonPipe.Near(transform.position)) { text = "E: crawl in"; color = new Color(0.6f, 0.9f, 1f); }
            else if (spot.HasValue && Squad.HidingSpot.Hides(_raccoon)) { text = "HIDDEN"; color = new Color(0.5f, 1f, 0.6f); Ui.DebugChecklist.Mark("hide"); }
            if (text == null) return;
            Vector3 sp = cam.WorldToScreenPoint(transform.position + Vector3.up * 0.95f);
            if (sp.z <= 0f) return;
            Ui.UiScale.Apply();
            Vector2 p = Ui.UiScale.FromScreen(sp);
            _tag ??= new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            GUI.color = color;
            GUI.Label(new Rect(p.x - 70, p.y - 12, 140, 24), text, _tag);
            GUI.color = Color.white;
        }

        void FindBones()
        {
            if (_hips || !Model) return;
            foreach (var t in Model.GetComponentsInChildren<Transform>())
                switch (t.name)
                {
                    case "Hips": _hips = t; break;
                    case "Spine": _spine = t; break;
                    case "Head": _head = t; break;
                    case "LeftArm": _armL = t; break;
                    case "RightArm": _armR = t; break;
                    case "LeftForeArm": _foreL = t; break;
                    case "RightForeArm": _foreR = t; break;
                    case "LeftUpLeg": _thighL = t; break;
                    case "RightUpLeg": _thighR = t; break;
                    case "LeftLeg": _shinL = t; break;
                    case "RightLeg": _shinR = t; break;
                }
            _jitter = Random.Range(0.9f, 1.12f);
            _gaitPhase = Random.value * Mathf.PI * 2f;
        }

        /// <summary>Sneaking (moving or not): a real crouch — knees bent, back forward, head up — instead of squashing.</summary>
        void Crouch()
        {
            if (_crouchK <= 0.001f || !_hips || (_clip != "Idle" && _clip != "Move")) return;
            Transform body = Model.transform;
            Vector3 right = body.right;
            float k = _crouchK;
            Turn(_thighL, -38f * k, right);
            Turn(_thighR, -38f * k, right);
            Turn(_shinL, 62f * k, right);
            Turn(_shinR, 62f * k, right);
            Turn(_spine, 22f * k, right);
            if (_head) Turn(_head, -18f * k, right);
        }

        static void Turn(Transform bone, float degrees, Vector3 worldAxis)
        {
            if (bone && Mathf.Abs(degrees) > 0.01f) bone.rotation = Quaternion.AngleAxis(degrees, worldAxis) * bone.rotation;
        }

        /// <summary>
        /// Our own gait for a short-legged chubby raccoon, layered on the idle pose: a waddle (short steps, hip roll,
        /// arm swing), a tiptoe sneak (paws up), and a bounding gallop (front paws and back legs alternate, body low).
        /// Positive degrees about the body's right axis swing a limb backward; negative, forward.
        /// </summary>
        void BuildGait(float planar, bool moving, float dt)
        {
            FindBones();
            _gaitK = Mathf.MoveTowards(_gaitK, moving ? 1f : 0f, dt * 6f);
            _gallopK = Mathf.MoveTowards(_gallopK, moving && _runK > 0.5f ? 1f : 0f, dt * 5f);
            _tiptoeK = Mathf.MoveTowards(_tiptoeK, moving && _sneakK > 0.5f && _runK < 0.5f ? 1f : 0f, dt * 5f);
            if (_gaitK <= 0.001f || !_hips) return;
            // Short legs: about 0.32 m per step walking, longer bounds galloping.
            float stride = Mathf.Lerp(Mathf.Lerp(0.44f, 0.26f, _tiptoeK), 0.8f, _gallopK);
            _gaitPhase += planar / stride * Mathf.PI * dt * _jitter;
            float s = Mathf.Sin(_gaitPhase), c = Mathf.Cos(_gaitPhase);
            Transform body = Model.transform;
            Vector3 right = body.right, fwd = body.forward, up = body.up;
            float k = _gaitK;
            float walkK = k * (1f - _gallopK);

            // Legs: walk/sneak alternate; gallop = both together.
            float legAmp = Mathf.Lerp(Mathf.Lerp(30f, 18f, _tiptoeK), 0f, _gallopK);
            Turn(_thighL, -legAmp * s * walkK, right);
            Turn(_thighR, legAmp * s * walkK, right);
            // Knee lifts on the forward swing (the leg that's coming through bends).
            Turn(_shinL, Mathf.Max(0f, c) * Mathf.Lerp(30f, 40f, _tiptoeK) * walkK, right);
            Turn(_shinR, Mathf.Max(0f, -c) * Mathf.Lerp(30f, 40f, _tiptoeK) * walkK, right);
            // A light, fluid sway (not a waddle): a touch of hip roll and counter-twist.
            Turn(_hips, s * 2.5f * walkK, fwd);
            Turn(_hips, s * 3f * walkK, up);
            Turn(_spine, -s * 3f * walkK, up);
            Turn(_spine, 6f * walkK * (1f - _tiptoeK), right); // leaning into the walk
            // Arms: swing against the legs walking; held up and forward (sneaky paws) when tiptoeing.
            Turn(_armL, legAmp * 0.8f * s * walkK * (1f - _tiptoeK), right);
            Turn(_armR, -legAmp * 0.8f * s * walkK * (1f - _tiptoeK), right);
            Turn(_armL, -55f * _tiptoeK * k, right);
            Turn(_armR, -55f * _tiptoeK * k, right);
            Turn(_foreL, -45f * _tiptoeK * k + s * 6f * _tiptoeK, right);
            Turn(_foreR, -45f * _tiptoeK * k - s * 6f * _tiptoeK, right);

            // Gallop: body pitched (Visual lean), front paws reach and push, back legs drive together, in turn.
            if (_gallopK > 0.001f)
            {
                float g = _gallopK * k;
                float front = Mathf.Sin(_gaitPhase), back = Mathf.Sin(_gaitPhase + Mathf.PI * 0.6f);
                Turn(_armL, (-70f + front * 40f) * g, right);
                Turn(_armR, (-70f + front * 40f + 8f) * g, right);
                Turn(_foreL, -15f * g, right);
                Turn(_foreR, -15f * g, right);
                Turn(_thighL, (back * 45f + 10f) * g, right);
                Turn(_thighR, (back * 45f + 14f) * g, right);
                Turn(_shinL, Mathf.Max(0f, -back) * 50f * g, right);
                Turn(_shinR, Mathf.Max(0f, -back) * 50f * g, right);
                Turn(_spine, front * 8f * g, right); // the back flexes with each bound
                if (_head) Turn(_head, -_leanK * 0.75f, right); // keep looking ahead
            }
        }

        /// <summary>Which clip fits what the raccoon is doing (owner: from its state; others: from how it moves).</summary>
        void PlayModelClip(float planar, bool moving, bool riding)
        {
            var r = _raccoon;
            bool local = r && r.enabled;
            bool carrying = r && Squad.CarryDirector.Instance && Squad.CarryDirector.Instance.IsCarrying(r.PlayerId);
            bool airborne = r && !riding && !r.Hanging && !r.Crawling && !r.InCan && (local ? !r.IsGroundedForPeel : Mathf.Abs(_vy) > 1.2f);
            string clip; float speed = 1f;
            if (r && r.Hanging) clip = "Hang";
            else if (r && r.Crawling) { clip = "Crawl"; speed = 1.6f; }
            else if (riding) clip = "Idle";
            else if (airborne) { clip = "Jump"; speed = 1f; }
            else if (r && r.Pushing) clip = "Push";
            else if (moving) { clip = "Move"; speed = 0f; } // walk / sneak / gallop: a still neutral pose + our own gait on the bones
            else if (r && r.Emote == 1) clip = "Dance";
            else if (r && r.Emote == 2) clip = "Cheer";
            else clip = "Idle";
            if (clip != _clip && clip == "Move")
            {
                // The idle's first frame, frozen: looking straight ahead, feet under the body.
                _clip = clip;
                Model.Play("Idle", 0, 0f);
            }
            else if (clip != _clip)
            {
                _clip = clip;
                _clipSince = Time.time;
                // Loops start somewhere random so a crew never moves in lockstep.
                bool loop = clip == "Idle" || clip == "Dance" || clip == "Hang" || clip == "Crawl" || clip == "Push";
                Model.CrossFade(clip, clip == "Jump" ? 0.06f : 0.15f, 0, loop ? Random.value : 0f);
            }
            Model.speed = speed * _jitter;
        }

        void UpdateBandana()
        {
            if (Model && Skins.Length > 0 && _raccoon && _raccoon.PlayerId != _colorFor)
            {
                _colorFor = _raccoon.PlayerId;
                var skin = Skins[Mathf.Abs(_colorFor) % Skins.Length];
                foreach (var smr in Model.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.sharedMaterial = skin;
                return;
            }
            if (!Bandana || !_raccoon || _raccoon.PlayerId == _colorFor) return;
            _colorFor = _raccoon.PlayerId;
            Bandana.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", PlayerColors[Mathf.Abs(_colorFor) % PlayerColors.Length]);
            Bandana.SetPropertyBlock(_block);
        }
    }
}
