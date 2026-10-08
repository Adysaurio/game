using NUnit.Framework;
using TrashPandas.Core.Panic;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class NemesisTests
    {
        [Test]
        public void Roll_NeverRepeatsLastRound()
        {
            for (int seed = 0; seed < 200; seed++)
                Assert.AreNotEqual(NemesisKind.Granny, NemesisPick.Roll(seed, NemesisKind.Granny));
        }

        [Test]
        public void Roll_GivesEveryKindSometimes()
        {
            var seen = new System.Collections.Generic.HashSet<NemesisKind>();
            for (int seed = 0; seed < 100; seed++) seen.Add(NemesisPick.Roll(seed, null));
            Assert.AreEqual(3, seen.Count);
        }

        [Test]
        public void EachKind_HasItsOwnRule()
        {
            var planner = NemesisProfile.Of(NemesisKind.WeddingPlanner);
            var pest = NemesisProfile.Of(NemesisKind.PestControl);
            var granny = NemesisProfile.Of(NemesisKind.Granny);
            Assert.IsTrue(planner.Radio, "the planner hears every witness, anywhere");
            Assert.IsFalse(pest.Radio);
            Assert.Greater(pest.SeesIntoHideoutsWithin, 0f, "the flashlight");
            Assert.Greater(pest.Pursuit.ChaseBeforeWinded, 60f, "never tires");
            Assert.Greater(granny.Pursuit.SwingRange, 3f, "the slipper flies");
            Assert.Greater(granny.Hearing, 1.5f);
            Assert.Greater(planner.Speed, granny.Speed);
        }

        [Test]
        public void Memory_ChecksAHideoutUsedTwice()
        {
            var m = new NemesisMemory();
            m.HidIn(7);
            Assert.IsFalse(m.Suspects(7));
            m.HidIn(7);
            Assert.IsTrue(m.Suspects(7), "same trick twice");
            Assert.IsFalse(m.Suspects(3));
        }

        [Test]
        public void Memory_PebblesStopWorking()
        {
            var m = new NemesisMemory();
            Assert.IsTrue(m.FallsForPebble());
            Assert.IsTrue(m.FallsForPebble());
            Assert.IsFalse(m.FallsForPebble(), "not again");
        }

        [Test]
        public void Memory_AmbushesThePipeYouWereSeenEntering()
        {
            var m = new NemesisMemory();
            Assert.IsNull(m.Ambush);
            m.SawEnterPipe(new Vector3(5f, 0f, 1f));
            Assert.AreEqual(new Vector3(5f, 0f, 1f), m.Ambush);
            m.AmbushDone();
            Assert.IsNull(m.Ambush);
        }
    }
}
