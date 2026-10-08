using System.Collections.Generic;
using TrashPandas.Runtime.Raccoon;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>
    /// A drain pipe: raccoon-only shortcut between two zones. E at one mouth, pop out of the other a moment
    /// later (unseen while inside).
    /// </summary>
    public sealed class RaccoonPipe : MonoBehaviour
    {
        public Transform OtherEnd;
        [Tooltip("World points from this mouth, down the shaft, along the tunnel, up to the other mouth.")]
        public Vector3[] Path = new Vector3[0];
        public const float EnterRadius = 0.9f;

        static readonly List<RaccoonPipe> s_all = new List<RaccoonPipe>();
        void OnEnable() { s_all.RemoveAll(h => !h); if (!s_all.Contains(this)) s_all.Add(this); }
        void OnDisable() => s_all.Remove(this);

        public static RaccoonPipe Near(Vector3 p)
        {
            foreach (var pipe in s_all)
            {
                if (!pipe || !pipe.OtherEnd) continue;
                Vector3 d = pipe.transform.position - p;
                d.y = 0f;
                if (d.magnitude <= EnterRadius) return pipe;
            }
            return null;
        }

        /// <summary>Owner side: crawl through (works offline and online — the owner moves its raccoon).</summary>
        public void Crawl(RaccoonController r)
        {
            if (!r || r.Crawling || r.Frozen || !OtherEnd || Path.Length < 2) return;
            r.EnterTunnel(new Core.Raccoons.TunnelPath(Path), OtherEnd.forward, transform.forward);
            if (Net.SimulationAuthority.IsSimulating) Panic.NemesisDirector.Instance?.OnPipe(r, OtherEnd.position);
        }
    }
}
