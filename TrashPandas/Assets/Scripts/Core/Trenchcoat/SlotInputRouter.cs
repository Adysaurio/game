using System.Collections.Generic;

namespace TrashPandas.Core.Trenchcoat
{
    /// <summary>Distributes each occupant's slot input to the body parts their slot controls.</summary>
    public static class SlotInputRouter
    {
        public static PartInputs Route(SlotSystem slots, IReadOnlyDictionary<int, SlotInput> inputsByPlayer)
        {
            var parts = PartInputs.Idle;
            for (int i = 0; i < slots.SlotCount; i++)
            {
                var occupant = slots.OccupantOf(i);
                if (!occupant.HasValue || !inputsByPlayer.TryGetValue(occupant.Value, out var input))
                    continue;
                Apply(slots.PartsOf(i), input, ref parts);
            }
            return parts;
        }

        static void Apply(BodyPart slotParts, SlotInput input, ref PartInputs parts)
        {
            var leg = new LegInput { Move = input.Move, JumpPressedAt = input.JumpPressedAt, Crouch = input.Crouch };
            if ((slotParts & BodyPart.LegLeft) != 0) parts.LegLeft = leg;
            if ((slotParts & BodyPart.LegRight) != 0) parts.LegRight = leg;

            bool bothArms = (slotParts & BodyPart.Arms) == BodyPart.Arms;
            if (bothArms)
            {
                parts.ArmLeft = new ArmInput { Aim = input.Aim, Reach = input.PrimaryReach };
                parts.ArmRight = new ArmInput { Aim = input.Aim, Reach = input.SecondaryReach };
            }
            else if ((slotParts & BodyPart.ArmLeft) != 0)
            {
                parts.ArmLeft = new ArmInput { Aim = input.Aim, Reach = input.PrimaryReach };
            }
            else if ((slotParts & BodyPart.ArmRight) != 0)
            {
                parts.ArmRight = new ArmInput { Aim = input.Aim, Reach = input.PrimaryReach };
            }

            if ((slotParts & BodyPart.Head) != 0)
                parts.Head = new HeadInput { Aim = input.Aim };
        }
    }
}
