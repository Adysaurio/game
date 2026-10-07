using System.Collections.Generic;
using NUnit.Framework;
using TrashPandas.Core.Events;

namespace TrashPandas.Tests
{
    public class EventRollTests
    {
        [Test]
        public void Roll_AlwaysAsksSomethingOfTheBody()
        {
            var r = new System.Random(1);
            for (int i = 0; i < 500; i++)
            {
                var roll = EventRoll.Roll(r);
                Assert.IsFalse(roll.Arms == ArmsTask.None && roll.Legs == LegsTask.None, $"roll {i}");
            }
        }

        [Test]
        public void Roll_CoversEveryTaskOverTime()
        {
            var r = new System.Random(2);
            var arms = new HashSet<ArmsTask>();
            var legs = new HashSet<LegsTask>();
            for (int i = 0; i < 500; i++) { var roll = EventRoll.Roll(r); arms.Add(roll.Arms); legs.Add(roll.Legs); }
            Assert.AreEqual(4, arms.Count, "None, Handshake, TakeGlass, HandsTogether");
            Assert.AreEqual(4, legs.Count, "None, StayStill, Kneel, DanceStep");
        }

        [Test]
        public void Roll_ShufflesAnswerOrder()
        {
            var r = new System.Random(3);
            var orders = new HashSet<string>();
            for (int i = 0; i < 200; i++)
            {
                var roll = EventRoll.Roll(r);
                CollectionAssert.AreEquivalent(new[] { 0, 1, 2 }, EventRoll.DecodeOrder(roll.Order));
                orders.Add(string.Join(",", EventRoll.DecodeOrder(roll.Order)));
            }
            Assert.AreEqual(6, orders.Count, "all six orders appear");
        }

        [Test]
        public void Apply_PermutesOptionsAndKindsTogether_AndSetsTasks()
        {
            var template = new SocialEvent
            {
                Speaker = "Waiter", SpeakerName = "The waiter", Line = "Champagne?",
                Options = new[] { "good", "odd", "absurd" },
                OptionKinds = new[] { HeadAnswer.Good, HeadAnswer.Odd, HeadAnswer.Absurd },
            };
            var roll = new RolledTasks { Arms = ArmsTask.HandsTogether, Legs = LegsTask.Kneel, Order = EventRoll.EncodeOrder(new[] { 2, 0, 1 }) };
            var e = EventRoll.Apply(template, roll);

            CollectionAssert.AreEqual(new[] { "absurd", "good", "odd" }, e.Options);
            CollectionAssert.AreEqual(new[] { HeadAnswer.Absurd, HeadAnswer.Good, HeadAnswer.Odd }, e.OptionKinds);
            Assert.AreEqual(ArmsTask.HandsTogether, e.Arms);
            Assert.AreEqual(LegsTask.Kneel, e.Legs);
            Assert.AreEqual("Champagne?", e.Line);
            CollectionAssert.AreEqual(new[] { "good", "odd", "absurd" }, template.Options, "the catalog entry is untouched");
        }

        [Test]
        public void DecodeOrder_BadValue_FallsBackToIdentity()
        {
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, EventRoll.DecodeOrder(250));
        }
    }
}
