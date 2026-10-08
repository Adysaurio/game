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
        public float CrawlSeconds = 1.1f;
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
            if (!r || r.Crawling || r.Frozen || !OtherEnd) return;
            r.StartCoroutine(r.CrawlTo(OtherEnd.position + OtherEnd.forward * 0.6f, CrawlSeconds));
        }
    }
}
