using NUnit.Framework;
using TrashPandas.Core.Panic;
using TrashPandas.Core.Raccoons;

namespace TrashPandas.Tests
{
    public class ReviewFixRulesTests
    {
        [Test]
        public void Riders_DontEscapeWithTheBottom()
        {
            Assert.IsTrue(TowerRules.CountsForExit(riding: false));
            Assert.IsFalse(TowerRules.CountsForExit(riding: true), "only the bottom raccoon reaches the exit");
        }

        [Test]
        public void HeavyThings_OnlyDeliveredWhenLifted()
        {
            Assert.IsTrue(HeavyCarry.CanDeliver(heavy: false, lifted: false));
            Assert.IsFalse(HeavyCarry.CanDeliver(heavy: true, lifted: false), "one raccoon straining at the gift can't bank it");
            Assert.IsTrue(HeavyCarry.CanDeliver(heavy: true, lifted: true));
        }

        [Test]
        public void DeliveredDuringTheRun_StillPays()
        {
            var pay = new RoundPayout { SharesAreSafe = true };
            pay.SetShares(new System.Collections.Generic.Dictionary<int, int> { [0] = 50 });
            pay.AddShare(0, 30);
            pay.Caught(0);
            Assert.AreEqual(80, pay.Of(0));
        }

        [Test]
        public void DeliveredAfterEscaping_IsAddedToo()
        {
            var pay = new RoundPayout { SharesAreSafe = true };
            pay.SetShares(new System.Collections.Generic.Dictionary<int, int> { [0] = 50 });
            pay.Escaped(0);
            pay.AddShare(0, 30);
            Assert.AreEqual(80, pay.Of(0));
        }

        [Test]
        public void CaughtRaccoons_CantBeMounted_EvenIfNotFrozenHere()
        {
            Assert.IsFalse(TowerRules.CanMount(targetCarryingHeavy: false, targetFrozen: false, towerSize: 1, targetCaught: true));
        }
    }
}
