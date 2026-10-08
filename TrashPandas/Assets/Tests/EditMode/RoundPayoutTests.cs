using System.Collections.Generic;
using NUnit.Framework;
using TrashPandas.Core.Loot;
using TrashPandas.Core.Panic;

namespace TrashPandas.Tests
{
    public class RoundPayoutTests
    {
        [Test]
        public void Escaped_GetsShareAndWhatsInTheirMouth()
        {
            var pay = new RoundPayout();
            pay.SetShares(new Dictionary<int, int> { [0] = 50, [1] = 50 });
            pay.Carry(0, value: 40, objective: null);
            pay.Escaped(0);
            Assert.AreEqual(90, pay.Of(0));
        }

        [Test]
        public void Caught_LosesEverything_AndDropsTheMouthItem()
        {
            var pay = new RoundPayout();
            pay.SetShares(new Dictionary<int, int> { [0] = 50 });
            pay.Carry(0, 300, ObjectiveId.Ring);
            Assert.IsTrue(pay.Caught(0), "had something to drop");
            Assert.AreEqual(0, pay.Of(0));
            Assert.IsFalse(pay.ObjectiveEscaped(ObjectiveId.Ring));
        }

        [Test]
        public void ObjectiveInMouth_CountsOnlyIfEscaped()
        {
            var pay = new RoundPayout();
            pay.Carry(2, 300, ObjectiveId.Ring);
            Assert.IsFalse(pay.ObjectiveEscaped(ObjectiveId.Ring));
            pay.Escaped(2);
            Assert.IsTrue(pay.ObjectiveEscaped(ObjectiveId.Ring));
            Assert.AreEqual(300, pay.Of(2));
        }

        [Test]
        public void Escaping_Twice_PaysOnce()
        {
            var pay = new RoundPayout();
            pay.SetShares(new Dictionary<int, int> { [0] = 50 });
            pay.Escaped(0);
            pay.Escaped(0);
            Assert.AreEqual(50, pay.Of(0));
        }

        [Test]
        public void DroppedBeforeEscaping_IsNotPaid()
        {
            var pay = new RoundPayout();
            pay.Carry(0, 40, null);
            pay.Drop(0);
            pay.Escaped(0);
            Assert.AreEqual(0, pay.Of(0));
        }

        [Test]
        public void Total_SumsEveryone()
        {
            var pay = new RoundPayout();
            pay.SetShares(new Dictionary<int, int> { [0] = 10, [1] = 20 });
            pay.Escaped(0);
            pay.Escaped(1);
            Assert.AreEqual(30, pay.Total);
        }
    }

    public class CleanExitTests
    {
        [Test]
        public void Qualifies_OnlyWithEveryoneInside_DuringInfiltration()
        {
            Assert.IsTrue(CleanExit.Qualifies(infiltrating: true, coatInArch: true, missingParts: 0, pocketTotal: 40));
            Assert.IsFalse(CleanExit.Qualifies(true, true, missingParts: 1, pocketTotal: 40), "an empty seat isn't a clean exit");
            Assert.IsFalse(CleanExit.Qualifies(true, coatInArch: false, missingParts: 0, pocketTotal: 40));
            Assert.IsFalse(CleanExit.Qualifies(infiltrating: false, coatInArch: true, missingParts: 0, pocketTotal: 40));
        }

        [Test]
        public void EmptyPocket_CantLeave()
        {
            Assert.IsFalse(CleanExit.Qualifies(true, true, 0, pocketTotal: 0), "walking straight back out isn't a heist");
        }
    }
}

namespace TrashPandas.Tests
{
    public class MouthRulesTests
    {
        [Test]
        public void CaughtRaccoon_CantPickUpAgain()
        {
            Assert.IsFalse(TrashPandas.Core.Panic.MouthRules.MayPickUp(infiltrating: false, status: TrashPandas.Core.Panic.PlayerOutcome.Caught),
                "a caught raccoon must not re-grab the objective it just dropped");
        }

        [Test]
        public void RunningOrSneaking_CanPickUp()
        {
            Assert.IsTrue(TrashPandas.Core.Panic.MouthRules.MayPickUp(false, TrashPandas.Core.Panic.PlayerOutcome.Running));
            Assert.IsTrue(TrashPandas.Core.Panic.MouthRules.MayPickUp(true, TrashPandas.Core.Panic.PlayerOutcome.None));
        }
    }
}

namespace TrashPandas.Tests
{
    public class SafeDeliveryTests
    {
        [Test]
        public void Delivered_IsSafe_WhenCaught()
        {
            var pay = new TrashPandas.Core.Panic.RoundPayout { SharesAreSafe = true };
            pay.SetShares(new System.Collections.Generic.Dictionary<int, int> { [0] = 80 });
            pay.Carry(0, 40, null);
            pay.Caught(0);
            Assert.AreEqual(80, pay.Of(0), "what reached the den stays yours; only the mouth item is lost");
        }

        [Test]
        public void Delivered_PlusMouth_WhenEscaped()
        {
            var pay = new TrashPandas.Core.Panic.RoundPayout { SharesAreSafe = true };
            pay.SetShares(new System.Collections.Generic.Dictionary<int, int> { [0] = 80 });
            pay.Carry(0, 40, null);
            pay.Escaped(0);
            Assert.AreEqual(120, pay.Of(0));
        }
    }
}
