using System.Collections.Generic;
using NUnit.Framework;
using TrashPandas.Core.Panic;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class PursuitMindTests
    {
        static readonly Vector3 Home = new Vector3(10, 0, 0);
        static PursuitMind Make() => new PursuitMind(Home, new PursuitPersonality { SearchSeconds = 4f, ChaseBeforeWinded = 8f, WindedSeconds = 2f, Windup = 0.5f, SwingRange = 1.2f, SwingCooldown = 1.2f });
        static PursuitInput See(Vector3 self, Vector3 target) => new PursuitInput { Self = self, HasWeapon = true, Visible = new ChaseTarget { Id = 1, Position = target } };
        static PursuitInput Blind(Vector3 self) => new PursuitInput { Self = self, HasWeapon = true };

        [Test]
        public void ChasesWhatItSees()
        {
            var o = Make().Update(0.1f, See(Vector3.zero, new Vector3(5, 0, 0)));
            Assert.AreEqual(PursuitState.Chase, o.State);
            Assert.AreEqual(new Vector3(5, 0, 0), o.Destination);
        }

        [Test]
        public void LosingSight_GoesToTheLastPlaceItSawYou()
        {
            var m = Make();
            m.Update(0.1f, See(Vector3.zero, new Vector3(5, 0, 0)));
            var o = m.Update(0.1f, Blind(Vector3.zero));
            Assert.AreEqual(PursuitState.Search, o.State);
            Assert.AreEqual(new Vector3(5, 0, 0), o.Destination, "not where you really are now");
        }

        [Test]
        public void SearchesAWhile_ThenGivesUpAndGoesHome()
        {
            var m = Make();
            m.Update(0.1f, See(Vector3.zero, new Vector3(5, 0, 0)));
            PursuitOutput o = default;
            for (int i = 0; i < 39; i++) o = m.Update(0.1f, Blind(new Vector3(5, 0, 0)));
            Assert.AreEqual(PursuitState.Search, o.State, "still looking at 3.9 s");
            o = m.Update(0.2f, Blind(new Vector3(5, 0, 0)));
            Assert.AreEqual(PursuitState.Return, o.State);
            Assert.AreEqual(Home, o.Destination);
        }

        [Test]
        public void SpottingYouAgainWhileSearching_ResumesTheChase()
        {
            var m = Make();
            m.Update(0.1f, See(Vector3.zero, new Vector3(5, 0, 0)));
            m.Update(1f, Blind(Vector3.zero));
            Assert.AreEqual(PursuitState.Chase, m.Update(0.1f, See(Vector3.zero, new Vector3(2, 0, 2))).State);
        }

        [Test]
        public void LongChases_LeaveThemWinded_ThenTheyGoOn()
        {
            var m = Make();
            PursuitOutput o = default;
            for (int i = 0; i < 81; i++) o = m.Update(0.1f, See(Vector3.zero, new Vector3(6, 0, 0)));
            Assert.AreEqual(PursuitState.Winded, o.State, "8 s of running: catching their breath");
            for (int i = 0; i < 21; i++) o = m.Update(0.1f, See(Vector3.zero, new Vector3(6, 0, 0)));
            Assert.AreEqual(PursuitState.Chase, o.State);
        }

        [Test]
        public void TheyWindUp_BeforeStriking()
        {
            var m = Make();
            var o = m.Update(0.1f, See(Vector3.zero, new Vector3(1, 0, 0)));
            Assert.AreEqual(PursuitState.Windup, o.State);
            Assert.IsFalse(o.Strike, "no instant hits: there's a beat to dodge");
            o = m.Update(0.3f, See(Vector3.zero, new Vector3(1, 0, 0)));
            Assert.IsFalse(o.Strike);
            o = m.Update(0.3f, See(Vector3.zero, new Vector3(1, 0, 0)));
            Assert.IsTrue(o.Strike);
        }

        [Test]
        public void Stunned_DoesNothing()
        {
            var m = Make();
            m.Stun(1.5f);
            var o = m.Update(0.1f, See(Vector3.zero, new Vector3(1, 0, 0)));
            Assert.AreEqual(PursuitState.Stunned, o.State);
            Assert.IsFalse(o.Strike);
            for (int i = 0; i < 15; i++) o = m.Update(0.1f, See(Vector3.zero, new Vector3(1, 0, 0)));
            Assert.AreNotEqual(PursuitState.Stunned, o.State);
        }

        [Test]
        public void FetchesAWeaponFirst()
        {
            var o = Make().Update(0.1f, new PursuitInput { Self = Vector3.zero, HasWeapon = false, Weapon = new Vector3(3, 0, 3), Visible = new ChaseTarget { Id = 1, Position = new Vector3(5, 0, 0) } });
            Assert.AreEqual(PursuitState.FetchWeapon, o.State);
        }

        [Test]
        public void NothingSeen_NothingRemembered_GoesHome()
        {
            Assert.AreEqual(PursuitState.Return, Make().Update(0.1f, Blind(Vector3.zero)).State);
        }
    }

    public class PursuitAssignerTests
    {
        [Test]
        public void AtMostTwoOnTheSameRaccoon_NearestFirst()
        {
            var chasers = new List<PursuitAssigner.Chaser>
            {
                new PursuitAssigner.Chaser { Position = new Vector3(1, 0, 0), Sees = new List<ChaseTarget> { new ChaseTarget { Id = 7, Position = Vector3.zero } } },
                new PursuitAssigner.Chaser { Position = new Vector3(9, 0, 0), Sees = new List<ChaseTarget> { new ChaseTarget { Id = 7, Position = Vector3.zero } } },
                new PursuitAssigner.Chaser { Position = new Vector3(2, 0, 0), Sees = new List<ChaseTarget> { new ChaseTarget { Id = 7, Position = Vector3.zero } } },
            };
            var a = PursuitAssigner.Assign(chasers, maxPerTarget: 2);
            Assert.AreEqual(7, a[0].Value.Id);
            Assert.IsNull(a[1], "the far one doesn't pile on");
            Assert.AreEqual(7, a[2].Value.Id);
        }

        [Test]
        public void SpreadsOverSeveralRaccoons()
        {
            var both = new List<ChaseTarget> { new ChaseTarget { Id = 1, Position = Vector3.zero }, new ChaseTarget { Id = 2, Position = new Vector3(4, 0, 0) } };
            var chasers = new List<PursuitAssigner.Chaser>
            {
                new PursuitAssigner.Chaser { Position = new Vector3(0.5f, 0, 0), Sees = both },
                new PursuitAssigner.Chaser { Position = new Vector3(0.6f, 0, 0), Sees = both },
                new PursuitAssigner.Chaser { Position = new Vector3(0.7f, 0, 0), Sees = both },
            };
            var a = PursuitAssigner.Assign(chasers, 2);
            Assert.AreEqual(2, a[2].Value.Id, "the third goes after the other raccoon");
        }
    }

    public class RescueTests
    {
        [Test]
        public void CaughtPlayer_CanBeRescued_WhileTheRoundRuns()
        {
            var r = new RoundOutcome();
            r.Begin(new[] { 0, 1 }, 0f, 90f);
            r.MarkCaught(0);
            Assert.IsTrue(r.Rescue(0));
            Assert.AreEqual(PlayerOutcome.Running, r.StatusOf(0));
            Assert.AreEqual(0, r.Caught);
            Assert.IsFalse(r.Rescue(1), "only caught players can be rescued");
        }

        [Test]
        public void EveryoneCaught_EndsTheRound_NoRescue()
        {
            var r = new RoundOutcome();
            r.Begin(new[] { 0, 1 }, 0f, 90f);
            r.MarkCaught(0);
            r.MarkCaught(1);
            r.Tick(1f);
            Assert.IsTrue(r.IsOver);
            Assert.IsFalse(r.Rescue(0));
        }
    }
}

namespace TrashPandas.Tests
{
    public class RescueBookkeepingTests
    {
        [Test]
        public void Rescued_CanStillCashOutLater()
        {
            var pay = new RoundPayout { SharesAreSafe = true };
            pay.SetShares(new System.Collections.Generic.Dictionary<int, int> { [0] = 50 });
            pay.Caught(0);
            pay.Rescued(0);
            pay.Carry(0, 40, null);
            pay.Escaped(0);
            Assert.AreEqual(90, pay.Of(0));
        }

        [Test]
        public void JoiningMidRun_AddsARunner()
        {
            var r = new RoundOutcome();
            r.Begin(new[] { 0 }, 0f, 90f);
            Assert.IsTrue(r.Join(2));
            Assert.AreEqual(PlayerOutcome.Running, r.StatusOf(2));
            Assert.IsFalse(r.Join(2), "already in");
        }

        [Test]
        public void RescuedRaccoon_StartsWithNoHits()
        {
            var h = new HitTracker(hitsToCatch: 3, stunSeconds: 0f, invulnerableSeconds: 0f);
            h.TryHit(4, 0f); h.TryHit(4, 1f);
            h.Forget(4);
            Assert.AreEqual(0, h.Hits(4));
        }
    }
}

namespace TrashPandas.Tests
{
    public class ScreamTests
    {
        [Test]
        public void HearingAScream_SendsThemToLook_IfTheyAreNotChasingAlready()
        {
            var m = new PursuitMind(Vector3.zero);
            m.Hear(new Vector3(4, 0, 4));
            var o = m.Update(0.1f, new PursuitInput { Self = Vector3.zero, HasWeapon = true });
            Assert.AreEqual(PursuitState.Search, o.State);
            Assert.AreEqual(new Vector3(4, 0, 4), o.Destination);
        }
    }
}
