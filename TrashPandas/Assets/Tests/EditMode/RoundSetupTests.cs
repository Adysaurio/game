using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TrashPandas.Core.Loot;
using TrashPandas.Core.Round;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class RoundSetupTests
    {
        static ObjectiveSpots[] AllObjectives() =>
            LootCatalog.Objectives.Select((o, i) => new ObjectiveSpots { Id = o.Id, Spots = new[] { new Vector3(i, 0, 0), new Vector3(i, 0, 10) } }).ToArray();

        [Test]
        public void PickObjectives_ThreeDistinct_EachAtOneOfItsSpots()
        {
            var all = AllObjectives();
            for (int seed = 0; seed < 50; seed++)
            {
                var picked = RoundSetup.PickObjectives(all, 3, new System.Random(seed));
                Assert.AreEqual(3, picked.Count);
                Assert.AreEqual(3, picked.Select(p => p.Id).Distinct().Count());
                foreach (var p in picked)
                    CollectionAssert.Contains(all.First(a => a.Id == p.Id).Spots, p.Spot);
            }
        }

        [Test]
        public void PickObjectives_VariesBetweenRounds()
        {
            var all = AllObjectives();
            var sets = new HashSet<string>();
            for (int seed = 0; seed < 30; seed++)
                sets.Add(string.Join(",", RoundSetup.PickObjectives(all, 3, new System.Random(seed)).Select(p => p.Id).OrderBy(x => x)));
            Assert.Greater(sets.Count, 5);
        }

        static readonly ExitCandidate[] FiveExits =
        {
            new ExitCandidate { Position = new Vector3(-25, 0, 5), Zone = 1 },   // kitchen door
            new ExitCandidate { Position = new Vector3(0, 0, 22), Zone = 2 },    // bathroom window
            new ExitCandidate { Position = new Vector3(26, 0, 0), Zone = 3 },    // van
            new ExitCandidate { Position = new Vector3(20, 0, -20), Zone = 4 },  // orchard hedge
            new ExitCandidate { Position = new Vector3(-22, 0, -8), Zone = 1 },  // sewer behind the tent (kitchen zone)
        };

        [Test]
        public void PickExits_ThreeFromDifferentZones_FarFromCenter()
        {
            for (int seed = 0; seed < 50; seed++)
            {
                var picked = RoundSetup.PickExits(FiveExits, Vector3.zero, 3, 12f, new System.Random(seed));
                Assert.AreEqual(3, picked.Count);
                Assert.AreEqual(3, picked.Select(i => FiveExits[i].Zone).Distinct().Count(), $"seed {seed}");
                Assert.AreEqual(3, picked.Distinct().Count());
                foreach (var i in picked) Assert.GreaterOrEqual(FiveExits[i].Position.magnitude, 12f);
            }
        }

        [Test]
        public void PickExits_ImpossibleRules_RelaxesWithoutRepeats()
        {
            var cramped = new[]
            {
                new ExitCandidate { Position = new Vector3(3, 0, 0), Zone = 0 },
                new ExitCandidate { Position = new Vector3(20, 0, 0), Zone = 0 },
                new ExitCandidate { Position = new Vector3(-20, 0, 0), Zone = 1 },
            };
            var picked = RoundSetup.PickExits(cramped, Vector3.zero, 3, 12f, new System.Random(1));
            Assert.AreEqual(3, picked.Count);
            Assert.AreEqual(3, picked.Distinct().Count());
            Assert.AreEqual(2, RoundSetup.PickExits(cramped.Take(2).ToArray(), Vector3.zero, 3, 12f, new System.Random(1)).Count, "can't invent exits");
        }

        [Test]
        public void PickLootSpots_NoRepeats_AndCapped()
        {
            var spots = Enumerable.Range(0, 40).Select(i => new Vector3(i, 0, 0)).ToArray();
            var picked = RoundSetup.PickLootSpots(spots, 25, new System.Random(3));
            Assert.AreEqual(25, picked.Count);
            Assert.AreEqual(25, picked.Distinct().Count());
            Assert.AreEqual(40, RoundSetup.PickLootSpots(spots, 99, new System.Random(3)).Count);
        }

        [Test]
        public void SameSeed_SameRound()
        {
            var a = RoundSetup.PickExits(FiveExits, Vector3.zero, 3, 12f, new System.Random(42));
            var b = RoundSetup.PickExits(FiveExits, Vector3.zero, 3, 12f, new System.Random(42));
            CollectionAssert.AreEqual(a, b);
        }
    }

    public class InfiltrationClockTests
    {
        [Test]
        public void CountsDownFromEightMinutes()
        {
            var c = new InfiltrationClock();
            c.Begin(100f);
            Assert.AreEqual(480f, c.SecondsLeft(100f), 1e-3f);
            Assert.AreEqual(380f, c.SecondsLeft(200f), 1e-3f);
            Assert.AreEqual(0f, c.SecondsLeft(1000f), 1e-3f);
        }

        [Test]
        public void Overtime_PushesSuspicionUp()
        {
            var c = new InfiltrationClock();
            c.Begin(0f);
            Assert.AreEqual(0f, c.OvertimeSuspicion(479f, 0.5f), 1e-3f);
            Assert.AreEqual(5f, c.OvertimeSuspicion(481f, 0.5f), 1e-3f);
        }

        [Test]
        public void NotStarted_NoOvertime()
        {
            var c = new InfiltrationClock();
            Assert.AreEqual(0f, c.OvertimeSuspicion(9999f, 1f), 1e-3f);
        }
    }
}
