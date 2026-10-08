using System.Collections.Generic;

namespace TrashPandas.Core.Loot
{
    public enum LootKind : byte { Wallet, Phone, Cutlery, Bottle, Food, Objective, GiantGift }

    public enum ObjectiveId : byte { Ring, CakeTopper, Envelope, Champagne, Bouquet, Keys }

    public sealed class ObjectiveInfo
    {
        public ObjectiveId Id;
        public string Name;
        public int Value;
    }

    /// <summary>What everything at the wedding is worth (spec §16.2–16.3).</summary>
    public static class LootCatalog
    {
        public static int ValueOf(LootKind kind) => kind switch
        {
            LootKind.Wallet => 40,
            LootKind.Phone => 60,
            LootKind.Cutlery => 15,
            LootKind.Bottle => 30,
            LootKind.Food => 5,
            LootKind.GiantGift => 150,
            _ => 0,
        };

        public static readonly IReadOnlyList<ObjectiveInfo> Objectives = new[]
        {
            new ObjectiveInfo { Id = ObjectiveId.Ring, Name = "The bride's ring", Value = 300 },
            new ObjectiveInfo { Id = ObjectiveId.CakeTopper, Name = "The cake topper", Value = 250 },
            new ObjectiveInfo { Id = ObjectiveId.Envelope, Name = "The fattest envelope", Value = 200 },
            new ObjectiveInfo { Id = ObjectiveId.Champagne, Name = "The fancy champagne", Value = 150 },
            new ObjectiveInfo { Id = ObjectiveId.Bouquet, Name = "The bouquet", Value = 120 },
            new ObjectiveInfo { Id = ObjectiveId.Keys, Name = "The van keys", Value = 100 },
        };

        public static ObjectiveInfo Objective(ObjectiveId id) => Objectives[(int)id];
    }
}
