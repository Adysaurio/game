using System.Collections.Generic;

namespace TrashPandas.Core.Events
{
    public enum HeadAnswer : byte { None, Good, Odd, Absurd }
    public enum ArmsTask : byte { None, Handshake, TakeGlass, HandsTogether }
    public enum LegsTask : byte { None, StayStill, Kneel, DanceStep }

    /// <summary>One social event: who comes over, what they say, the head's options, and what arms/legs must do.</summary>
    public sealed class SocialEvent
    {
        /// <summary>NPC id in the scene: "MotherInLaw", "Waiter", "Priest", "Bride".</summary>
        public string Speaker;
        public string SpeakerName;
        public string Line;
        public string[] Options;
        public HeadAnswer[] OptionKinds;
        public ArmsTask Arms;
        public LegsTask Legs;

        public HeadAnswer KindOf(int optionIndex) =>
            optionIndex >= 0 && optionIndex < OptionKinds.Length ? OptionKinds[optionIndex] : HeadAnswer.None;
    }

    public static class SocialEventCatalog
    {
        public static readonly IReadOnlyList<SocialEvent> All = new[]
        {
            new SocialEvent
            {
                Speaker = "MotherInLaw", SpeakerName = "The mother-in-law",
                Line = "Oh, dear! And which side of the family are YOU from?",
                Options = new[] { "The... good side.", "The groom's side, ma'am.", "The dumpster side." },
                OptionKinds = new[] { HeadAnswer.Odd, HeadAnswer.Good, HeadAnswer.Absurd },
                Arms = ArmsTask.Handshake, Legs = LegsTask.StayStill,
            },
            new SocialEvent
            {
                Speaker = "Waiter", SpeakerName = "The waiter",
                Line = "Champagne, sir?",
                Options = new[] { "Why, thank you.", "Got any garbage?", "Mmm... bubbly water." },
                OptionKinds = new[] { HeadAnswer.Good, HeadAnswer.Absurd, HeadAnswer.Odd },
                Arms = ArmsTask.TakeGlass, Legs = LegsTask.StayStill,
            },
            new SocialEvent
            {
                Speaker = "Priest", SpeakerName = "The priest",
                Line = "A blessed day, my son. Shall we say a little prayer?",
                Options = new[] { "Is there cake after?", "Sure thing, padre.", "Amen." },
                OptionKinds = new[] { HeadAnswer.Absurd, HeadAnswer.Odd, HeadAnswer.Good },
                Arms = ArmsTask.HandsTogether, Legs = LegsTask.Kneel,
            },
            new SocialEvent
            {
                Speaker = "Bride", SpeakerName = "The bride",
                Line = "You! Mysterious stranger! Dance with me!",
                Options = new[] { "It would be an honor.", "*hisses*", "I only dance in trash cans." },
                OptionKinds = new[] { HeadAnswer.Good, HeadAnswer.Absurd, HeadAnswer.Odd },
                Arms = ArmsTask.Handshake, Legs = LegsTask.DanceStep,
            },
            new SocialEvent
            {
                Speaker = "MotherInLaw", SpeakerName = "The mother-in-law",
                Line = "You smell... unusual. Is that a new cologne?",
                Options = new[] { "Eau de Alley.", "It's called 'Musk'.", "Yes, from Paris." },
                OptionKinds = new[] { HeadAnswer.Absurd, HeadAnswer.Odd, HeadAnswer.Good },
                Arms = ArmsTask.None, Legs = LegsTask.StayStill,
            },
            new SocialEvent
            {
                Speaker = "Waiter", SpeakerName = "The waiter",
                Line = "Shrimp canapé? Careful, the cat's been eyeing them.",
                Options = new[] { "Just one, thanks.", "I'll take the whole tray.", "The cat can't have them!" },
                OptionKinds = new[] { HeadAnswer.Good, HeadAnswer.Odd, HeadAnswer.Absurd },
                Arms = ArmsTask.TakeGlass, Legs = LegsTask.None,
            },
        };
    }
}
