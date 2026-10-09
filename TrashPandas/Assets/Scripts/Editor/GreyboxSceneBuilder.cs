using System.Linq;
using System.IO;
using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Grabbing;
using TrashPandas.Runtime.Net;
using TrashPandas.Runtime.Npc;
using TrashPandas.Runtime.Panic;
using Unity.AI.Navigation;
using UnityEngine.AI;
using Unity.Netcode;
using Unity.Netcode.Components;
using TrashPandas.Runtime.Raccoon;
using TrashPandas.Runtime.Trenchcoat;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrashPandas.EditorTools
{
    /// <summary>Builds the greybox test scene deterministically. Safe to re-run.</summary>
    public static partial class GreyboxSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Greybox_Trenchcoat.unity";
        const string MenuScenePath = "Assets/Scenes/Menu.unity";
        const string PrefabPath = "Assets/Prefabs/Raccoon.prefab";
        const string MaterialDir = "Assets/Materials/Greybox";

        [MenuItem("TrashPandas/Build All Scenes")]
        public static void BuildAll()
        {
            Build();
            BuildMenu();
        }

        static void SetBuildScenes()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            if (File.Exists(MenuScenePath)) scenes.Add(new EditorBuildSettingsScene(MenuScenePath, true));
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>The menu owns the app-wide NetworkManager (it survives loading the game scene).</summary>
        [MenuItem("TrashPandas/Build Menu Scene")]
        public static void BuildMenu()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.22f, 0.16f);

            var net = new GameObject("Network");
            net.AddComponent<NetworkManager>();
            net.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>().DisconnectTimeoutMS = 8000;
            net.AddComponent<NetworkBootstrap>();
            net.AddComponent<SessionHost>();

            new GameObject("MainMenu").AddComponent<TrashPandas.Runtime.Menu.MainMenu>();
            new GameObject("DevAutomation").AddComponent<DevAutomation>();

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[GreyboxSceneBuilder] Built {MenuScenePath}");
        }

        [MenuItem("TrashPandas/Build Greybox Scene")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Prefabs");
            Directory.CreateDirectory(MaterialDir);

            var grass = Mat("Grass", new Color(0.45f, 0.65f, 0.35f));
            var wood = Mat("Wood", new Color(0.85f, 0.8f, 0.7f));
            var coat = Mat("Coat", new Color(0.55f, 0.42f, 0.3f));
            var skin = Mat("Skin", new Color(0.95f, 0.8f, 0.7f));
            var fur = Mat("Fur", new Color(0.45f, 0.45f, 0.5f));
            var curtain = Mat("Curtain", new Color(0.8f, 0.3f, 0.35f));
            var hedge = Mat("Hedge", new Color(0.2f, 0.45f, 0.2f));
            var pants = Mat("Pants", new Color(0.25f, 0.27f, 0.35f));
            var glass = Mat("Glass", new Color(0.7f, 0.85f, 1f));
            var plate = Mat("Plate", new Color(0.97f, 0.97f, 0.95f));
            var cake = Mat("Cake", new Color(1f, 0.92f, 0.8f));

            var raccoonPrefab = BuildRaccoonPrefab(fur);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            light.intensity = 1.2f;

            Box("Ground", new Vector3(0f, -0.5f, 2f), new Vector3(64f, 1f, 58f), grass);
            s_lootSpots.Clear();
            s_objectiveIds.Clear();
            s_objectivePositions.Clear();

            // Tables: the coat can't fit under them; a crouched raccoon can (gap 0.75 m, raccoon crouch 0.3 m).
            for (int i = 0; i < 6; i++)
            {
                var pos = new Vector3(-6f + (i % 3) * 6f, 0f, 4f + (i / 3) * 5f);
                var table = new GameObject($"Table_{i}");
                table.transform.position = pos;
                Box("Top", pos + new Vector3(0f, 0.8f, 0f), new Vector3(2f, 0.1f, 1.2f), wood).transform.SetParent(table.transform, true);
                Tablecloth(table.transform, pos, new Vector2(2f, 1.2f), 0.85f);
                foreach (var leg in new[] { new Vector3(-0.9f, 0f, -0.5f), new Vector3(0.9f, 0f, -0.5f), new Vector3(-0.9f, 0f, 0.5f), new Vector3(0.9f, 0f, 0.5f) })
                    Box("Leg", pos + leg + new Vector3(0f, 0.375f, 0f), new Vector3(0.08f, 0.75f, 0.08f), wood).transform.SetParent(table.transform, true);
            }

            // Things to grab: glasses, plates and wallets on every table; a two-hand cake on the far middle table.
            const float tableTop = 0.85f;

            for (int i = 0; i < 6; i++)
            {
                var t = new Vector3(-6f + (i % 3) * 6f, 0f, 4f + (i / 3) * 5f);
                Prop($"Glass_{i}a", PrimitiveType.Cylinder, t + new Vector3(-0.6f, tableTop + 0.1f, -0.35f), new Vector3(0.08f, 0.1f, 0.08f), glass, 0.2f, false);
                Prop($"Glass_{i}b", PrimitiveType.Cylinder, t + new Vector3(0.5f, tableTop + 0.1f, -0.4f), new Vector3(0.08f, 0.1f, 0.08f), glass, 0.2f, false);
                Prop($"Plate_{i}", PrimitiveType.Cylinder, t + new Vector3(0f, tableTop + 0.02f, -0.3f), new Vector3(0.28f, 0.015f, 0.28f), plate, 0.4f, false);
                // Loot sits near the table ends, within reach of a coat standing beside it.
                Spot(t + new Vector3(-0.8f, tableTop + 0.03f, -0.2f));
                Spot(t + new Vector3(0.8f, tableTop + 0.03f, 0.2f));
            }
            BuildEstate(wood, hedge);
            BuildLoot();
            var cakeGo = Prop("Cake", PrimitiveType.Cylinder, new Vector3(0f, tableTop + 0.2f, 8.6f), new Vector3(0.5f, 0.2f, 0.5f), cake, 3f, true);
            var tier2 = Visual(PrimitiveType.Cylinder, "Tier2", cakeGo.transform, new Vector3(0f, 1.4f, 0f), new Vector3(0.65f, 0.6f, 0.65f), cake);
            tier2.gameObject.AddComponent<BoxCollider>(); // the topper stands on it

            // Climbable curtain with a ledge on top.
            var curtainGo = Box("Curtain_Climbable", new Vector3(10f, 2f, 0f), new Vector3(2f, 4f, 0.1f), curtain);
            curtainGo.AddComponent<Climbable>();
            Box("Ledge", new Vector3(10f, 4.05f, 0.6f), new Vector3(2f, 0.1f, 1.3f), wood);

            // Hedge wall with a raccoon-sized gap (1 m wide, 0.5 m tall: only a crouched raccoon fits).
            Box("Hedge_Left", new Vector3(-4.25f, 1f, -8f), new Vector3(7.5f, 2f, 1f), hedge);
            Box("Hedge_Right", new Vector3(4.25f, 1f, -8f), new Vector3(7.5f, 2f, 1f), hedge);
            Box("Hedge_Top", new Vector3(0f, 1.25f, -8f), new Vector3(1f, 1.5f, 1f), hedge);
            var hedgeClimb = Box("Hedge_Climbable", new Vector3(8f, 1f, -8f), new Vector3(1f, 2f, 1f), hedge);
            hedgeClimb.AddComponent<Climbable>();

            var body = BuildTrenchcoat(coat, skin, pants, raccoonPrefab);
            BuildWeddingPeople(body, skin);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var outputCamera = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 3f, -5f);
            camGo.AddComponent<CinemachineBrain>().UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            camGo.AddComponent<CameraShake>();
            var rig = BuildPlayerCamera(outputCamera, body.transform);


            var controller = new GameObject("TrenchcoatController").AddComponent<TrenchcoatController>();
            controller.PlayerCount = 3;
            controller.Body = body;
            controller.RaccoonPrefab = raccoonPrefab;
            controller.CameraRig = rig;

            // Concept v2: a squad of loose raccoons (debug); the coat waits for the events.
            var squad = new GameObject("SquadController").AddComponent<TrashPandas.Runtime.Squad.SquadController>();
            squad.RaccoonPrefab = raccoonPrefab;
            squad.CameraRig = rig;
            squad.Coat = body;
            new GameObject("GrabHighlight").AddComponent<TrashPandas.Runtime.Raccoon.GrabHighlight>();
            new GameObject("DebugChecklist").AddComponent<TrashPandas.Runtime.Ui.DebugChecklist>();
            new GameObject("GameHud").AddComponent<TrashPandas.Runtime.Ui.GameHud>();
            var gadgets = new GameObject("GadgetDirector");
            gadgets.AddComponent<NetworkObject>();
            gadgets.AddComponent<TrashPandas.Runtime.Squad.GadgetDirector>();
            var intro = new GameObject("RoundIntro");
            intro.AddComponent<NetworkObject>();
            intro.AddComponent<TrashPandas.Runtime.Squad.RoundIntro>().CameraRig = rig;
            var carry = new GameObject("CarryDirector");
            carry.AddComponent<NetworkObject>();
            carry.AddComponent<TrashPandas.Runtime.Squad.CarryDirector>();

            var online = new GameObject("OnlinePlayerController").AddComponent<OnlinePlayerController>();
            online.CameraRig = rig;

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssignNetworkIds(scene);
            SetBuildScenes();
            AssetDatabase.SaveAssets();
            Debug.Log($"[GreyboxSceneBuilder] Built {ScenePath}");
        }

        /// <summary>
        /// In-scene NetworkObjects get their GlobalObjectIdHash from the saved scene's object ids, which only
        /// exist after the first save: validate them now and save again.
        /// </summary>
        static void AssignNetworkIds(UnityEngine.SceneManagement.Scene scene)
        {
            var validate = typeof(NetworkObject).GetMethod("OnValidate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            foreach (var no in Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                validate?.Invoke(no, null);
                EditorUtility.SetDirty(no);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        /// <summary>Guests (seated and standing), a waiter on a service route, the cat, the catering tent,
        /// the NavMesh surface, and the suspicion director + HUD (stage 3a).</summary>
        static void BuildWeddingPeople(TrenchcoatBody coat, Material skin)
        {
            var nav = new GameObject("Navigation").AddComponent<NavMeshSurface>();
            nav.collectObjects = CollectObjects.All;
            nav.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            Color[] outfits =
            {
                new Color(0.55f, 0.6f, 0.85f), new Color(0.85f, 0.55f, 0.6f), new Color(0.6f, 0.8f, 0.6f),
                new Color(0.9f, 0.75f, 0.45f), new Color(0.7f, 0.6f, 0.85f), new Color(0.5f, 0.75f, 0.8f),
            };
            int n = 0;
            // One seated guest at the far side of each table, facing the garden.
            for (int i = 0; i < 6; i++)
            {
                var t = new Vector3(-6f + (i % 3) * 6f, 0f, 4f + (i / 3) * 5f);
                Person($"Guest_Seated_{i}", NpcKind.Guest, t + new Vector3(0.4f * (i % 2 == 0 ? 1 : -1), 0f, 1.1f), 180f, outfits[n++ % outfits.Length], skin, seated: true);
            }
            // Two standing groups chatting.
            Person("Guest_Standing_0", NpcKind.Guest, new Vector3(-10f, 0f, -2f), 90f, outfits[n++ % outfits.Length], skin, false);
            Person("Guest_Standing_1", NpcKind.Guest, new Vector3(-8.8f, 0f, -2f), -90f, outfits[n++ % outfits.Length], skin, false);

            // Social-event speakers (stage 3b). They're guests too: they see and react like everyone else.
            Person("MotherInLaw", NpcKind.Guest, new Vector3(-3f, 0f, 13f), 180f, new Color(0.55f, 0.3f, 0.65f), skin, false).SpeakerId = "MotherInLaw";
            Person("Priest", NpcKind.Guest, new Vector3(8f, 0f, 13f), 200f, new Color(0.08f, 0.08f, 0.1f), skin, false).SpeakerId = "Priest";
            Person("Bride", NpcKind.Guest, new Vector3(2.6f, 0f, 14.2f), 180f, new Color(0.98f, 0.97f, 0.95f), skin, false).SpeakerId = "Bride";

            // The rest of the estate (stage 3c-1): cooks, guests in the house, the chauffeur, a couple in the orchard.
            Person("Cook_0", NpcKind.Guest, new Vector3(-20f, 0f, 10.5f), 90f, new Color(0.95f, 0.95f, 0.95f), skin, false);
            Person("Guest_House_0", NpcKind.Guest, new Vector3(-7.5f, 0f, 21.5f), 90f, outfits[n++ % outfits.Length], skin, false);
            Person("Chauffeur", NpcKind.Guest, new Vector3(19.5f, 0f, 11f), -90f, new Color(0.15f, 0.15f, 0.2f), skin, false);
            Person("Guest_Orchard_0", NpcKind.Guest, new Vector3(14f, 0f, -16f), 90f, outfits[n++ % outfits.Length], skin, false);
            Person("Guest_Orchard_1", NpcKind.Guest, new Vector3(15.2f, 0f, -16f), -90f, outfits[n++ % outfits.Length], skin, false);

            var waiter = Person("Waiter", NpcKind.Waiter, new Vector3(-17f, 0f, 9.5f), 0f, new Color(0.12f, 0.12f, 0.14f), skin, false);
            waiter.SpeakerId = "Waiter";
            // Kitchen → garden → the house → back through the garden.
            waiter.gameObject.AddComponent<NpcRoute>().Points = new[]
            {
                new Vector3(-17f, 0f, 9.5f), new Vector3(-6f, 0f, 6.8f), new Vector3(0f, 0f, 6.8f), new Vector3(6f, 0f, 6.8f),
                new Vector3(3f, 0f, 16.5f), new Vector3(0f, 0f, 19.5f), new Vector3(-4f, 0f, 22.5f), new Vector3(0f, 0f, 16.5f),
                new Vector3(6f, 0f, 1.8f), new Vector3(0f, 0f, 1.8f), new Vector3(-6f, 0f, 1.8f),
            };

            var cat = Person("Cat", NpcKind.Cat, new Vector3(4f, 0f, -4f), 0f, new Color(0.98f, 0.98f, 0.98f), skin, false);
            cat.gameObject.AddComponent<NpcRoute>().Points = new[]
            {
                new Vector3(4f, 0f, -4f), new Vector3(18f, 0f, 0f), new Vector3(9f, 0f, 1f), new Vector3(3f, 0f, 11.5f), new Vector3(0f, 0f, 16.5f),
                new Vector3(-3f, 0f, 22f), new Vector3(0f, 0f, 16.5f), new Vector3(-9f, 0f, 11.5f), new Vector3(-17f, 0f, 6f), new Vector3(-9f, 0f, 0f),
                new Vector3(18f, 0f, -17f),
            };

            BuildNemesis(skin);
            BuildSillyStuff();

            var director = new GameObject("SuspicionDirector");
            director.AddComponent<NetworkObject>();
            var d = director.AddComponent<SuspicionDirector>();
            d.Coat = coat;
            d.Navigation = nav;
            new GameObject("SuspicionHud").AddComponent<SuspicionHud>();
            var events = new GameObject("SocialEventDirector");
            events.AddComponent<NetworkObject>();
            events.AddComponent<SocialEventDirector>();
            new GameObject("EventHud").AddComponent<EventHud>();
            BuildPanic();
        }

        /// <summary>Stage 4: weapons to grab, the four exits, an overview point for spectators, the panic director + HUD.</summary>
        static void BuildPanic()
        {
            var wood = Mat("BroomWood", new Color(0.65f, 0.48f, 0.3f));
            var straw = Mat("BroomStraw", new Color(0.9f, 0.78f, 0.4f));
            var metal = Mat("Pan", new Color(0.25f, 0.25f, 0.28f));
            var chairMat = Mat("Chair", new Color(0.8f, 0.72f, 0.6f));
            var silver = Mat("Tray", new Color(0.82f, 0.84f, 0.88f));

            for (int i = 0; i < 3; i++)
            {
                var broom = Weapon($"Broom_{i}", WeaponKind.Broom, i < 2 ? new Vector3(-14.8f, 0.6f, 3f + i * 0.6f) : new Vector3(-9f, 0.6f, 19f));
                Visual(PrimitiveType.Cylinder, "Stick", broom.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.05f, 0.6f, 0.05f), wood);
                Visual(PrimitiveType.Cube, "Bristles", broom.transform, new Vector3(0f, 1.25f, 0f), new Vector3(0.3f, 0.2f, 0.08f), straw);
            }
            for (int i = 0; i < 2; i++)
            {
                var pan = Weapon($"Pan_{i}", WeaponKind.Pan, new Vector3(-21.5f + i * 1f, 0.1f, 10f));
                Visual(PrimitiveType.Cylinder, "Pan", pan.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.35f, 0.02f, 0.35f), metal);
                Visual(PrimitiveType.Cylinder, "Handle", pan.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.04f, 0.25f, 0.04f), metal);
            }
            for (int i = 0; i < 6; i++)
            {
                var t = new Vector3(-6f + (i % 3) * 6f, 0f, 4f + (i / 3) * 5f);
                var chair = Weapon($"Chair_{i}", WeaponKind.Chair, t + new Vector3(1.4f, 0f, -0.9f));
                var underChair = chair.gameObject.AddComponent<TrashPandas.Runtime.Squad.HidingSpot>();
                underChair.Kind = TrashPandas.Runtime.Squad.HidingKind.UnderTablecloth;
                underChair.Size = new Vector3(0.55f, 0.45f, 0.55f);
                Visual(PrimitiveType.Cube, "Seat", chair.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.06f, 0.45f), chairMat);
                Visual(PrimitiveType.Cube, "Back", chair.transform, new Vector3(0f, 0.75f, -0.2f), new Vector3(0.45f, 0.6f, 0.05f), chairMat);
            }
            var tray = Weapon("Tray", WeaponKind.Tray, new Vector3(-16f, 0.05f, 8.8f));
            Visual(PrimitiveType.Cylinder, "Tray", tray.transform, new Vector3(0f, 0.4f, 0f), new Vector3(0.45f, 0.015f, 0.45f), silver);

            // Five possible exits (spec §16.4), three open per round, one per zone at most:
            // kitchen back door + sewer (kitchen), bathroom window (house), the van (parking), the orchard gap.
            var gold = Mat("Exit", new Color(1f, 0.82f, 0.2f));
            var exits = new[]
            {
                new Vector3(-25.2f, 0f, 8.2f), new Vector3(-20f, 0f, -4f), new Vector3(7f, 0f, 29.6f),
                new Vector3(23.8f, 0f, 2f), new Vector3(20f, 0f, -23.2f),
            };
            var names = new[] { "Kitchen back door", "Sewer", "Bathroom window (jump!)", "Catering van", "Orchard gap (crouch!)" };
            var zones = new[] { 1, 1, 2, 3, 4 };
            var markers = new GameObject[exits.Length];
            for (int i = 0; i < exits.Length; i++)
            {
                // Ring on the ground + a floating arrow (shaft + diamond tip pointing down). Hidden until RUN.
                var marker = new GameObject($"ExitMarker_{i}");
                marker.transform.position = exits[i];
                Visual(PrimitiveType.Cylinder, "Ring", marker.transform, Vector3.up * 0.02f, new Vector3(2.4f, 0.005f, 2.4f), gold);
                var arrow = new GameObject("Arrow").transform;
                arrow.SetParent(marker.transform, false);
                arrow.localPosition = Vector3.up * 3.8f;
                Visual(PrimitiveType.Cube, "Shaft", arrow, Vector3.up * 0.45f, new Vector3(0.18f, 0.7f, 0.18f), gold);
                var tip = Visual(PrimitiveType.Cube, "Tip", arrow, Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f), gold);
                tip.localRotation = Quaternion.Euler(45f, 0f, 45f);
                foreach (var c in marker.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                marker.AddComponent<ExitBeacon>().Arrow = arrow;
                marker.SetActive(false);
                markers[i] = marker;
            }

            var overview = new GameObject("Overview").transform;
            overview.position = new Vector3(0f, 0f, 3f);

            var go = new GameObject("PanicDirector");
            go.AddComponent<NetworkObject>();
            var pd = go.AddComponent<PanicDirector>();
            pd.Exits = exits;
            pd.ExitNames = names;
            pd.ExitMarkers = markers;
            pd.ExitZoneIds = zones;
            pd.GardenCenter = GardenCenter;
            pd.ArchCenter = ArchCenter;
            pd.ArchRadius = 1.8f;
            // The pet carrier for caught raccoons, by the house door (friends can free them).
            pd.CagePosition = new Vector3(-4.5f, 0f, 16.2f);
            pd.CageRadius = 1.4f;
            var cageMat = Mat("Cage", new Color(0.55f, 0.75f, 0.95f));
            var cage = new GameObject("PetCarrier").transform;
            cage.position = pd.CagePosition;
            Visual(PrimitiveType.Cube, "Floor", cage, new Vector3(0f, 0.02f, 0f), new Vector3(2.0f, 0.04f, 1.0f), cageMat);
            Visual(PrimitiveType.Cube, "Roof", cage, new Vector3(0f, 0.9f, 0f), new Vector3(2.0f, 0.06f, 1.0f), cageMat);
            for (int b = 0; b < 9; b++)
                Visual(PrimitiveType.Cylinder, $"Bar_{b}", cage, new Vector3(-0.95f + b * 0.2375f, 0.45f, 0.5f), new Vector3(0.04f, 0.45f, 0.04f), cageMat);
            pd.Overview = overview;
            new GameObject("PanicHud").AddComponent<PanicHud>();
            new GameObject("LootHud").AddComponent<TrashPandas.Runtime.Loot.LootHud>();
        }

        static PanicWeapon Weapon(string name, WeaponKind kind, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(0.4f, 1f, 0.4f);
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            IgnoreForNavigation(go);
            var weapon = go.AddComponent<PanicWeapon>();
            weapon.Kind = kind;
            return weapon;
        }

        /// <summary>
        /// Irreverent nonsense (playtest: "no hay cosas irreverentes o graciosas sin sentido"): an alien guest nobody
        /// questions, a dancing hot dog mascot you can ride inside, and tool pickups lying around (bananas!).
        /// </summary>
        static void BuildSillyStuff()
        {
            // The alien: green, big black eyes, antennae. It doesn't report raccoons; it has its own agenda.
            var alien = Person("Alien", NpcKind.Guest, new Vector3(5f, 0f, -1.5f), 200f, new Color(0.35f, 0.75f, 0.35f), Mat("AlienSkin", new Color(0.55f, 0.9f, 0.45f)), false);
            alien.SpeakerId = "Alien";
            var eye = Mat("AlienEye", new Color(0.02f, 0.02f, 0.03f));
            Visual(PrimitiveType.Sphere, "EyeL", alien.Head, new Vector3(-0.08f, 0.02f, 0.13f), new Vector3(0.1f, 0.13f, 0.05f), eye);
            Visual(PrimitiveType.Sphere, "EyeR", alien.Head, new Vector3(0.08f, 0.02f, 0.13f), new Vector3(0.1f, 0.13f, 0.05f), eye);
            var antenna = Mat("Antenna", new Color(0.3f, 0.7f, 0.3f));
            Visual(PrimitiveType.Cylinder, "AntennaL", alien.Head, new Vector3(-0.07f, 0.22f, 0f), new Vector3(0.02f, 0.1f, 0.02f), antenna);
            Visual(PrimitiveType.Cylinder, "AntennaR", alien.Head, new Vector3(0.07f, 0.22f, 0f), new Vector3(0.02f, 0.1f, 0.02f), antenna);
            var bulb = Mat("AntennaBulb", new Color(1f, 0.35f, 0.8f));
            Visual(PrimitiveType.Sphere, "BulbL", alien.Head, new Vector3(-0.07f, 0.33f, 0f), Vector3.one * 0.06f, bulb);
            Visual(PrimitiveType.Sphere, "BulbR", alien.Head, new Vector3(0.07f, 0.33f, 0f), Vector3.one * 0.06f, bulb);
            alien.gameObject.AddComponent<NpcRoute>().Points = new[] { new Vector3(5f, 0f, -1.5f), new Vector3(2f, 0f, 3f), new Vector3(9f, 0f, 2.5f), new Vector3(6f, 0f, -6f) };

            // The hot dog mascot: dances around the garden; hop inside and ride along.
            var dog = new GameObject("HotDogMascot");
            dog.transform.position = new Vector3(-4f, 0f, -0.5f);
            var col = dog.AddComponent<CapsuleCollider>();
            col.height = 2f; col.radius = 0.45f; col.center = new Vector3(0f, 1f, 0f); col.isTrigger = true;
            var body = new GameObject("Body").transform;
            body.SetParent(dog.transform, false);
            var bun = Mat("Bun", new Color(0.88f, 0.65f, 0.35f));
            var sausage = Mat("Sausage", new Color(0.75f, 0.3f, 0.2f));
            var mustard = Mat("Mustard", new Color(1f, 0.85f, 0.1f));
            Visual(PrimitiveType.Capsule, "BunBack", body, new Vector3(0f, 1f, -0.18f), new Vector3(0.7f, 0.95f, 0.3f), bun);
            Visual(PrimitiveType.Capsule, "Sausage", body, new Vector3(0f, 1.1f, 0.02f), new Vector3(0.45f, 1.05f, 0.45f), sausage);
            Visual(PrimitiveType.Cube, "Mustard", body, new Vector3(0f, 1.15f, 0.24f), new Vector3(0.08f, 1.2f, 0.03f), mustard);
            var white = Mat("MascotEye", Color.white);
            Visual(PrimitiveType.Sphere, "EyeL", body, new Vector3(-0.1f, 1.7f, 0.2f), Vector3.one * 0.16f, white);
            Visual(PrimitiveType.Sphere, "EyeR", body, new Vector3(0.1f, 1.7f, 0.2f), Vector3.one * 0.16f, white);
            var pupil = Mat("MascotPupil", new Color(0.05f, 0.05f, 0.05f));
            Visual(PrimitiveType.Sphere, "PupilL", body, new Vector3(-0.1f, 1.7f, 0.27f), Vector3.one * 0.06f, pupil);
            Visual(PrimitiveType.Sphere, "PupilR", body, new Vector3(0.1f, 1.7f, 0.27f), Vector3.one * 0.06f, pupil);
            Visual(PrimitiveType.Cube, "Smile", body, new Vector3(0f, 1.48f, 0.23f), new Vector3(0.18f, 0.03f, 0.03f), pupil);
            Transform Arm(string n, float x)
            {
                var pivot = new GameObject(n).transform;
                pivot.SetParent(body, false);
                pivot.localPosition = new Vector3(x, 1.25f, 0f);
                Visual(PrimitiveType.Capsule, n + "Mesh", pivot, new Vector3(Mathf.Sign(x) * 0.25f, 0f, 0f), new Vector3(0.12f, 0.25f, 0.12f), bun).localRotation = Quaternion.Euler(0f, 0f, 90f);
                return pivot;
            }
            var hide = dog.AddComponent<TrashPandas.Runtime.Squad.MascotHideout>();
            hide.Body = body;
            hide.ArmL = Arm("ArmL", -0.3f);
            hide.ArmR = Arm("ArmR", 0.3f);
            hide.Route = new[] { new Vector3(-4f, 0f, -0.5f), new Vector3(3f, 0f, 0f), new Vector3(9f, 0f, 1.5f), new Vector3(3f, 0f, 6.8f), new Vector3(-5f, 0f, 6.8f), new Vector3(-9f, 0f, 1f) };
            dog.AddComponent<NetworkObject>();
            var nt = dog.AddComponent<NetworkTransform>();
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            IgnoreForNavigation(dog);

            // Tools lying around: pebble piles, banana bunches, smoke bombs (they come back after a while).
            void Pickup(string name, TrashPandas.Core.Raccoons.Gadget kind, int amount, Vector3 at)
            {
                var root = new GameObject(name);
                root.transform.position = at;
                var vis = new GameObject("Visual").transform;
                vis.SetParent(root.transform, false);
                vis.localPosition = Vector3.up * 0.45f;
                switch (kind)
                {
                    case TrashPandas.Core.Raccoons.Gadget.Pebble:
                        var stone = Mat("Pebbles", new Color(0.62f, 0.62f, 0.65f));
                        for (int i = 0; i < 4; i++) Visual(PrimitiveType.Sphere, "Stone", vis, new Vector3((i % 2) * 0.14f - 0.07f, (i / 2) * 0.1f, (i % 3) * 0.06f - 0.06f), Vector3.one * 0.14f, stone);
                        break;
                    case TrashPandas.Core.Raccoons.Gadget.Banana:
                        var yellow = Mat("BananaBunch", new Color(1f, 0.85f, 0.15f));
                        for (int i = 0; i < 4; i++) Visual(PrimitiveType.Capsule, "Banana", vis, new Vector3(i * 0.07f - 0.1f, 0f, 0f), new Vector3(0.08f, 0.17f, 0.08f), yellow).localRotation = Quaternion.Euler(0f, 0f, -20f + i * 12f);
                        break;
                    default:
                        Visual(PrimitiveType.Sphere, "Bomb", vis, Vector3.zero, Vector3.one * 0.26f, Mat("SmokeBomb", new Color(0.4f, 0.32f, 0.55f)));
                        Visual(PrimitiveType.Cylinder, "Fuse", vis, new Vector3(0f, 0.16f, 0f), new Vector3(0.03f, 0.05f, 0.03f), Mat("Fuse", new Color(0.9f, 0.5f, 0.2f)));
                        break;
                }
                var p = root.AddComponent<TrashPandas.Runtime.Squad.GadgetPickup>();
                p.Kind = kind;
                p.Amount = amount;
                p.Visual = vis;
            }
            Pickup("Pickup_Pebbles_Garden", TrashPandas.Core.Raccoons.Gadget.Pebble, 3, new Vector3(-2f, 0f, -4.5f));
            Pickup("Pickup_Pebbles_Orchard", TrashPandas.Core.Raccoons.Gadget.Pebble, 3, new Vector3(12f, 0f, -12f));
            Pickup("Pickup_Pebbles_House", TrashPandas.Core.Raccoons.Gadget.Pebble, 3, new Vector3(-12f, 0f, 16f));
            Pickup("Pickup_Bananas_Kitchen", TrashPandas.Core.Raccoons.Gadget.Banana, 2, new Vector3(-19f, 0f, 6f));
            Pickup("Pickup_Bananas_Garden", TrashPandas.Core.Raccoons.Gadget.Banana, 2, new Vector3(10f, 0f, 4.5f));
            Pickup("Pickup_Bananas_Parking", TrashPandas.Core.Raccoons.Gadget.Banana, 2, new Vector3(21f, 0f, -4f));
            Pickup("Pickup_Smoke_Shed", TrashPandas.Core.Raccoons.Gadget.SmokeBomb, 1, new Vector3(-10f, 0f, -6f));
            Pickup("Pickup_Smoke_Lawn", TrashPandas.Core.Raccoons.Gadget.SmokeBomb, 1, new Vector3(6f, 0f, -10f));
        }

        /// <summary>Tonight's antagonist: one body, three costumes (the director shows the one picked this round).</summary>
        static void BuildNemesis(Material skin)
        {
            var pawn = Person("Nemesis", NpcKind.Nemesis, new Vector3(0f, 0f, 9.5f), 180f, new Color(0.3f, 0.3f, 0.32f), skin, false);
            pawn.SpeakerId = ""; // not part of the social events
            var root = pawn.transform;
            var head = pawn.Head;
            Transform Costume(string name)
            {
                var c = new GameObject(name).transform;
                c.SetParent(root, false);
                return c;
            }
            Transform OnHead(Transform costume, string name)
            {
                // Head pieces follow the head (it turns), but switch on/off with the costume.
                var h = new GameObject(name).transform;
                h.SetParent(head, false);
                costume.gameObject.AddComponent<TrashPandas.Runtime.Panic.CostumePart>().Linked = h.gameObject;
                return h;
            }
            float H = 1.75f;

            // The wedding planner: hot-pink blazer, headset with a mic, clipboard.
            var planner = Costume("Costume_WeddingPlanner");
            var pink = Mat("Planner", new Color(0.95f, 0.35f, 0.6f));
            var black = Mat("Headset", new Color(0.08f, 0.08f, 0.1f));
            Visual(PrimitiveType.Capsule, "Blazer", planner, new Vector3(0f, H * 0.45f, 0f), new Vector3(0.56f, H * 0.36f, 0.46f), pink);
            Visual(PrimitiveType.Cube, "Clipboard", planner, new Vector3(0.36f, H * 0.6f, 0.22f), new Vector3(0.05f, 0.32f, 0.24f), Mat("Clipboard", new Color(0.75f, 0.6f, 0.4f)));
            var ph = OnHead(planner, "PlannerHead");
            Visual(PrimitiveType.Cube, "Band", ph, new Vector3(0f, 0.15f, 0f), new Vector3(0.34f, 0.03f, 0.06f), black);
            Visual(PrimitiveType.Cube, "Mic", ph, new Vector3(0.13f, -0.06f, 0.1f), new Vector3(0.02f, 0.02f, 0.18f), black);
            Visual(PrimitiveType.Sphere, "Bun", ph, new Vector3(0f, 0.12f, -0.12f), Vector3.one * 0.14f, Mat("Hair", new Color(0.35f, 0.2f, 0.1f)));

            // The pest control guy: khaki overalls, yellow tank on his back, cap, flashlight (a real light).
            var pest = Costume("Costume_PestControl");
            var khaki = Mat("Khaki", new Color(0.6f, 0.55f, 0.35f));
            Visual(PrimitiveType.Capsule, "Overalls", pest, new Vector3(0f, H * 0.45f, 0f), new Vector3(0.58f, H * 0.37f, 0.48f), khaki);
            Visual(PrimitiveType.Cylinder, "Tank", pest, new Vector3(0f, H * 0.6f, -0.3f), new Vector3(0.24f, 0.28f, 0.24f), Mat("Tank", new Color(0.95f, 0.8f, 0.15f)));
            var torch = Visual(PrimitiveType.Cylinder, "Flashlight", pest, new Vector3(0.34f, H * 0.62f, 0.3f), new Vector3(0.07f, 0.12f, 0.07f), black);
            torch.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var beam = new GameObject("Beam").AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.spotAngle = 50f;
            beam.range = 14f;
            beam.intensity = 6f;
            beam.color = new Color(1f, 0.95f, 0.75f);
            beam.transform.SetParent(pest, false);
            beam.transform.localPosition = new Vector3(0.34f, H * 0.62f, 0.4f);
            beam.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            var pc = OnHead(pest, "PestHead");
            Visual(PrimitiveType.Cylinder, "Cap", pc, new Vector3(0f, 0.12f, 0f), new Vector3(0.32f, 0.04f, 0.32f), khaki);
            Visual(PrimitiveType.Cube, "Visor", pc, new Vector3(0f, 0.1f, 0.16f), new Vector3(0.24f, 0.02f, 0.14f), khaki);

            // Granny: lavender shawl, grey bun, glasses, a cane (and a slipper in reserve).
            var granny = Costume("Costume_Granny");
            var lavender = Mat("Shawl", new Color(0.7f, 0.6f, 0.85f));
            Visual(PrimitiveType.Capsule, "Shawl", granny, new Vector3(0f, H * 0.42f, 0f), new Vector3(0.6f, H * 0.36f, 0.5f), lavender);
            Visual(PrimitiveType.Cylinder, "Cane", granny, new Vector3(0.36f, 0.45f, 0.2f), new Vector3(0.04f, 0.45f, 0.04f), Mat("Cane", new Color(0.4f, 0.25f, 0.12f)));
            var gh = OnHead(granny, "GrannyHead");
            var grey = Mat("GreyHair", new Color(0.82f, 0.82f, 0.85f));
            Visual(PrimitiveType.Sphere, "Hair", gh, new Vector3(0f, 0.06f, -0.03f), new Vector3(0.33f, 0.25f, 0.33f), grey);
            Visual(PrimitiveType.Sphere, "Bun", gh, new Vector3(0f, 0.2f, -0.06f), Vector3.one * 0.15f, grey);
            Visual(PrimitiveType.Cube, "Glasses", gh, new Vector3(0f, 0.02f, 0.15f), new Vector3(0.24f, 0.05f, 0.02f), black);

            // Real models (Meshy, rigged) in place of the greybox costumes, where they exist.
            bool anyModel = false;
            foreach (var (artName, costume) in new[] { ("Planner", planner), ("PestControl", pest), ("Granny", granny) })
            {
                var m = AttachCharacterModel(artName, costume, pawn);
                if (!m) continue;
                anyModel = true;
                foreach (Transform c in costume) if (c != m.transform && c.name != "Beam") c.gameObject.SetActive(false);
                var part = costume.GetComponent<TrashPandas.Runtime.Panic.CostumePart>();
                if (part && part.Linked) foreach (Transform c in part.Linked.transform) c.gameObject.SetActive(false);
            }
            if (anyModel)
            {
                var body = root.Find("Body");
                if (body) body.gameObject.SetActive(false);
                foreach (Transform c in head) c.gameObject.SetActive(false);
                foreach (Transform c in head) if (c.GetComponent<Renderer>()) c.gameObject.SetActive(false);
            }

            var go = new GameObject("NemesisDirector");
            go.AddComponent<NetworkObject>();
            var nd = go.AddComponent<TrashPandas.Runtime.Panic.NemesisDirector>();
            nd.Pawn = pawn;
            nd.Costumes = new[] { planner.gameObject, pest.gameObject, granny.gameObject };
            // Round the estate: garden, house, kitchen, parking, orchard, south lawn.
            nd.Patrol = new[]
            {
                new Vector3(0f, 0f, 9.5f), new Vector3(-3f, 0f, 19f), new Vector3(-15f, 0f, 9f), new Vector3(-8f, 0f, -1f),
                new Vector3(4f, 0f, -8f), new Vector3(15f, 0f, -14f), new Vector3(20f, 0f, 0f), new Vector3(9f, 0f, 4f),
            };
        }

        static NpcPawn Person(string name, NpcKind kind, Vector3 position, float yaw, Color outfit, Material skin, bool seated)
        {
            bool cat = kind == NpcKind.Cat;
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            var outfitMat = Mat($"Outfit_{name}", outfit);

            float height = cat ? 0.4f : seated ? 1.3f : 1.75f;
            var col = root.AddComponent<CapsuleCollider>();
            col.height = height;
            col.radius = cat ? 0.18f : 0.28f;
            col.center = new Vector3(0f, height / 2f, 0f);
            if (cat)
            {
                Visual(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.3f, 0.2f, 0.45f), outfitMat);
            }
            else
            {
                Visual(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, height * 0.42f, 0f), new Vector3(0.5f, height * 0.42f, 0.4f), outfitMat);
            }
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, cat ? 0.32f : height - 0.15f, cat ? 0.22f : 0f);
            Visual(PrimitiveType.Sphere, "Face", head, Vector3.zero, Vector3.one * (cat ? 0.22f : 0.3f), cat ? outfitMat : skin);
            Visual(PrimitiveType.Cube, "Nose", head, new Vector3(0f, 0f, cat ? 0.12f : 0.17f), new Vector3(0.06f, 0.06f, 0.08f), cat ? outfitMat : skin);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = col.radius;
            agent.height = height;
            agent.speed = cat ? 2.6f : kind == NpcKind.Waiter ? 1.6f : 1.1f;
            agent.angularSpeed = 360f;
            agent.enabled = false; // the director enables it once the NavMesh is baked

            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            var pawn = root.AddComponent<NpcPawn>();
            pawn.Kind = kind;
            pawn.Head = head;
            pawn.Seated = seated;
            pawn.EyeHeight = head.localPosition.y;
            var hand = new GameObject("Hand").transform;
            hand.SetParent(root.transform, false);
            hand.localPosition = cat ? new Vector3(0f, 0.25f, 0.3f) : new Vector3(0.32f, height * 0.6f, 0.2f);
            pawn.Hand = hand;
            return pawn;
        }

        static PlayerCameraRig BuildPlayerCamera(Camera output, Transform target)
        {
            var go = new GameObject("PlayerCamera");
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Follow = target;
            vcam.LookAt = target;
            vcam.Lens.FieldOfView = 55f;

            var orbit = go.AddComponent<CinemachineOrbitalFollow>();
            orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.Sphere;
            orbit.Radius = 5.5f;
            orbit.TargetOffset = new Vector3(0f, 1.4f, 0f);
            var tracker = orbit.TrackerSettings;
            tracker.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.WorldSpace;
            tracker.PositionDamping = new Vector3(0.1f, 0.3f, 0.2f);
            orbit.TrackerSettings = tracker;
            orbit.VerticalAxis.Range = new Vector2(-20f, 70f);
            orbit.VerticalAxis.Value = 15f;
            orbit.HorizontalAxis.Recentering.Enabled = false;
            orbit.VerticalAxis.Recentering.Enabled = false;

            var composer = go.AddComponent<CinemachineRotationComposer>();
            composer.TargetOffset = new Vector3(0f, 1.4f, 0f);
            composer.Damping = new Vector2(0.15f, 0.15f);

            var rig = go.AddComponent<PlayerCameraRig>();
            rig.VirtualCamera = vcam;
            rig.Orbit = orbit;
            rig.Composer = composer;
            rig.OutputCamera = output;
            return rig;
        }

        static TrenchcoatBody BuildTrenchcoat(Material coat, Material skin, Material pants, RaccoonController raccoonPrefab)
        {
            var root = new GameObject("Trenchcoat");
            root.transform.position = new Vector3(0f, 0.05f, 0f);
            root.AddComponent<NetworkObject>();
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 60f;
            var col = root.AddComponent<CapsuleCollider>();
            col.height = 2.1f;
            col.radius = 0.35f;
            col.center = new Vector3(0f, 1.05f, 0f);
            // Frictionless so ground contact doesn't fight the velocity we set every physics step.
            col.sharedMaterial = FrictionlessMaterial();

            var torso = Visual(PrimitiveType.Capsule, "Torso", root.transform, TrenchcoatBody.TorsoRest, new Vector3(0.7f, 0.6f, 0.5f), coat);
            var head = Visual(PrimitiveType.Sphere, "Head", root.transform, TrenchcoatBody.HeadRest, Vector3.one * 0.35f, skin);
            Visual(PrimitiveType.Cube, "Hat", head, new Vector3(0f, 0.55f, 0f), new Vector3(1.1f, 0.4f, 1.1f), coat);
            Visual(PrimitiveType.Cube, "Nose", head, new Vector3(0f, 0f, 0.5f), new Vector3(0.2f, 0.2f, 0.3f), skin);
            var left = Visual(PrimitiveType.Sphere, "LeftHand", root.transform, TrenchcoatBody.LeftShoulder + Vector3.down * 0.75f, Vector3.one * 0.18f, skin);
            var right = Visual(PrimitiveType.Sphere, "RightHand", root.transform, TrenchcoatBody.RightShoulder + Vector3.down * 0.75f, Vector3.one * 0.18f, skin);
            var leftArm = Visual(PrimitiveType.Cylinder, "LeftArm", root.transform, TrenchcoatBody.LeftShoulder, new Vector3(0.09f, 0.37f, 0.09f), coat);
            var rightArm = Visual(PrimitiveType.Cylinder, "RightArm", root.transform, TrenchcoatBody.RightShoulder, new Vector3(0.09f, 0.37f, 0.09f), coat);
            var leftLeg = Leg("LeftLeg", root.transform, TrenchcoatBody.LeftHip, pants);
            var rightLeg = Leg("RightLeg", root.transform, TrenchcoatBody.RightHip, pants);

            var body = root.AddComponent<TrenchcoatBody>();
            root.AddComponent<HandGrabber>();

            // Online: the host simulates; clients get position/rotation interpolated plus a visual state.
            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.SyncScaleX = netTransform.SyncScaleY = netTransform.SyncScaleZ = false;
            netTransform.Interpolate = true;
            root.AddComponent<NetworkRigidbody>();
            root.AddComponent<NetworkedTrenchcoat>().RaccoonPrefab = raccoonPrefab.GetComponent<NetworkObject>();
            IgnoreForNavigation(root);
            body.Torso = torso;
            body.Head = head;
            body.LeftHand = left;
            body.RightHand = right;
            body.LeftArm = leftArm;
            body.RightArm = rightArm;
            body.LeftLeg = leftLeg;
            body.RightLeg = rightLeg;
            return body;
        }

        /// <summary>A hip pivot with the visible leg hanging below it, so rotating the pivot swings the leg.</summary>
        static Transform Leg(string name, Transform parent, Vector3 hip, Material mat)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(parent, false);
            pivot.localPosition = hip;
            Visual(PrimitiveType.Capsule, "Mesh", pivot, new Vector3(0f, -0.33f, 0f), new Vector3(0.2f, 0.33f, 0.2f), mat);
            return pivot;
        }

        static RaccoonController BuildRaccoonPrefab(Material fur)
        {
            var root = new GameObject("Raccoon");
            var cc = root.AddComponent<CharacterController>();
            cc.height = 0.6f;
            cc.radius = 0.2f;
            cc.center = new Vector3(0f, 0.3f, 0f);
            // A greybox raccoon you can read at a glance: grey body, black mask, ears, ringed tail, big eyes,
            // and a bandana in the player's color. Everything hangs from a pivot at the feet (squash & stretch).
            var mask = Mat("RaccoonMask", new Color(0.12f, 0.12f, 0.14f));
            var white = Mat("EyeWhite", new Color(0.97f, 0.97f, 0.97f));
            var light = Mat("RaccoonLight", new Color(0.78f, 0.78f, 0.8f));
            var bandanaMat = Mat("Bandana", new Color(0.9f, 0.25f, 0.25f));
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            Visual(PrimitiveType.Capsule, "Body", visual, new Vector3(0f, 0.27f, 0f), new Vector3(0.4f, 0.27f, 0.4f), fur);
            Visual(PrimitiveType.Sphere, "Belly", visual, new Vector3(0f, 0.24f, 0.12f), new Vector3(0.26f, 0.3f, 0.18f), light);
            var head = new GameObject("Head").transform;
            head.SetParent(visual, false);
            head.localPosition = new Vector3(0f, 0.5f, 0.05f);
            Visual(PrimitiveType.Sphere, "Skull", head, Vector3.zero, new Vector3(0.34f, 0.28f, 0.3f), fur);
            Visual(PrimitiveType.Sphere, "Snout", head, new Vector3(0f, -0.04f, 0.15f), new Vector3(0.14f, 0.1f, 0.14f), light);
            Visual(PrimitiveType.Sphere, "Nose", head, new Vector3(0f, -0.02f, 0.22f), Vector3.one * 0.045f, mask);
            Visual(PrimitiveType.Cube, "Mask", head, new Vector3(0f, 0.025f, 0.11f), new Vector3(0.32f, 0.07f, 0.1f), mask);
            Visual(PrimitiveType.Sphere, "EarL", head, new Vector3(-0.12f, 0.14f, -0.02f), new Vector3(0.09f, 0.1f, 0.05f), fur);
            Visual(PrimitiveType.Sphere, "EarR", head, new Vector3(0.12f, 0.14f, -0.02f), new Vector3(0.09f, 0.1f, 0.05f), fur);
            var pupils = new Transform[2];
            var lids = new Transform[2];
            for (int e = 0; e < 2; e++)
            {
                float x = e == 0 ? -0.07f : 0.07f;
                Visual(PrimitiveType.Sphere, e == 0 ? "EyeL" : "EyeR", head, new Vector3(x, 0.03f, 0.145f), Vector3.one * 0.075f, white);
                pupils[e] = Visual(PrimitiveType.Sphere, e == 0 ? "PupilL" : "PupilR", head, new Vector3(x, 0.03f, 0.175f), Vector3.one * 0.038f, mask);
                lids[e] = Visual(PrimitiveType.Cube, e == 0 ? "LidL" : "LidR", head, new Vector3(x, 0.06f, 0.18f), new Vector3(0.085f, 0.001f, 0.02f), fur);
            }
            var bandana = Visual(PrimitiveType.Cylinder, "Bandana", visual, new Vector3(0f, 0.38f, 0.01f), new Vector3(0.44f, 0.035f, 0.44f), bandanaMat);
            var tail = new GameObject("Tail").transform;
            tail.SetParent(visual, false);
            tail.localPosition = new Vector3(0f, 0.18f, -0.18f);
            for (int t = 0; t < 4; t++)
                Visual(PrimitiveType.Sphere, $"Ring_{t}", tail, new Vector3(0f, t * 0.045f, -0.06f - t * 0.075f), new Vector3(0.12f, 0.12f, 0.1f) * (1f - t * 0.08f), t % 2 == 0 ? fur : mask);
            // Four little paws on the ground (they stay under the body whatever the pose).
            var paws = new Transform[4];
            var pawPos = new[] { new Vector3(-0.1f, 0.04f, 0.1f), new Vector3(0.1f, 0.04f, 0.1f), new Vector3(-0.1f, 0.04f, -0.1f), new Vector3(0.1f, 0.04f, -0.1f) };
            for (int i = 0; i < 4; i++)
                paws[i] = Visual(PrimitiveType.Sphere, $"Paw_{i}", root.transform, pawPos[i], new Vector3(0.09f, 0.07f, 0.11f), mask);
            var look = root.AddComponent<RaccoonLook>();
            look.Head = head;
            look.Paws = paws;
            look.Visual = visual;
            look.Tail = tail;
            look.Pupils = pupils;
            look.Lids = lids;
            look.Bandana = bandana.GetComponent<Renderer>();
            AttachRaccoonModel(visual, head, tail, paws, bandana, look);
            root.AddComponent<RaccoonController>();
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner; // the owner moves it, instantly
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            root.AddComponent<NetworkedRaccoon>();
            IgnoreForNavigation(root);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            // Prefab NetworkObjects take their id from the saved asset: validate and save again.
            var validate = typeof(NetworkObject).GetMethod("OnValidate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            validate?.Invoke(prefab.GetComponent<NetworkObject>(), null);
            EditorUtility.SetDirty(prefab);
            PrefabUtility.SavePrefabAsset(prefab);
            return prefab.GetComponent<RaccoonController>();
        }

        const string RaccoonArt = "Assets/Art/Raccoon";
        public const float RaccoonModelScale = 0.72f;

        /// <summary>
        /// The real raccoon (Meshy model, rigged, 11 clips) in place of the greybox shapes. The greybox transforms stay
        /// (head, tail, paws drive nothing now but keep old code happy); only the player-colored bandana stays visible.
        /// Without the model asset the greybox raccoon is kept.
        /// </summary>
        static void AttachRaccoonModel(Transform visual, Transform head, Transform tail, Transform[] paws, Transform bandana, RaccoonLook look)
        {
            AssetDatabase.ImportAsset(TrashPandas.EditorTools.RaccoonModelImport.ModelPath, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(TrashPandas.EditorTools.RaccoonModelImport.ModelPath);
            if (!model) { Debug.LogWarning("[GreyboxSceneBuilder] no raccoon model — keeping the greybox raccoon"); return; }

            // URP material with the baked texture.
            string texPath = RaccoonArt + "/RaccoonSkin.png";
            AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceSynchronousImport);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            string matPath = RaccoonArt + "/RaccoonSkin.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.12f);
            EditorUtility.SetDirty(mat);

            // One state per clip; RaccoonLook cross-fades by name.
            string ctrlPath = RaccoonArt + "/Raccoon.controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = ctrl.layers[0].stateMachine;
            var clips = AssetDatabase.LoadAllAssetsAtPath(TrashPandas.EditorTools.RaccoonModelImport.ModelPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
            foreach (var clip in clips)
            {
                var st = sm.AddState(clip.name);
                st.motion = clip;
                if (clip.name == "Idle") sm.defaultState = st;
            }
            Debug.Log($"[GreyboxSceneBuilder] raccoon clips: {string.Join(", ", clips.Select(c => c.name))} | takes: {string.Join(", ", ((ModelImporter)AssetImporter.GetAtPath(TrashPandas.EditorTools.RaccoonModelImport.ModelPath)).defaultClipAnimations.Select(c => c.takeName))}");

            var inst = (GameObject)Object.Instantiate(model, visual);
            inst.name = "Model";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localScale = Vector3.one * RaccoonModelScale;
            inst.transform.localRotation = Quaternion.Euler(0f, RaccoonYaw, 0f); // our own Blender rig (tools/rig_raccoon.py)
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.sharedMaterial = mat;
                smr.updateWhenOffscreen = true;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            var anim = inst.GetComponent<Animator>();
            if (!anim) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            look.Model = anim;

            // Hide the greybox shapes (keep the transforms) — but keep the bandana in the player's color, fitted to the neck.
            foreach (Transform child in visual)
                if (child != inst.transform && child != bandana && child != head && child != tail) child.gameObject.SetActive(false);
            foreach (Transform child in head) child.gameObject.SetActive(false);
            foreach (Transform child in tail) child.gameObject.SetActive(false);
            foreach (var p in paws) p.gameObject.SetActive(false);
            // The player's color is the model's own bandana, recolored per player (tools: RaccoonSkin_P1..P4.png).
            bandana.gameObject.SetActive(false);
            var skins = new Material[5];
            skins[0] = mat;
            for (int p = 1; p < 5; p++)
            {
                string tp = $"{RaccoonArt}/RaccoonSkin_P{p}.png";
                AssetDatabase.ImportAsset(tp, ImportAssetOptions.ForceSynchronousImport);
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
                string mp = $"{RaccoonArt}/RaccoonSkin_P{p}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (!m) { m = new Material(mat); AssetDatabase.CreateAsset(m, mp); }
                m.CopyPropertiesFromMaterial(mat);
                if (t) m.SetTexture("_BaseMap", t);
                EditorUtility.SetDirty(m);
                skins[p] = m;
            }
            look.Skins = skins;
        }

        /// <summary>
        /// A Meshy character from Assets/Art/Characters/&lt;name&gt;/&lt;name&gt;.fbx (+ &lt;name&gt;.png): URP material, one animator
        /// state per clip, driven by NpcModelAnimator. Returns null when the art isn't there (greybox stays).
        /// </summary>
        static GameObject AttachCharacterModel(string name, Transform parent, NpcPawn pawn)
        {
            string dir = TrashPandas.EditorTools.RaccoonModelImport.CharactersDir + name;
            string fbx = $"{dir}/{name}.fbx";
            AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceSynchronousImport);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (!model) return null;
            AssetDatabase.ImportAsset($"{dir}/{name}.png", ImportAssetOptions.ForceSynchronousImport);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{name}.png");
            string matPath = $"{dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(mat);
            string ctrlPath = $"{dir}/{name}.controller";
            AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")))
            {
                var st = sm.AddState(clip.name);
                st.motion = clip;
                if (clip.name == "Idle") sm.defaultState = st;
            }
            var inst = (GameObject)Object.Instantiate(model, parent);
            inst.name = "Model";
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localScale = Vector3.one;
            FaceForward(inst.transform);
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>()) { smr.sharedMaterial = mat; smr.updateWhenOffscreen = true; }
            var anim = inst.GetComponent<Animator>();
            if (!anim) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            inst.AddComponent<TrashPandas.Runtime.Npc.NpcModelAnimator>().Pawn = pawn;
            return inst;
        }

        /// <summary>
        /// Meshy models come in facing +X (their "headfront" bone does NOT point at the face — trusting it made the
        /// raccoon crab-walk): turn them a quarter so they look along the parent's +Z. Verified in game with the
        /// "keyd" bot (moving right must show the right-hand profile, not the face).
        /// </summary>
        public static float MeshyYaw = -90f;
        /// <summary>Our Blender-rigged raccoon (faces -Y in Blender, exported -Z forward / Y up).</summary>
        public static float RaccoonYaw = 0f;
        static void FaceForward(Transform model)
        {
            model.localRotation = Quaternion.Euler(0f, MeshyYaw, 0f);
        }

        static GameObject Prop(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material mat, float mass, bool bothHands)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (type == PrimitiveType.Cylinder)
            {
                // Cylinder primitives use a capsule collider; a box keeps flat things (plates) from rolling away.
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.AddComponent<BoxCollider>();
            }
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            go.AddComponent<Grabbable>().RequiresBothHands = bothHands;

            // Online: the host simulates and grabs; everyone sees the same glass fly.
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            go.AddComponent<NetworkRigidbody>();
            IgnoreForNavigation(go);
            return go;
        }

        /// <summary>Things that move (coat, raccoons, props) must not carve permanent holes into the baked NavMesh.</summary>
        static void IgnoreForNavigation(GameObject go)
        {
            var modifier = go.AddComponent<NavMeshModifier>();
            modifier.ignoreFromBuild = true;
            modifier.applyToChildren = true;
        }

        static GameObject Box(string name, Vector3 position, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static Transform Visual(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        static PhysicsMaterial FrictionlessMaterial()
        {
            string path = $"{MaterialDir}/CoatFrictionless.physicMaterial";
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (mat == null)
            {
                mat = new PhysicsMaterial("CoatFrictionless");
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.dynamicFriction = 0f;
            mat.staticFriction = 0f;
            mat.frictionCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material Mat(string name, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
