using NUnit.Framework;
using TrashPandas.Core.Raccoons;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class FoodPowerTests
    {
        [Test]
        public void Eating_GivesAPower_ThatWearsOff()
        {
            var p = new PowerState();
            Assert.AreEqual(FoodPower.None, p.Current(0f));
            p.Eat(FoodPower.Beans, 10f);
            Assert.AreEqual(FoodPower.Beans, p.Current(15f));
            Assert.AreEqual(FoodPower.None, p.Current(10f + PowerState.Duration + 0.1f));
        }

        [Test]
        public void OnePowerAtATime_TheNewOneReplaces()
        {
            var p = new PowerState();
            p.Eat(FoodPower.Beans, 0f);
            p.Eat(FoodPower.Salsa, 1f);
            Assert.AreEqual(FoodPower.Salsa, p.Current(2f));
        }

        [Test]
        public void Beans_OneFartJumpPerAirtime()
        {
            var p = new PowerState();
            Assert.IsFalse(p.TryFartJump(0f, grounded: false), "no beans");
            p.Eat(FoodPower.Beans, 0f);
            Assert.IsFalse(p.TryFartJump(1f, grounded: true), "only in the air");
            Assert.IsTrue(p.TryFartJump(1f, false));
            Assert.IsFalse(p.TryFartJump(1.1f, false), "one per jump");
            p.Landed();
            Assert.IsTrue(p.TryFartJump(2f, false));
        }

        [Test]
        public void Salsa_BurpHasACooldown()
        {
            var p = new PowerState();
            p.Eat(FoodPower.Salsa, 0f);
            Assert.IsTrue(p.TryBurp(1f));
            Assert.IsFalse(p.TryBurp(1.2f));
            Assert.IsTrue(p.TryBurp(1f + PowerState.BurpCooldown));
        }

        [Test]
        public void FireBurp_HitsAShortConeInFront()
        {
            var o = Vector3.zero; var f = Vector3.forward;
            Assert.IsTrue(FireBurp.Hits(o, f, new Vector3(0f, 0f, 2f)));
            Assert.IsTrue(FireBurp.Hits(o, f, new Vector3(0.8f, 0.3f, 2f)), "a bit to the side");
            Assert.IsFalse(FireBurp.Hits(o, f, new Vector3(0f, 0f, 5f)), "too far");
            Assert.IsFalse(FireBurp.Hits(o, f, new Vector3(0f, 0f, -1f)), "behind");
        }
    }
}
