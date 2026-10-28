using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

/// <summary>Builds a headless standalone IL2CPP Player that runs generated serialization code.</summary>
public static class LuminPackSmokeBuild
{
    /// <summary>Builds the desktop platform and backend selected by the native CI runner.</summary>
    public static void BuildConfigured()
    {
        var target = (BuildTarget)System.Enum.Parse(typeof(BuildTarget), System.Environment.GetEnvironmentVariable("LUMINPACK_UNITY_TARGET"));
        var backend = (ScriptingImplementation)System.Enum.Parse(typeof(ScriptingImplementation), System.Environment.GetEnvironmentVariable("LUMINPACK_UNITY_BACKEND"));
        if (target == BuildTarget.StandaloneOSX)
            EditorUserBuildSettings.SetPlatformSettings("OSXUniversal", "Architecture",
                System.Environment.GetEnvironmentVariable("LUMINPACK_UNITY_ARCH") == "arm64" ? "ARM64" : "x64");
        BuildPlayer(target, backend);
    }

    /// <summary>Creates a scene, selects IL2CPP and fails the CI process if the Player build fails.</summary>
    public static void Build()
        => BuildPlayer(BuildTarget.StandaloneLinux64, ScriptingImplementation.IL2CPP);

    /// <summary>Builds a Windows x64 IL2CPP Player for local execution.</summary>
    public static void BuildWindows() => BuildPlayer(BuildTarget.StandaloneWindows64, ScriptingImplementation.IL2CPP);

    /// <summary>Builds a Windows x86 IL2CPP Player for real 32-bit layout validation.</summary>
    public static void BuildWindows32() => BuildPlayer(BuildTarget.StandaloneWindows, ScriptingImplementation.IL2CPP);

    /// <summary>Builds both Windows IL2CPP architectures from the same staged assemblies.</summary>
    public static void BuildWindowsBoth()
    {
        BuildWindows();
        BuildWindows32();
    }

    /// <summary>Executes Mono validation synchronously before building both native Player architectures.</summary>
    public static void VerifyAndBuildWindowsBoth()
    {
        LuminPackSmoke.Verify();
        UnityEngine.Debug.Log("LUMINPACK_MONO_SMOKE_PASSED");
        BuildWindowsBoth();
    }

    /// <summary>Builds a Windows Mono Player for cross-runtime fixture execution.</summary>
    public static void BuildWindowsMono() => BuildPlayer(BuildTarget.StandaloneWindows64, ScriptingImplementation.Mono2x);

    /// <summary>Builds the smoke graph using the selected native architecture and scripting backend.</summary>
    /// <param name="target">The installed standalone target.</param>
    /// <param name="backend">Mono or IL2CPP execution.</param>
    private static void BuildPlayer(BuildTarget target, ScriptingImplementation backend)
    {
        string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
        Directory.CreateDirectory(Path.Combine(projectRoot, "Assets/Scenes"));
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Smoke.unity");
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, backend);
        PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_Standard_2_0);
        string outputDirectory = Path.Combine(projectRoot, "build", target + "_" + backend);
        Directory.CreateDirectory(outputDirectory);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Smoke.unity" },
            locationPathName = Path.Combine(outputDirectory, target == BuildTarget.StandaloneLinux64 ? "LuminPackSmoke" : target == BuildTarget.StandaloneOSX ? "LuminPackSmoke.app" : "LuminPackSmoke.exe"),
            target = target,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.InvalidOperationException("The IL2CPP smoke Player did not build successfully.");
    }
}
