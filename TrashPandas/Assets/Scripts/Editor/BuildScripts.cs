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

        static void Build(BuildTarget target, string path)
        {
            PlayerSettings.runInBackground = true; // several local instances must keep simulating unfocused
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
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
