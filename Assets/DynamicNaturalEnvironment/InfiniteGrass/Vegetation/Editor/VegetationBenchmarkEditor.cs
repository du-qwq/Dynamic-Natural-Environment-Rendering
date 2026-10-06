using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal static class VegetationBenchmarkEditor
{
    private const string ScenePath = "Assets/Scenes/Environment_Showcase.unity";

    [MenuItem("Tools/Vegetation/B1 Run Editor Smoke")]
    private static void RunSmoke()
    {
        StartInPlayMode(true);
    }

    [MenuItem("Tools/Vegetation/B1 Run Full Benchmark In Editor")]
    private static void RunFullInEditor()
    {
        StartInPlayMode(false);
    }

    private static void StartInPlayMode(bool smoke)
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[VegetationBenchmark] Load Environment_Showcase and enter Play Mode before starting an Editor run.");
            return;
        }
        VegetationRenderer renderer = Object.FindObjectOfType<VegetationRenderer>();
        if (renderer == null) { Debug.LogError("[VegetationBenchmark] No VegetationRenderer in the active scene."); return; }
        var diagnostic = renderer.GetComponent<VegetationPerformanceDiagnostic>();
        if (diagnostic == null) diagnostic = renderer.gameObject.AddComponent<VegetationPerformanceDiagnostic>();
        diagnostic.vegetationRenderer = renderer;
        if (smoke) diagnostic.StartBenchmarkSmoke();
        else diagnostic.StartBenchmark();
    }

    [MenuItem("Tools/Vegetation/B1 Build Development Player")]
    private static void BuildDevelopment()
    {
        Build(true);
    }

    [MenuItem("Tools/Vegetation/B1 Build Release Player")]
    private static void BuildRelease()
    {
        Build(false);
    }

    private static void Build(bool development)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folder = Path.Combine(projectRoot, "Builds", "VegetationBenchmark", development ? "Development" : "Release");
        Directory.CreateDirectory(folder);
        string playerPath = Path.Combine(folder, "VegetationBenchmark.exe");
        bool oldFrameTiming = PlayerSettings.enableFrameTimingStats;
        try
        {
            PlayerSettings.enableFrameTimingStats = true;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = playerPath,
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None
            });
            if (report.summary.result == BuildResult.Succeeded)
                Debug.Log("[VegetationBenchmark] Build ready: " + playerPath +
                    " | Run with -vegetationBenchmark for the full matrix, -vegetationBenchmarkCore100k for one core case, -vegetationBenchmarkGPUCull100k for the GPU cull validation case, or -vegetationBenchmarkSupplemental for the five B1 replacement/comparison rows. CSV is written to Application.persistentDataPath/VegetationBenchmark.");
            else
                Debug.LogError("[VegetationBenchmark] Build failed: " + report.summary.result);
        }
        finally
        {
            PlayerSettings.enableFrameTimingStats = oldFrameTiming;
            EditorApplication.ExecuteMenuItem("File/Save Project");
        }
    }
}
