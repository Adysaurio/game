using System;

namespace TrashPandas.Core.Events
{
    /// <summary>What this particular conversation asks of the body, decided when it starts.</summary>
    public struct RolledTasks
    {
        public ArmsTask Arms;
        public LegsTask Legs;
        /// <summary>Which of the six orders the three answers are shown in (0..5).</summary>
        public byte Order;
    }

    /// <summary>
    /// Social events are a surprise: who's coming is announced, but what the arms and legs must do (and the
    /// order of the answers) is rolled only when the conversation starts.
    /// </summary>
    public static class EventRoll
    {
        static readonly int[][] Orders =
        {
            new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
            new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 },
        };

        public static RolledTasks Roll(Random random)
        {
            var arms = (ArmsTask)random.Next(4);
            var legs = (LegsTask)random.Next(4);
            if (arms == ArmsTask.None && legs == LegsTask.None) legs = (LegsTask)(1 + random.Next(3));
            return new RolledTasks { Arms = arms, Legs = legs, Order = (byte)random.Next(Orders.Length) };
        }

        public static int[] DecodeOrder(byte order) => (int[])Orders[order < Orders.Length ? order : 0].Clone();

        public static byte EncodeOrder(int[] order)
        {
            for (int i = 0; i < Orders.Length; i++)
                if (Orders[i][0] == order[0] && Orders[i][1] == order[1] && Orders[i][2] == order[2]) return (byte)i;
            return 0;
        }

        /// <summary>A copy of the catalog event with the rolled tasks and the answers in the rolled order.</summary>
        public static SocialEvent Apply(SocialEvent template, RolledTasks roll)
        {
            int[] order = DecodeOrder(roll.Order);
            var options = new string[3];
            var kinds = new HeadAnswer[3];
            for (int i = 0; i < 3; i++)
            {
                options[i] = template.Options[order[i]];
                kinds[i] = template.OptionKinds[order[i]];
            }
            return new SocialEvent
            {
                Speaker = template.Speaker, SpeakerName = template.SpeakerName, Line = template.Line,
                Options = options, OptionKinds = kinds, Arms = roll.Arms, Legs = roll.Legs,
            };
        }
    }
}
