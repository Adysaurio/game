using System.IO;
using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Raccoon;
using TrashPandas.Runtime.Trenchcoat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TrashPandas.EditorTools
{
    /// <summary>Builds the greybox test scene deterministically. Safe to re-run.</summary>
    public static class GreyboxSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Greybox_Trenchcoat.unity";
        const string PrefabPath = "Assets/Prefabs/Raccoon.prefab";
        const string MaterialDir = "Assets/Materials/Greybox";

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

            var body = BuildTrenchcoat(coat, skin, pants);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 3f, -5f);
            var follow = camGo.AddComponent<FollowCamera>();

            var controller = new GameObject("TrenchcoatController").AddComponent<TrenchcoatController>();
            controller.PlayerCount = 3;
            controller.Body = body;
            controller.RaccoonPrefab = raccoonPrefab;
            controller.Camera = follow;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[GreyboxSceneBuilder] Built {ScenePath}");
        }

        static TrenchcoatBody BuildTrenchcoat(Material coat, Material skin, Material pants)
        {
            var root = new GameObject("Trenchcoat");
            root.transform.position = new Vector3(0f, 0.05f, 0f);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 60f;
            var col = root.AddComponent<CapsuleCollider>();
            col.height = 2.1f;
            col.radius = 0.35f;
            col.center = new Vector3(0f, 1.05f, 0f);

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

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<RaccoonController>();
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
