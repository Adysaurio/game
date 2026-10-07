using System.Collections.Generic;
using UnityEngine;

namespace TrashPandas.Core.Trenchcoat
{
    /// <summary>Distributes each occupant's slot input to the body parts their slot controls.</summary>
    public static class SlotInputRouter
    {
        public const float ArmSpread = 0.25f;

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
            var leg = new LegInput
            {
                Drive = input.Move.y,
                Steer = input.Move.x,
                JumpPressedAt = input.JumpPressedAt,
                Crouch = input.Crouch,
            };
            if ((slotParts & BodyPart.LegLeft) != 0) parts.LegLeft = leg;
            if ((slotParts & BodyPart.LegRight) != 0) parts.LegRight = leg;

            bool bothArms = (slotParts & BodyPart.Arms) == BodyPart.Arms;
            if (bothArms)
            {
                parts.ArmLeft = new ArmInput { HandTarget = input.HandTarget + new Vector3(-ArmSpread, 0f, 0f), Grab = input.PrimaryGrab };
                parts.ArmRight = new ArmInput { HandTarget = input.HandTarget + new Vector3(ArmSpread, 0f, 0f), Grab = input.SecondaryGrab };
            }
            else if ((slotParts & BodyPart.ArmLeft) != 0)
            {
                parts.ArmLeft = new ArmInput { HandTarget = input.HandTarget, Grab = input.PrimaryGrab };
            }
            else if ((slotParts & BodyPart.ArmRight) != 0)
            {
                parts.ArmRight = new ArmInput { HandTarget = input.HandTarget, Grab = input.PrimaryGrab };
            }

            if ((slotParts & BodyPart.Head) != 0)
                parts.Head = new HeadInput { Yaw = input.Look.x, Pitch = input.Look.y };
        }
    }
}
