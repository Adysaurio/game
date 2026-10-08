using System.Collections.Generic;
using TrashPandas.Core.Loot;
using TrashPandas.Runtime.Loot;
using TrashPandas.Runtime.Raccoon;
using Unity.Netcode;
using UnityEngine;

namespace TrashPandas.EditorTools
{
    /// <summary>
    /// Stage 3c-1: the wedding grows into a small estate (spec §16): entrance arch (S), the garden (center),
    /// the catering kitchen (W), the house without a roof (N), the parking lot (E) and the orchard (SE),
    /// plus where loot and the objectives can appear.
    /// </summary>
    public static partial class GreyboxSceneBuilder
    {
        // Map bounds: x -32..32, z -27..31. The garden center (and the coat's start) stays where it was.
        public static readonly Vector3 GardenCenter = new Vector3(0f, 0f, 6f);
        public static readonly Vector3 ArchCenter = new Vector3(0f, 0f, -23f);

        static readonly List<Vector3> s_lootSpots = new List<Vector3>();
        static readonly List<ObjectiveId> s_objectiveIds = new List<ObjectiveId>();
        static readonly List<Vector3> s_objectivePositions = new List<Vector3>();

        static void Spot(Vector3 p) => s_lootSpots.Add(p);
        static void ObjectiveSpot(ObjectiveId id, Vector3 p) { s_objectiveIds.Add(id); s_objectivePositions.Add(p); }

        static void BuildEstate(Material wood, Material hedge)
        {
            var wall = Mat("HouseWall", new Color(0.93f, 0.88f, 0.8f));
            var floor = Mat("HouseFloor", new Color(0.7f, 0.55f, 0.4f));
            var tent = Mat("Tent", new Color(0.95f, 0.95f, 0.98f));
            var steel = Mat("Counter", new Color(0.75f, 0.77f, 0.8f));
            var asphalt = Mat("Asphalt", new Color(0.35f, 0.35f, 0.38f));
            var carMat = Mat("Car", new Color(0.55f, 0.15f, 0.15f));
            var van = Mat("Van", new Color(0.92f, 0.92f, 0.95f));
            var trunk = Mat("Trunk", new Color(0.45f, 0.32f, 0.2f));
            var leaves = Mat("Leaves", new Color(0.3f, 0.55f, 0.25f));
            var flowers = Mat("Arch", new Color(0.98f, 0.8f, 0.85f));
            var grate = Mat("Grate", new Color(0.2f, 0.2f, 0.22f));
            var cloth = Mat("Tablecloth", new Color(0.98f, 0.98f, 0.96f));

            // --- Perimeter: hedges all around. South side: the entrance arch and the orchard's raccoon gap.
            Box("Perimeter_W", new Vector3(-32f, 1f, 2f), new Vector3(1f, 2f, 58f), hedge);
            Box("Perimeter_E", new Vector3(32f, 1f, 2f), new Vector3(1f, 2f, 58f), hedge);
            Box("Perimeter_N", new Vector3(0f, 1f, 31f), new Vector3(64f, 2f, 1f), hedge);
            Box("Perimeter_S_West", new Vector3(-16.8f, 1f, -24f), new Vector3(30.4f, 2f, 1f), hedge);
            Box("Perimeter_S_Mid", new Vector3(10.5f, 1f, -24f), new Vector3(18f, 2f, 1f), hedge);
            Box("Perimeter_S_GapTop", new Vector3(20f, 1.25f, -24f), new Vector3(1f, 1.5f, 1f), hedge);
            Box("Perimeter_S_East", new Vector3(26.25f, 1f, -24f), new Vector3(11.5f, 2f, 1f), hedge);

            // --- Entrance (S): the flower arch. Walking out through it with loot = clean exit.
            Box("Arch_PillarL", new Vector3(-1.6f, 1.4f, -24f), new Vector3(0.3f, 2.8f, 0.6f), flowers);
            Box("Arch_PillarR", new Vector3(1.6f, 1.4f, -24f), new Vector3(0.3f, 2.8f, 0.6f), flowers);
            Box("Arch_Top", new Vector3(0f, 2.95f, -24f), new Vector3(3.5f, 0.3f, 0.6f), flowers);
            Box("Gate_Back", new Vector3(0f, 1f, -26f), new Vector3(5f, 2f, 0.5f), hedge); // nothing beyond the arch
            Box("Gate_SideL", new Vector3(-2.5f, 1f, -25f), new Vector3(0.5f, 2f, 2f), hedge);
            Box("Gate_SideR", new Vector3(2.5f, 1f, -25f), new Vector3(0.5f, 2f, 2f), hedge);

            // --- Garden extras: the head table (bride), the bar, a bench, lawn spots only raccoons reach.
            Table("HeadTable", new Vector3(0f, 0f, 13f), new Vector2(4f, 1.2f), 0.85f, cloth);
            Spot(new Vector3(-1.6f, 0.88f, 12.6f));
            Spot(new Vector3(1.6f, 0.88f, 12.6f));
            ObjectiveSpot(ObjectiveId.Ring, new Vector3(-0.4f, 0.88f, 12.6f));
            ObjectiveSpot(ObjectiveId.Bouquet, new Vector3(0.6f, 0.9f, 12.6f));
            Box("Bar", new Vector3(10f, 0.45f, 6f), new Vector3(0.9f, 0.9f, 3f), wood);
            Spot(new Vector3(10f, 0.93f, 5f));
            Spot(new Vector3(10f, 0.93f, 7.2f));
            ObjectiveSpot(ObjectiveId.Champagne, new Vector3(10f, 1.0f, 6.1f));
            Table("Bench_Garden", new Vector3(-12f, 0f, -4f), new Vector2(1.6f, 0.45f), 0.45f, wood);
            ObjectiveSpot(ObjectiveId.Bouquet, new Vector3(-12f, 0.52f, -4f));
            Spot(new Vector3(-12.5f, 0.48f, -4f));
            Spot(new Vector3(-4f, 0.05f, -3f));
            Spot(new Vector3(5f, 0.05f, -5f));
            Spot(new Vector3(-9f, 0.05f, 13f));
            // The cake topper sits on the cake (jump + reach).
            ObjectiveSpot(ObjectiveId.CakeTopper, new Vector3(0f, 1.6f, 8.6f));

            // --- Kitchen (W): the catering tent, open toward the garden, back door on the far side.
            Box("Tent_N", new Vector3(-19f, 1.4f, 14f), new Vector3(10f, 2.8f, 0.2f), tent);
            Box("Tent_S", new Vector3(-19f, 1.4f, 2f), new Vector3(10f, 2.8f, 0.2f), tent);
            Box("Tent_W_South", new Vector3(-24f, 1.4f, 4.75f), new Vector3(0.2f, 2.8f, 5.5f), tent);   // z 2..7.5
            Box("Tent_W_North", new Vector3(-24f, 1.4f, 11.5f), new Vector3(0.2f, 2.8f, 5f), tent);    // z 9..14 (door 7.5..9)
            Box("Tent_Post_SE", new Vector3(-14f, 1.4f, 2f), new Vector3(0.2f, 2.8f, 0.2f), tent);
            Box("Tent_Post_NE", new Vector3(-14f, 1.4f, 14f), new Vector3(0.2f, 2.8f, 0.2f), tent);
            Box("Kitchen_Floor", new Vector3(-19f, 0.005f, 8f), new Vector3(10f, 0.01f, 12f), steel).GetComponent<Collider>().enabled = false;
            Box("Counter_North", new Vector3(-19f, 0.45f, 13f), new Vector3(6f, 0.9f, 0.8f), steel);
            Spot(new Vector3(-21f, 0.93f, 12.75f));
            Spot(new Vector3(-19f, 0.93f, 12.75f));
            Spot(new Vector3(-17f, 0.93f, 12.75f));
            ObjectiveSpot(ObjectiveId.Keys, new Vector3(-21.8f, 0.93f, 12.75f));
            Box("Island", new Vector3(-19f, 0.45f, 7f), new Vector3(3f, 0.9f, 1f), steel);
            Spot(new Vector3(-20.2f, 0.93f, 6.8f));
            Spot(new Vector3(-17.8f, 0.93f, 7.2f));
            ObjectiveSpot(ObjectiveId.Champagne, new Vector3(-19f, 1.0f, 7.1f));
            Visual(PrimitiveType.Cylinder, "SewerGrate", null, new Vector3(-20f, 0.01f, -4f), new Vector3(1.2f, 0.01f, 1.2f), grate);

            // --- House (N): no roof (greybox), front door S, living room W with the gift table, hall + bathroom E.
            Box("House_Floor", new Vector3(0f, 0.005f, 23f), new Vector3(20f, 0.01f, 10f), floor).GetComponent<Collider>().enabled = false;
            Box("House_S_West", new Vector3(-5.6f, 1.5f, 18f), new Vector3(8.8f, 3f, 0.3f), wall);   // x -10..-1.2
            Box("House_S_East", new Vector3(5.6f, 1.5f, 18f), new Vector3(8.8f, 3f, 0.3f), wall);    // x 1.2..10
            Box("House_W", new Vector3(-10f, 1.5f, 23f), new Vector3(0.3f, 3f, 10f), wall);
            Box("House_E", new Vector3(10f, 1.5f, 23f), new Vector3(0.3f, 3f, 10f), wall);
            Box("House_N_West", new Vector3(-1.75f, 1.5f, 28f), new Vector3(16.5f, 3f, 0.3f), wall); // x -10..6.5
            Box("House_N_East", new Vector3(8.75f, 1.5f, 28f), new Vector3(2.5f, 3f, 0.3f), wall);   // x 7.5..10
            Box("Window_Sill", new Vector3(7f, 0.225f, 28f), new Vector3(1f, 0.45f, 0.3f), wall);     // window 0.45..1.3
            Box("Window_Lintel", new Vector3(7f, 2.15f, 28f), new Vector3(1f, 1.7f, 0.3f), wall);
            Box("Inner_X4_South", new Vector3(4f, 1.5f, 19f), new Vector3(0.3f, 3f, 2f), wall);      // z 18..20 (door 20..21.6)
            Box("Inner_X4_North", new Vector3(4f, 1.5f, 24.8f), new Vector3(0.3f, 3f, 6.4f), wall);  // z 21.6..28
            Box("Bath_Wall_West", new Vector3(4.5f, 1.5f, 23f), new Vector3(1f, 3f, 0.3f), wall);    // x 4..5 (door 5..6.4)
            Box("Bath_Wall_East", new Vector3(8.2f, 1.5f, 23f), new Vector3(3.6f, 3f, 0.3f), wall);  // x 6.4..10
            Table("GiftTable", new Vector3(-6f, 0f, 25f), new Vector2(3f, 1f), 0.85f, cloth);
            Spot(new Vector3(-7.2f, 0.88f, 24.6f));
            Spot(new Vector3(-4.8f, 0.88f, 24.6f));
            ObjectiveSpot(ObjectiveId.Envelope, new Vector3(-6f, 0.9f, 24.7f));
            Box("Sideboard", new Vector3(-1f, 0.425f, 27.4f), new Vector3(2f, 0.85f, 0.5f), wood);
            Spot(new Vector3(-1.6f, 0.88f, 27.25f));
            Spot(new Vector3(-0.4f, 0.88f, 27.25f));
            ObjectiveSpot(ObjectiveId.Envelope, new Vector3(-1f, 0.9f, 27.25f));
            Box("CoffeeTable", new Vector3(-6f, 0.25f, 21f), new Vector3(1f, 0.5f, 1f), wood);
            Spot(new Vector3(-6f, 0.53f, 21f));
            Box("Sink", new Vector3(8.5f, 0.45f, 27.4f), new Vector3(1.2f, 0.9f, 0.6f), steel);
            Spot(new Vector3(8.2f, 0.93f, 27.3f));
            ObjectiveSpot(ObjectiveId.Ring, new Vector3(8.8f, 0.93f, 27.3f));

            // --- Parking (E): asphalt, cars, the catering van (exit at its side door).
            Box("Parking_Floor", new Vector3(23f, 0.005f, 1f), new Vector3(16f, 0.01f, 26f), asphalt).GetComponent<Collider>().enabled = false;
            Box("CateringVan", new Vector3(26f, 1.1f, 2f), new Vector3(2.4f, 2.2f, 4.6f), van);
            foreach (var (name, pos) in new[] { ("Car_0", new Vector3(21f, 0.7f, -6f)), ("Car_1", new Vector3(21f, 0.7f, 9f)), ("Car_2", new Vector3(28.5f, 0.7f, 9f)) })
            {
                Box(name, pos, new Vector3(1.8f, 1.4f, 4f), carMat);
                Spot(pos + new Vector3(0f, 0.73f, 1.2f));
            }
            ObjectiveSpot(ObjectiveId.Keys, new Vector3(21f, 1.43f, 8f));

            // --- Orchard (SE): trees, benches, a picnic only raccoons can raid.
            for (int i = 0; i < 8; i++)
            {
                var t = new Vector3(14f + (i % 4) * 4.5f, 0f, -21f + (i / 4) * 6f);
                Box($"Tree_{i}_Trunk", t + new Vector3(0f, 1f, 0f), new Vector3(0.3f, 2f, 0.3f), trunk);
                Visual(PrimitiveType.Sphere, $"Tree_{i}_Leaves", null, t + new Vector3(0f, 2.6f, 0f), new Vector3(2f, 1.6f, 2f), leaves);
            }
            Table("Bench_Orchard_0", new Vector3(16f, 0f, -16f), new Vector2(1.6f, 0.45f), 0.45f, wood);
            Table("Bench_Orchard_1", new Vector3(25f, 0f, -14f), new Vector2(1.6f, 0.45f), 0.45f, wood);
            Spot(new Vector3(16.4f, 0.48f, -16f));
            Spot(new Vector3(25.4f, 0.48f, -14f));
            Spot(new Vector3(19f, 0.05f, -19f));
            Spot(new Vector3(27f, 0.05f, -20f));
        }

        /// <summary>A plain table (no legs to trip on, the coat can't go under it).</summary>
        static void Table(string name, Vector3 at, Vector2 size, float height, Material mat)
        {
            Box(name, at + new Vector3(0f, height / 2f, 0f), new Vector3(size.x, height, size.y), mat);
        }

        /// <summary>The 27 loose loot items plus the 6 objectives (only 3 are used each round).</summary>
        static void BuildLoot()
        {
            var wallet = Mat("Wallet", new Color(0.35f, 0.2f, 0.12f));
            var phone = Mat("Phone", new Color(0.08f, 0.08f, 0.1f));
            var silver = Mat("Cutlery", new Color(0.85f, 0.86f, 0.9f));
            var bottle = Mat("Bottle", new Color(0.2f, 0.45f, 0.25f));
            var food = Mat("Food", new Color(0.95f, 0.6f, 0.25f));
            var gold = Mat("Objective", new Color(1f, 0.8f, 0.15f));

            int spot = 0;
            Vector3 NextSpot() => s_lootSpots.Count == 0 ? Vector3.up : s_lootSpots[spot++ % s_lootSpots.Count];
            void Loose(LootKind kind, int count, PrimitiveType shape, Vector3 scale, Material mat, float mass)
            {
                for (int i = 0; i < count; i++)
                    Prop($"Loot_{kind}_{i}", shape, NextSpot(), scale, mat, mass, false).AddComponent<LootItem>().Kind = kind;
            }
            Loose(LootKind.Wallet, 6, PrimitiveType.Cube, new Vector3(0.2f, 0.05f, 0.12f), wallet, 0.3f);
            Loose(LootKind.Phone, 5, PrimitiveType.Cube, new Vector3(0.08f, 0.015f, 0.16f), phone, 0.2f);
            Loose(LootKind.Cutlery, 6, PrimitiveType.Cube, new Vector3(0.22f, 0.015f, 0.04f), silver, 0.15f);
            Loose(LootKind.Bottle, 5, PrimitiveType.Cylinder, new Vector3(0.08f, 0.14f, 0.08f), bottle, 0.6f);
            Loose(LootKind.Food, 5, PrimitiveType.Sphere, new Vector3(0.12f, 0.12f, 0.12f), food, 0.2f);

            void Objective(ObjectiveId id, PrimitiveType shape, Vector3 scale)
            {
                int i = s_objectiveIds.IndexOf(id);
                var item = Prop($"Objective_{id}", shape, i >= 0 ? s_objectivePositions[i] : Vector3.up, scale, gold, 0.3f, false).AddComponent<LootItem>();
                item.Kind = LootKind.Objective;
                item.Objective = id;
            }
            // Heavy loot: needs two raccoons (concept v2). Not shuffled with the loose loot (LootDirector skips it).
            var gift = Mat("GiantGift", new Color(0.85f, 0.25f, 0.45f));
            var giant = Prop("Loot_GiantGift", PrimitiveType.Cube, new Vector3(3f, 0.4f, -2f), new Vector3(0.8f, 0.8f, 0.8f), gift, 8f, true).AddComponent<LootItem>();
            giant.Kind = LootKind.GiantGift;
            Objective(ObjectiveId.Ring, PrimitiveType.Sphere, new Vector3(0.09f, 0.09f, 0.09f));
            Objective(ObjectiveId.CakeTopper, PrimitiveType.Cylinder, new Vector3(0.1f, 0.12f, 0.1f)); // a cylinder doesn't roll off
            Objective(ObjectiveId.Envelope, PrimitiveType.Cube, new Vector3(0.26f, 0.03f, 0.16f));
            Objective(ObjectiveId.Champagne, PrimitiveType.Cylinder, new Vector3(0.09f, 0.17f, 0.09f));
            Objective(ObjectiveId.Bouquet, PrimitiveType.Sphere, new Vector3(0.22f, 0.22f, 0.22f));
            Objective(ObjectiveId.Keys, PrimitiveType.Cube, new Vector3(0.12f, 0.02f, 0.06f));

            var go = new GameObject("LootDirector");
            go.AddComponent<NetworkObject>();
            var director = go.AddComponent<LootDirector>();
            director.LootSpots = s_lootSpots.ToArray();
            director.ObjectiveSpotIds = s_objectiveIds.ToArray();
            director.ObjectiveSpotPositions = s_objectivePositions.ToArray();
            // The den (concept v2): a nest of rags by the catering van.
            director.DenCenter = DenCenter;
            var rags = Mat("Den", new Color(0.4f, 0.3f, 0.2f));
            var grateMat = Mat("Grate", new Color(0.2f, 0.2f, 0.22f));
            Visual(PrimitiveType.Cylinder, "Manhole", null, TrashPandas.Runtime.Squad.GameMode.Manhole + Vector3.up * 0.012f, new Vector3(1f, 0.01f, 1f), grateMat);
            Visual(PrimitiveType.Cylinder, "Den", null, DenCenter + Vector3.up * 0.01f, new Vector3(3f, 0.01f, 3f), rags);
        }

        public static readonly Vector3 DenCenter = new Vector3(23.4f, 0f, -1.2f);
    }
}
