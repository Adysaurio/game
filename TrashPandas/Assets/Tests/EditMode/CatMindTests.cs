using NUnit.Framework;
using TrashPandas.Core.Npc;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class CatMindTests
    {
        static readonly Vector3[] Route = { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 10) };
        static readonly Vector3 FarCoat = new Vector3(-30, 0, -30);

        [Test]
        public void Patrols_WalksToNextPointWhenReached()
        {
            var cat = new CatMind(Route);
            Assert.AreEqual(CatState.Patrol, cat.Update(0.1f, new Vector3(0.2f, 0, 0), FarCoat));
            Assert.AreEqual(Route[1], cat.Destination, "already at point 0, heads to point 1");
            cat.Update(0.1f, new Vector3(9.8f, 0, 0), FarCoat);
            Assert.AreEqual(Route[2], cat.Destination);
        }

        [Test]
        public void SmellsCoat_ApproachesThenHisses()
        {
            var cat = new CatMind(Route);
            Assert.AreEqual(CatState.Sniffing, cat.Update(0.1f, new Vector3(5, 0, 0), new Vector3(5, 0, 3)));
            Assert.AreEqual(new Vector3(5, 0, 3), cat.Destination);
            Assert.AreEqual(CatState.Hissing, cat.Update(0.1f, new Vector3(5, 0, 2), new Vector3(5, 0, 3)));
            Assert.IsTrue(cat.IsHissing);
        }

        [Test]
        public void AfterHissing_WalksAway_IgnoresCoatForAWhile()
        {
            var cat = new CatMind(Route);
            var coat = new Vector3(5, 0, 3);
            cat.Update(0.1f, new Vector3(5, 0, 2), coat);           // sniff
            for (int i = 0; i < 30; i++) cat.Update(0.1f, new Vector3(5, 0, 2), coat); // hiss for 3 s
            Assert.AreEqual(CatState.Cooldown, cat.State);
            Assert.IsFalse(cat.IsHissing);
            for (int i = 0; i < 30; i++) cat.Update(0.1f, new Vector3(5, 0, 2), coat);
            Assert.AreEqual(CatState.Cooldown, cat.State, "still cooling down 3 s later");
            for (int i = 0; i < 15; i++) cat.Update(0.1f, new Vector3(5, 0, 2), coat);
            Assert.IsTrue(cat.State == CatState.Sniffing || cat.State == CatState.Hissing,
                $"the coat is still right there: the cat engages again (was {cat.State})");
        }

        [Test]
        public void CoatWalksAway_CatGivesUp()
        {
            var cat = new CatMind(Route);
            cat.Update(0.1f, new Vector3(5, 0, 0), new Vector3(5, 0, 3));
            Assert.AreEqual(CatState.Patrol, cat.Update(0.1f, new Vector3(5, 0, 0), new Vector3(5, 0, 9)));
        }

        [Test]
        public void Stuck_SkipsToNextPatrolPoint()
        {
            var cat = new CatMind(Route);
            cat.Update(0.1f, new Vector3(0.2f, 0, 0), FarCoat); // heading to point 1
            for (int i = 0; i < 35; i++) cat.Update(0.1f, new Vector3(3f, 0, 0), FarCoat); // not moving for 3.5 s
            Assert.AreEqual(Route[2], cat.Destination);
        }
    }
}
