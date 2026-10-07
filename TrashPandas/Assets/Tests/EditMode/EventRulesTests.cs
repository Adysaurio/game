using NUnit.Framework;
using TrashPandas.Core.Events;
using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class EventTaskTrackerTests
    {
        static SocialEvent Ev(ArmsTask arms, LegsTask legs) => new SocialEvent
        {
            Speaker = "Test", Line = "?", Options = new[] { "a", "b", "c" },
            OptionKinds = new[] { HeadAnswer.Odd, HeadAnswer.Good, HeadAnswer.Absurd }, Arms = arms, Legs = legs,
        };

        [Test]
        public void FirstAnswer_Counts()
        {
            var t = new EventTaskTracker();
            t.Record(0.1f, default, answerKey: 2);
            t.Record(0.1f, default, answerKey: 3);
            Assert.AreEqual(HeadAnswer.Good, t.ToResponse(Ev(ArmsTask.None, LegsTask.None)).Answer);
        }

        [Test]
        public void NoAnswer_IsSilence()
        {
            var t = new EventTaskTracker();
            t.Record(1f, default, 0);
            Assert.AreEqual(HeadAnswer.None, t.ToResponse(Ev(ArmsTask.None, LegsTask.None)).Answer);
        }

        [Test]
        public void Handshake_NeedsHalfASecondOfHolding()
        {
            var t = new EventTaskTracker();
            var hold = new TaskInput { Primary = true };
            for (int i = 0; i < 4; i++) t.Record(0.1f, hold, 0);
            Assert.IsFalse(t.ToResponse(Ev(ArmsTask.Handshake, LegsTask.None)).ArmsDone);
            t.Record(0.1f, hold, 0);
            Assert.IsTrue(t.ToResponse(Ev(ArmsTask.Handshake, LegsTask.None)).ArmsDone);
        }

        [Test]
        public void HandsTogether_NeedsBothButtons()
        {
            var t = new EventTaskTracker();
            for (int i = 0; i < 10; i++) t.Record(0.1f, new TaskInput { Primary = true }, 0);
            Assert.IsFalse(t.ToResponse(Ev(ArmsTask.HandsTogether, LegsTask.None)).ArmsDone);
            for (int i = 0; i < 6; i++) t.Record(0.1f, new TaskInput { Primary = true, Secondary = true }, 0);
            Assert.IsTrue(t.ToResponse(Ev(ArmsTask.HandsTogether, LegsTask.None)).ArmsDone);
        }

        [Test]
        public void StayStill_FailsOnAnyRealMovement()
        {
            var t = new EventTaskTracker();
            t.Record(1f, new TaskInput { Move = new Vector2(0.1f, 0f) }, 0);
            Assert.IsTrue(t.ToResponse(Ev(ArmsTask.None, LegsTask.StayStill)).LegsDone, "a tiny nudge is fine");
            t.Record(0.1f, new TaskInput { Move = new Vector2(0f, 1f) }, 0);
            Assert.IsFalse(t.ToResponse(Ev(ArmsTask.None, LegsTask.StayStill)).LegsDone);
        }

        [Test]
        public void Kneel_IsCrouchHeldAtTheEnd()
        {
            var t = new EventTaskTracker();
            t.Record(0.5f, new TaskInput { Crouch = true }, 0);
            t.Record(0.5f, default, 0);
            Assert.IsFalse(t.ToResponse(Ev(ArmsTask.None, LegsTask.Kneel)).LegsDone);
            t.Record(0.5f, new TaskInput { Crouch = true }, 0);
            Assert.IsTrue(t.ToResponse(Ev(ArmsTask.None, LegsTask.Kneel)).LegsDone);
        }

        [Test]
        public void DanceStep_IsAJumpPressDuringTheWindow()
        {
            var t = new EventTaskTracker();
            t.Record(1f, default, 0);
            Assert.IsFalse(t.ToResponse(Ev(ArmsTask.None, LegsTask.DanceStep)).LegsDone);
            t.Record(0.1f, new TaskInput { Jump = true }, 0);
            Assert.IsTrue(t.ToResponse(Ev(ArmsTask.None, LegsTask.DanceStep)).LegsDone);
        }
    }

    public class EventResolverTests
    {
        static readonly SocialEvent Champagne = new SocialEvent
        {
            Speaker = "Waiter", Line = "Champagne, sir?", Options = new[] { "x", "y", "z" },
            OptionKinds = new[] { HeadAnswer.Good, HeadAnswer.Odd, HeadAnswer.Absurd },
            Arms = ArmsTask.TakeGlass, Legs = LegsTask.StayStill,
        };

        static EventResponse Perfect => new EventResponse { Answer = HeadAnswer.Good, ArmsDone = true, LegsDone = true };

        [Test]
        public void PerfectTeam_LowersSuspicion()
        {
            var r = EventResolver.Resolve(Champagne, BodyPart.All, _ => Perfect);
            Assert.AreEqual(EventScoring.GoodAnswer, r.Delta, 1e-3f);
            Assert.Less(r.Delta, 0f);
        }

        [Test]
        public void EachFailure_AddsUp()
        {
            var bad = new EventResponse { Answer = HeadAnswer.Absurd, ArmsDone = false, LegsDone = false };
            var r = EventResolver.Resolve(Champagne, BodyPart.All, _ => bad);
            Assert.AreEqual(EventScoring.BadAnswer + 2 * EventScoring.FailedAction, r.Delta, 1e-3f);
            Assert.AreEqual(3, r.Parts.Count);
        }

        [Test]
        public void EmptySeat_FailsEveryPartItCovered()
        {
            // The arms player was out stealing: both arms missing.
            var r = EventResolver.Resolve(Champagne, BodyPart.All & ~BodyPart.Arms, _ => Perfect);
            Assert.AreEqual(EventScoring.GoodAnswer + EventScoring.MissingPart, r.Delta, 1e-3f);
            Assert.IsTrue(r.Parts.Exists(p => p.Role == EventRole.Arms && p.Outcome == PartOutcome.Missing));
        }

        [Test]
        public void SilentHead_CountsAsBad()
        {
            var r = EventResolver.Resolve(Champagne, BodyPart.All, role => role == EventRole.Head ? new EventResponse() : Perfect);
            Assert.AreEqual(EventScoring.BadAnswer, r.Delta, 1e-3f);
        }

        [Test]
        public void NoResponseAtAll_FromAPresentPlayer_IsAFailNotACrash()
        {
            var r = EventResolver.Resolve(Champagne, BodyPart.All, _ => null);
            Assert.AreEqual(EventScoring.BadAnswer + 2 * EventScoring.FailedAction, r.Delta, 1e-3f);
        }

        [Test]
        public void EmptySeat_CostsEvenWhenThatRoleHasNoTask()
        {
            var e = new SocialEvent { Options = new[] { "a", "b", "c" }, OptionKinds = new[] { HeadAnswer.Good, HeadAnswer.Odd, HeadAnswer.Absurd }, Arms = ArmsTask.Handshake, Legs = LegsTask.None };
            var r = EventResolver.Resolve(e, BodyPart.All & ~BodyPart.Legs, _ => Perfect);
            Assert.AreEqual(EventScoring.GoodAnswer + EventScoring.MissingPart, r.Delta, 1e-3f, "spec §6b: an empty seat always counts");
        }

        [Test]
        public void RoleWithoutTask_PresentCostsNothing()
        {
            var e = new SocialEvent { Options = new[] { "a", "b", "c" }, OptionKinds = new[] { HeadAnswer.Good, HeadAnswer.Odd, HeadAnswer.Absurd }, Arms = ArmsTask.Handshake, Legs = LegsTask.None };
            var r = EventResolver.Resolve(e, BodyPart.All, _ => Perfect);
            Assert.AreEqual(EventScoring.GoodAnswer, r.Delta, 1e-3f);
        }

        [Test]
        public void SpeakerPick_NeverSamePersonTwiceInARow_WithRealCatalog()
        {
            var scheduler = new EventScheduler(3);
            var available = new System.Collections.Generic.HashSet<string> { "MotherInLaw", "Waiter", "Priest", "Bride" };
            string last = null;
            for (int i = 0; i < 300; i++)
            {
                int index = EventPicker.Pick(SocialEventCatalog.All, available, scheduler, new System.Random(i));
                string speaker = SocialEventCatalog.All[index].Speaker;
                Assert.AreNotEqual(last, speaker, $"pick {i}");
                last = speaker;
            }
        }

        [Test]
        public void SpeakerPick_OnlyFromSpeakersInTheScene()
        {
            var available = new System.Collections.Generic.HashSet<string> { "Priest" };
            int index = EventPicker.Pick(SocialEventCatalog.All, available, new EventScheduler(1), new System.Random(1));
            Assert.AreEqual("Priest", SocialEventCatalog.All[index].Speaker);
            Assert.AreEqual(-1, EventPicker.Pick(SocialEventCatalog.All, new System.Collections.Generic.HashSet<string>(), new EventScheduler(1), new System.Random(1)));
        }

        [Test]
        public void DuringConversation_ClicksDontGrab()
        {
            var input = new SlotInput { GrabOne = true, GrabBoth = true, Move = Vector2.up, Crouch = true };
            var filtered = ConversationInput.Filter(input, inConversation: true);
            Assert.IsFalse(filtered.GrabOne || filtered.GrabBoth, "the handshake click must not grab or throw things");
            Assert.AreEqual(Vector2.up, filtered.Move, "legs still move (and can fail 'don't move')");
            Assert.IsTrue(filtered.Crouch, "kneeling still works");
            Assert.IsTrue(ConversationInput.Filter(input, inConversation: false).GrabOne);
        }

        [Test]
        public void Catalog_HasWellFormedEvents()
        {
            Assert.GreaterOrEqual(SocialEventCatalog.All.Count, 4);
            foreach (var e in SocialEventCatalog.All)
            {
                Assert.AreEqual(3, e.Options.Length, e.Speaker);
                Assert.AreEqual(3, e.OptionKinds.Length, e.Speaker);
                CollectionAssert.Contains(e.OptionKinds, HeadAnswer.Good, e.Speaker);
                Assert.IsFalse(string.IsNullOrEmpty(e.Line));
            }
        }
    }
}
