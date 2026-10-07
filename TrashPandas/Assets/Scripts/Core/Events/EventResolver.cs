using System;
using System.Collections.Generic;
using TrashPandas.Core.Trenchcoat;

namespace TrashPandas.Core.Events
{
    public enum EventRole : byte { Head, Arms, Legs }
    public enum PartOutcome : byte { Good, Odd, Failed, Missing }

    public static class EventScoring
    {
        public const float GoodAnswer = -8f, OddAnswer = 5f, BadAnswer = 25f, FailedAction = 20f, MissingPart = 25f;
    }

    public struct PartResult
    {
        public EventRole Role;
        public PartOutcome Outcome;
        public float Delta;
    }

    public sealed class EventResult
    {
        public float Delta;
        public readonly List<PartResult> Parts = new List<PartResult>();
    }

    /// <summary>Scores a social event: each role's part, with empty seats failing the parts they covered.</summary>
    public static class EventResolver
    {
        public static EventResult Resolve(SocialEvent e, BodyPart present, Func<EventRole, EventResponse?> responseFor)
        {
            var result = new EventResult();

            if ((present & BodyPart.Head) == 0) Add(result, EventRole.Head, PartOutcome.Missing, EventScoring.MissingPart);
            else
            {
                var answer = responseFor(EventRole.Head)?.Answer ?? HeadAnswer.None;
                if (answer == HeadAnswer.Good) Add(result, EventRole.Head, PartOutcome.Good, EventScoring.GoodAnswer);
                else if (answer == HeadAnswer.Odd) Add(result, EventRole.Head, PartOutcome.Odd, EventScoring.OddAnswer);
                else Add(result, EventRole.Head, PartOutcome.Failed, EventScoring.BadAnswer);
            }

            if (e.Arms != ArmsTask.None) Judge(result, EventRole.Arms, (present & BodyPart.Arms) == BodyPart.Arms, responseFor(EventRole.Arms)?.ArmsDone ?? false);
            if (e.Legs != LegsTask.None) Judge(result, EventRole.Legs, (present & BodyPart.Legs) == BodyPart.Legs, responseFor(EventRole.Legs)?.LegsDone ?? false);
            return result;
        }

        static void Judge(EventResult r, EventRole role, bool present, bool done)
        {
            if (!present) Add(r, role, PartOutcome.Missing, EventScoring.MissingPart);
            else if (done) Add(r, role, PartOutcome.Good, 0f);
            else Add(r, role, PartOutcome.Failed, EventScoring.FailedAction);
        }

        static void Add(EventResult r, EventRole role, PartOutcome outcome, float delta)
        {
            r.Parts.Add(new PartResult { Role = role, Outcome = outcome, Delta = delta });
            r.Delta += delta;
        }
    }
}
