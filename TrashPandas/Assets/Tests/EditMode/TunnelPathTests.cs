using NUnit.Framework;
using TrashPandas.Core.Raccoons;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class TunnelPathTests
    {
        static TunnelPath Make() => new TunnelPath(new[] { new Vector3(0, 0, 0), new Vector3(0, -1, 0), new Vector3(10, -1, 0), new Vector3(10, 0, 0) });

        [Test]
        public void Length_IsTheSumOfSegments() => Assert.AreEqual(12f, Make().Length, 1e-4f);

        [Test]
        public void PointAt_WalksAlongThePath()
        {
            var t = Make();
            Assert.AreEqual(new Vector3(0, -0.5f, 0), t.PointAt(0.5f));
            Assert.AreEqual(new Vector3(5, -1, 0), t.PointAt(6f));
            Assert.AreEqual(new Vector3(10, 0, 0), t.PointAt(99f), "clamped at the far mouth");
            Assert.AreEqual(Vector3.zero, t.PointAt(-3f), "clamped at the near mouth");
        }

        [Test]
        public void DirectionAt_FollowsTheSegment()
        {
            var t = Make();
            Assert.AreEqual(Vector3.down, t.DirectionAt(0.5f));
            Assert.AreEqual(Vector3.right, t.DirectionAt(6f));
        }

        [Test]
        public void Reversed_GoesTheOtherWay()
        {
            var r = Make().Reversed();
            Assert.AreEqual(new Vector3(10, 0, 0), r.PointAt(0f));
            Assert.AreEqual(Vector3.left, r.DirectionAt(6f));
        }
    }
}
