using System.Linq;
using NUnit.Framework;
using TrashPandas.Core.Loot;

namespace TrashPandas.Tests
{
    public class LootPocketTests
    {
        [Test]
        public void Add_SumsValues()
        {
            var p = new LootPocket();
            Assert.IsTrue(p.Add(1, 40, null));
            Assert.IsTrue(p.Add(2, 60, null));
            Assert.AreEqual(100, p.Total);
        }

        [Test]
        public void Add_SameItemTwice_CountsOnce()
        {
            var p = new LootPocket();
            p.Add(7, 40, null);
            Assert.IsFalse(p.Add(7, 40, null));
            Assert.AreEqual(40, p.Total);
        }

        [Test]
        public void Objectives_AreMarkedDone()
        {
            var p = new LootPocket();
            p.Add(3, 300, ObjectiveId.Ring);
            Assert.IsTrue(p.IsDone(ObjectiveId.Ring));
            Assert.IsFalse(p.IsDone(ObjectiveId.Bouquet));
        }

        [Test]
        public void Split_EqualShares_RemainderToFirst()
        {
            var p = new LootPocket();
            p.Add(1, 100, null);
            var shares = p.Split(new[] { 4, 9, 2 });
            Assert.AreEqual(34, shares[4]);
            Assert.AreEqual(33, shares[9]);
            Assert.AreEqual(33, shares[2]);
            Assert.AreEqual(100, shares.Values.Sum());
        }

        [Test]
        public void Split_NoPlayers_IsEmpty()
        {
            var p = new LootPocket();
            p.Add(1, 100, null);
            Assert.AreEqual(0, p.Split(new int[0]).Count);
        }

        [Test]
        public void CleanExit_PaysOneAndAHalf()
        {
            var p = new LootPocket();
            p.Add(1, 100, null);
            var shares = p.CleanExitPayout(new[] { 0, 1 });
            Assert.AreEqual(150, shares.Values.Sum());
            Assert.AreEqual(75, shares[0]);
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            var p = new LootPocket();
            p.Add(1, 300, ObjectiveId.Ring);
            p.Reset();
            Assert.AreEqual(0, p.Total);
            Assert.IsFalse(p.IsDone(ObjectiveId.Ring));
            Assert.IsTrue(p.Add(1, 300, ObjectiveId.Ring), "the same item can be stashed again next round");
        }
    }

    public class LootCatalogTests
    {
        [Test]
        public void Values_MatchSpec()
        {
            Assert.AreEqual(40, LootCatalog.ValueOf(LootKind.Wallet));
            Assert.AreEqual(60, LootCatalog.ValueOf(LootKind.Phone));
            Assert.AreEqual(15, LootCatalog.ValueOf(LootKind.Cutlery));
            Assert.AreEqual(30, LootCatalog.ValueOf(LootKind.Bottle));
            Assert.AreEqual(5, LootCatalog.ValueOf(LootKind.Food));
        }

        [Test]
        public void SixObjectives_WithSpecValues()
        {
            Assert.AreEqual(6, LootCatalog.Objectives.Count);
            Assert.AreEqual(300, LootCatalog.Objective(ObjectiveId.Ring).Value);
            Assert.AreEqual(100, LootCatalog.Objective(ObjectiveId.Keys).Value);
            Assert.IsFalse(string.IsNullOrEmpty(LootCatalog.Objective(ObjectiveId.CakeTopper).Name));
        }
    }

    public class StashGestureTests
    {
        [Test]
        public void HoldNearChest_HalfSecond_Stashes()
        {
            var g = new StashGesture();
            Assert.IsFalse(g.Tick(0.3f, 0.25f));
            Assert.IsTrue(g.Tick(0.3f, 0.26f));
        }

        [Test]
        public void TooFar_NeverStashes()
        {
            var g = new StashGesture();
            for (int i = 0; i < 20; i++) Assert.IsFalse(g.Tick(0.6f, 0.1f));
        }

        [Test]
        public void MovingAway_Resets()
        {
            var g = new StashGesture();
            g.Tick(0.3f, 0.4f);
            g.Tick(0.8f, 0.05f);
            Assert.IsFalse(g.Tick(0.3f, 0.2f), "the clock restarted");
        }

        [Test]
        public void FiresOnlyOnce_UntilReset()
        {
            var g = new StashGesture();
            g.Tick(0.3f, 0.6f);
            Assert.IsFalse(g.Tick(0.3f, 0.6f));
            g.Reset();
            Assert.IsTrue(g.Tick(0.3f, 0.6f));
        }
    }
}
