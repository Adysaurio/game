using System;

namespace TrashPandas.Core.Trenchcoat
{
    [Flags]
    public enum BodyPart
    {
        None = 0,
        LegLeft = 1 << 0,
        LegRight = 1 << 1,
        ArmLeft = 1 << 2,
        ArmRight = 1 << 3,
        Head = 1 << 4,
        Legs = LegLeft | LegRight,
        Arms = ArmLeft | ArmRight,
        All = Legs | Arms | Head,
    }
}
