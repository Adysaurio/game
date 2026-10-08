using TrashPandas.Runtime.Raccoon;
using UnityEngine;

namespace TrashPandas.Runtime.Ui
{
    /// <summary>The throw preview: a dotted arc and a ring where it lands. Shown while you hold to aim.</summary>
    public sealed class AimArc : MonoBehaviour
    {
        static AimArc s_instance;
        LineRenderer _arc, _ring;
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

        /// <summary>Call every frame while aiming.</summary>
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
            float pulse = 0.45f + 0.08f * Mathf.Sin(Time.time * 8f);
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
            _arc.enabled = _ring.enabled = false;
        }
    }
}
