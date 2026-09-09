using System.IO;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class GooseMergeDemoBuild
{
    private const string ScenePath = "Assets/Scenes/GooseMergeDemo.unity";
    private const string OutputRoot = "Builds/GooseMergeDemo";

    [MenuItem("Tools/Demo/Build Goose Merge Demo/Windows")]
    public static void BuildWindows()
    {
        BuildTarget target = BuildTarget.StandaloneWindows64;
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, target);
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        HybridCLRSettings.Instance.enable = false;
        HybridCLRSettings.Save();
        Debug.Log($"Goose Merge Demo scripting backend: {PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone)}");
        string outputDir = Path.GetFullPath(OutputRoot);
        Directory.CreateDirectory(outputDir);

        string exePath = Path.Combine(outputDir, "GooseMergeDemo.exe");
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = exePath,
            target = target,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"Goose Merge Demo build failed: {report.summary.result}");
            return;
        }

        Debug.Log($"Goose Merge Demo build complete: {exePath}");
    }
}
