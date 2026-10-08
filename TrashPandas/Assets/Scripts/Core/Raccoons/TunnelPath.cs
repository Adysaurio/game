using UnityEngine;

namespace TrashPandas.Core.Raccoons
{
    /// <summary>A drain pipe's route (polyline): where you are and which way is "forward" after crawling s meters.</summary>
    public sealed class TunnelPath
    {
        readonly Vector3[] _p;
        readonly float[] _cum;
        public float Length { get; }

        public TunnelPath(Vector3[] points)
        {
            _p = points;
            _cum = new float[points.Length];
            for (int i = 1; i < points.Length; i++) _cum[i] = _cum[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            Length = _cum[points.Length - 1];
        }

        int SegmentAt(float s)
        {
            for (int i = 1; i < _p.Length; i++) if (s <= _cum[i]) return i;
            return _p.Length - 1;
        }

        public Vector3 PointAt(float s)
        {
            s = Mathf.Clamp(s, 0f, Length);
            int i = SegmentAt(s);
            float seg = _cum[i] - _cum[i - 1];
            return seg <= 0f ? _p[i] : Vector3.Lerp(_p[i - 1], _p[i], (s - _cum[i - 1]) / seg);
        }

        public Vector3 DirectionAt(float s)
        {
            int i = SegmentAt(Mathf.Clamp(s, 0f, Length));
            return (_p[i] - _p[i - 1]).normalized;
        }

        public TunnelPath Reversed()
        {
            var r = new Vector3[_p.Length];
            for (int i = 0; i < _p.Length; i++) r[i] = _p[_p.Length - 1 - i];
            return new TunnelPath(r);
        }
    }
}
