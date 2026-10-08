using TrashPandas.Runtime.Raccoon;
using UnityEngine;

namespace TrashPandas.Runtime.Ui
{
    /// <summary>The throw preview: a dotted arc and a ring where it lands. Shown while you hold to aim.</summary>
    public sealed class AimArc : MonoBehaviour
    {
        static AimArc s_instance;
        LineRenderer _arc, _ring, _effect;
        int _shownFrame = -1;
        const int RingSegments = 24;

        static AimArc Get()
        {
            if (s_instance) return s_instance;
            s_instance = new GameObject("AimArc").AddComponent<AimArc>();
            return s_instance;
        }

        void Awake()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            _arc = MakeLine("Arc", mat, 0.06f);
            _ring = MakeLine("Ring", mat, 0.05f);
            _ring.loop = true;
            _ring.positionCount = RingSegments;
            _effect = MakeLine("Effect", mat, 0.04f);
            _effect.loop = true;
            _effect.positionCount = RingSegments;
        }

        LineRenderer MakeLine(string name, Material mat, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var l = go.AddComponent<LineRenderer>();
            l.sharedMaterial = mat;
            l.widthMultiplier = width;
            l.numCapVertices = 4;
            l.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            l.receiveShadows = false;
            l.enabled = false;
            return l;
        }

        /// <summary>Where the landing marker is (for the HUD's distance label), valid while <see cref="Showing"/>.</summary>
        public static Vector3 Landing { get; private set; }
        public static float Distance { get; private set; }
        public static bool TooFar { get; private set; }
        public static bool Showing => s_instance && s_instance._shownFrame >= Time.frameCount - 1;

        /// <summary>
        /// What the crosshair (screen center) points at, ignoring raccoons; clamped to <paramref name="maxRange"/>
        /// from <paramref name="origin"/>. Too far → the point at max range (and the marker turns red).
        /// </summary>
        public static Vector3 AimPoint(Camera cam, Vector3 origin, float maxRange, out bool tooFar)
        {
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            Vector3 point = ray.GetPoint(60f);
            var hits = Physics.RaycastAll(ray, 60f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var h in hits)
                if (h.distance < best && !h.collider.GetComponentInParent<RaccoonController>()) { best = h.distance; point = h.point; }
            if (best == float.MaxValue && Physics.Raycast(point + Vector3.up * 50f, Vector3.down, out var down, 200f, ~0, QueryTriggerInteraction.Ignore)) point = down.point;
            Vector3 d = point - origin;
            Vector2 flat = new Vector2(d.x, d.z);
            tooFar = flat.magnitude > maxRange;
            if (tooFar)
            {
                flat = flat.normalized * maxRange;
                Vector3 p = new Vector3(origin.x + flat.x, origin.y + 2f, origin.z + flat.y);
                point = Physics.Raycast(p, Vector3.down, out var g, 20f, ~0, QueryTriggerInteraction.Ignore) ? g.point : new Vector3(p.x, origin.y - 0.5f, p.z);
            }
            return point;
        }

        /// <summary>Call every frame while aiming: the arc, a small landing marker, and the effect area.</summary>
        public static Vector3 Show(Vector3 origin, Vector3 velocity, Color color, float effectRadius, bool tooFar = false)
        {
            var landing = Show(origin, velocity, tooFar ? new Color(1f, 0.3f, 0.25f) : color);
            var a = Get();
            a._effect.enabled = effectRadius > 0.6f;
            if (a._effect.enabled)
            {
                Color ec = tooFar ? new Color(1f, 0.3f, 0.25f, 0.5f) : new Color(color.r, color.g, color.b, 0.45f);
                a._effect.startColor = a._effect.endColor = ec;
                for (int i = 0; i < RingSegments; i++)
                {
                    float ang = i * Mathf.PI * 2f / RingSegments;
                    a._effect.SetPosition(i, landing + new Vector3(Mathf.Cos(ang) * effectRadius, 0.06f, Mathf.Sin(ang) * effectRadius));
                }
            }
            TooFar = tooFar;
            Distance = new Vector2(landing.x - origin.x, landing.z - origin.z).magnitude;
            return landing;
        }

        public static Vector3 Show(Vector3 origin, Vector3 velocity, Color color)
        {
            var a = Get();
            a._shownFrame = Time.frameCount;
            var points = new System.Collections.Generic.List<Vector3> { origin };
            Vector3 p = origin, landing = origin;
            for (float t = 0.04f; t < 3f; t += 0.04f)
            {
                Vector3 next = origin + velocity * t + 0.5f * Physics.gravity * t * t;
                if (Physics.Linecast(p, next, out var hit, ~0, QueryTriggerInteraction.Ignore) && !hit.collider.GetComponentInParent<RaccoonController>())
                {
                    points.Add(hit.point);
                    landing = hit.point;
                    break;
                }
                points.Add(next);
                p = next;
                landing = next;
            }
            a._arc.enabled = a._ring.enabled = true;
            a._arc.positionCount = points.Count;
            a._arc.SetPositions(points.ToArray());
            a._arc.startColor = new Color(color.r, color.g, color.b, 0.2f);
            a._arc.endColor = color;
            a._ring.startColor = a._ring.endColor = color;
            Landing = landing;
            float pulse = 0.22f + 0.05f * Mathf.Sin(Time.time * 8f);
            for (int i = 0; i < RingSegments; i++)
            {
                float ang = i * Mathf.PI * 2f / RingSegments;
                a._ring.SetPosition(i, landing + new Vector3(Mathf.Cos(ang) * pulse, 0.05f, Mathf.Sin(ang) * pulse));
            }
            return landing;
        }

        void LateUpdate()
        {
            if (_shownFrame >= Time.frameCount - 1) return;
            _arc.enabled = _ring.enabled = _effect.enabled = false;
        }
    }
}
