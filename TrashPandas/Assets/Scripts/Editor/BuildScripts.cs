using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TrashPandas.EditorTools
{
    /// <summary>Player builds for playtests (macOS for the developer, Windows for friends).</summary>
    public static class BuildScripts
    {
        static string[] Scenes => new[] { "Assets/Scenes/Menu.unity", "Assets/Scenes/Greybox_Trenchcoat.unity" };

        [MenuItem("TrashPandas/Build macOS (dev)")]
        public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "../Builds/mac/TrashPandas.app");

        [MenuItem("TrashPandas/Build Windows (dev)")]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "../Builds/windows/TrashPandas.exe");

        const string IconPath = "Assets/Art/Icon/AppIcon.png";

        /// <summary>The app icon (the raccoon with the red bandana) for every platform.</summary>
        static void ApplyIcon()
        {
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(IconPath) is TextureImporter ti)
            {
                ti.textureType = TextureImporterType.Default;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.maxTextureSize = 1024;
                ti.SaveAndReimport();
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (!icon) { Debug.LogWarning($"[BuildScripts] no icon at {IconPath}"); return; }
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }

        static void Build(BuildTarget target, string path)
        {
            PlayerSettings.runInBackground = true; // several local instances must keep simulating unfocused
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            ApplyIcon();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = path,
                target = target,
                options = BuildOptions.Development,
            });
            Debug.Log($"[BuildScripts] {target}: {report.summary.result}, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors");
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
