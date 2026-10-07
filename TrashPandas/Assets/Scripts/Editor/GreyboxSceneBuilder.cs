using System.IO;
using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Grabbing;
using TrashPandas.Runtime.Net;
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
    public static class GreyboxSceneBuilder
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
            net.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
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
            var wallet = Mat("Wallet", new Color(0.35f, 0.2f, 0.12f));
            var cake = Mat("Cake", new Color(1f, 0.92f, 0.8f));

            var raccoonPrefab = BuildRaccoonPrefab(fur);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            light.intensity = 1.2f;

            Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(40f, 1f, 40f), grass);

            // Tables: the coat can't fit under them; a crouched raccoon can (gap 0.75 m, raccoon crouch 0.3 m).
            for (int i = 0; i < 6; i++)
            {
                var pos = new Vector3(-6f + (i % 3) * 6f, 0f, 4f + (i / 3) * 5f);
                var table = new GameObject($"Table_{i}");
                table.transform.position = pos;
                Box("Top", pos + new Vector3(0f, 0.8f, 0f), new Vector3(2f, 0.1f, 1.2f), wood).transform.SetParent(table.transform, true);
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
                Prop($"Wallet_{i}", PrimitiveType.Cube, t + new Vector3(0.75f, tableTop + 0.03f, 0.2f), new Vector3(0.2f, 0.05f, 0.12f), wallet, 0.3f, false);
            }
            var cakeGo = Prop("Cake", PrimitiveType.Cylinder, new Vector3(0f, tableTop + 0.2f, 8.6f), new Vector3(0.5f, 0.2f, 0.5f), cake, 3f, true);
            Visual(PrimitiveType.Cylinder, "Tier2", cakeGo.transform, new Vector3(0f, 1.4f, 0f), new Vector3(0.65f, 0.6f, 0.65f), cake);

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

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var outputCamera = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 3f, -5f);
            camGo.AddComponent<CinemachineBrain>().UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            var rig = BuildPlayerCamera(outputCamera, body.transform);


            var controller = new GameObject("TrenchcoatController").AddComponent<TrenchcoatController>();
            controller.PlayerCount = 3;
            controller.Body = body;
            controller.RaccoonPrefab = raccoonPrefab;
            controller.CameraRig = rig;

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
            Visual(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.4f, 0.3f, 0.4f), fur);
            Visual(PrimitiveType.Sphere, "Snout", root.transform, new Vector3(0f, 0.4f, 0.22f), Vector3.one * 0.12f, fur);
            root.AddComponent<RaccoonController>();
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner; // the owner moves it, instantly
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            root.AddComponent<NetworkedRaccoon>();

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
            return go;
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
