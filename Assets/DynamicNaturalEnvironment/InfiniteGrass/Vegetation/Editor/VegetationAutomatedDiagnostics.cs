// Diagnostics/Test Only. This file is not used by player builds and does not change runtime rendering logic.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace VegetationDiagnosticsTools
{
    [InitializeOnLoad]
    internal static class VegetationAutomatedDiagnostics
    {
        private const string RequestRelativePath = "Logs/VegetationDiagnostics/START.request";
        private const string StateRelativePath = "Logs/VegetationDiagnostics/run-state.json";
        private const string HarnessVersion = "5.1";

        [Serializable]
        private sealed class RunState
        {
            public string runId;
            public int groupIndex;
            public string status;
            public string activeGroup;
            public string lastStep;
            public string startedUtc;
            public bool completed;
        }

        private sealed class GroupConfig
        {
            public readonly string name;
            public readonly bool disableSceneForward;
            public readonly bool disableSceneShadow;
            public readonly bool disableForward;
            public readonly bool disableShadow;
            public readonly bool disableGpuCulling;
            public readonly SceneExerciseMode sceneMode;

            public GroupConfig(
                string name,
                bool disableSceneForward = false,
                bool disableSceneShadow = false,
                bool disableForward = false,
                bool disableShadow = false,
                bool disableGpuCulling = false,
                SceneExerciseMode sceneMode = SceneExerciseMode.Normal)
            {
                this.name = name;
                this.disableSceneForward = disableSceneForward;
                this.disableSceneShadow = disableSceneShadow;
                this.disableForward = disableForward;
                this.disableShadow = disableShadow;
                this.disableGpuCulling = disableGpuCulling;
                this.sceneMode = sceneMode;
            }
        }

        private enum SceneExerciseMode
        {
            Normal,
            GameOnly,
            ForcedSceneRepaint
        }

        private static readonly GroupConfig[] Groups =
        {
            new GroupConfig("A_BASELINE"),
            new GroupConfig("B_NO_SCENE_FORWARD", disableSceneForward: true),
            new GroupConfig("C_NO_SCENE_VEGETATION", disableSceneForward: true, disableSceneShadow: true),
            new GroupConfig("D_NO_SHADOW", disableShadow: true),
            new GroupConfig("E_NO_FORWARD", disableForward: true),
            new GroupConfig("F_NO_GPU_CULLING", disableGpuCulling: true),
            new GroupConfig("G_NO_SCENE_SHADOW", disableSceneShadow: true),
            new GroupConfig("H_GAME_ONLY", sceneMode: SceneExerciseMode.GameOnly),
            new GroupConfig("I_GAME_PLUS_SCENE", sceneMode: SceneExerciseMode.ForcedSceneRepaint)
        };

        private static readonly object FileLock = new object();
        private static RunState state;
        private static GroupRunner runner;
        private static double nextBootstrapTime;
        private static bool unexpectedStopHandled;

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        private static string RequestPath => Path.Combine(ProjectRoot, RequestRelativePath.Replace('/', Path.DirectorySeparatorChar));
        private static string StatePath => Path.Combine(ProjectRoot, StateRelativePath.Replace('/', Path.DirectorySeparatorChar));
        private static string RunDirectory => state == null || string.IsNullOrEmpty(state.runId)
            ? Path.Combine(ProjectRoot, "Logs", "VegetationDiagnostics", "unknown")
            : Path.Combine(ProjectRoot, "Logs", "VegetationDiagnostics", state.runId);

        static VegetationAutomatedDiagnostics()
        {
            EditorApplication.update -= BootstrapUpdate;
            EditorApplication.update += BootstrapUpdate;
            Application.logMessageReceivedThreaded -= OnLogMessage;
            Application.logMessageReceivedThreaded += OnLogMessage;
            nextBootstrapTime = EditorApplication.timeSinceStartup + 1.0;
        }

        private static void BootstrapUpdate()
        {
            if (!File.Exists(RequestPath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (EditorApplication.timeSinceStartup < nextBootstrapTime) return;

            if (state == null) state = LoadOrCreateState();
            if (state.completed) return;

            if (EditorApplication.isPlaying)
            {
                unexpectedStopHandled = false;
                if (runner == null)
                {
                    if (state.groupIndex < 0 || state.groupIndex >= Groups.Length)
                    {
                        CompleteRun();
                        return;
                    }

                    state.status = "running";
                    state.activeGroup = Groups[state.groupIndex].name;
                    SaveState();
                    runner = new GroupRunner(Groups[state.groupIndex]);
                }

                runner.Update();
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (!unexpectedStopHandled && string.Equals(state.status, "running", StringComparison.Ordinal))
            {
                unexpectedStopHandled = true;
                AppendCrashRecovery("Editor returned to Edit Mode before TEST_END. Treating the active group as interrupted.");
                state.groupIndex++;
                state.status = "idle";
                state.activeGroup = string.Empty;
                SaveState();
                nextBootstrapTime = EditorApplication.timeSinceStartup + 2.0;
                return;
            }

            if (string.Equals(state.status, "group_complete", StringComparison.Ordinal))
            {
                state.status = "idle";
                state.activeGroup = string.Empty;
                SaveState();
                nextBootstrapTime = EditorApplication.timeSinceStartup + 2.0;
                return;
            }

            if (state.groupIndex >= Groups.Length)
            {
                CompleteRun();
                return;
            }

            BeginNextGroup();
        }

        private static RunState LoadOrCreateState()
        {
            try
            {
                if (File.Exists(StatePath))
                {
                    RunState loaded = JsonUtility.FromJson<RunState>(File.ReadAllText(StatePath));
                    if (loaded != null && !string.IsNullOrEmpty(loaded.runId)) return loaded;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[VegetationAutoDiag] Could not read prior state: {exception.Message}");
            }

            string request = File.Exists(RequestPath) ? File.ReadAllText(RequestPath).Trim() : string.Empty;
            string runId = string.IsNullOrEmpty(request)
                ? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                : SanitizeFileName(request);
            RunState created = new RunState
            {
                runId = runId,
                groupIndex = 0,
                status = "idle",
                activeGroup = string.Empty,
                lastStep = "INITIALIZED",
                startedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                completed = false
            };
            state = created;
            Directory.CreateDirectory(RunDirectory);
            SaveState();
            AppendDurable(Path.Combine(RunDirectory, "run.log"),
                $"RUN_BEGIN|UTC={created.startedUtc}|Harness={HarnessVersion}|Unity={Application.unityVersion}|Graphics={SystemInfo.graphicsDeviceType}|GPU={SystemInfo.graphicsDeviceName}");
            return created;
        }

        private static void BeginNextGroup()
        {
            GroupConfig group = Groups[state.groupIndex];
            state.status = "starting";
            state.activeGroup = group.name;
            state.lastStep = "TEST_BEGIN";
            SaveState();
            AppendGroup(group.name,
                $"TEST_BEGIN|Group={group.name}|Index={state.groupIndex}|UTC={DateTime.UtcNow:O}|" +
                $"SceneForward={!group.disableSceneForward}|SceneShadow={!group.disableSceneShadow}|" +
                $"Forward={!group.disableForward}|Shadow={!group.disableShadow}|GPUCulling={!group.disableGpuCulling}|SceneMode={group.sceneMode}");
            nextBootstrapTime = EditorApplication.timeSinceStartup + 1.0;
            EditorApplication.isPlaying = true;
        }

        private static void CompleteGroup(string groupName)
        {
            AppendGroup(groupName, $"TEST_END|Group={groupName}|UTC={DateTime.UtcNow:O}");
            state.groupIndex++;
            state.status = "group_complete";
            state.activeGroup = string.Empty;
            state.lastStep = "TEST_END";
            SaveState();
            runner = null;
            EditorApplication.isPlaying = false;
        }

        private static void CompleteRun()
        {
            GameViewResolution.RemoveDiagnosticSizes();
            state.completed = true;
            state.status = "complete";
            state.lastStep = "RUN_COMPLETE";
            SaveState();
            string completePath = Path.Combine(RunDirectory, "RUN_COMPLETE.txt");
            AppendDurable(completePath,
                $"RUN_COMPLETE|UTC={DateTime.UtcNow:O}|Groups={Groups.Length}|Harness={HarnessVersion}");
            AppendDurable(Path.Combine(RunDirectory, "run.log"),
                $"RUN_COMPLETE|UTC={DateTime.UtcNow:O}|Groups={Groups.Length}");
            Debug.Log($"[VegetationAutoDiag] Automated diagnostics complete: {RunDirectory}");
        }

        private static void AppendCrashRecovery(string reason)
        {
            string group = string.IsNullOrEmpty(state.activeGroup) ? "UNKNOWN" : state.activeGroup;
            AppendGroup(group,
                $"TEST_INTERRUPTED|Group={group}|LastStep={state.lastStep}|UTC={DateTime.UtcNow:O}|Reason={Escape(reason)}");
            AppendDurable(Path.Combine(RunDirectory, $"{group}.ERROR.log"),
                $"TEST_INTERRUPTED|LastStep={state.lastStep}|Reason={Escape(reason)}");
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (state == null || string.IsNullOrEmpty(state.runId) || string.IsNullOrEmpty(condition)) return;
            bool vegetation = condition.IndexOf("VegetationDiag", StringComparison.OrdinalIgnoreCase) >= 0;
            bool critical = condition.IndexOf("[CRITICAL]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            condition.IndexOf("Category B", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            condition.IndexOf("Category C", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            condition.IndexOf("Category D", StringComparison.OrdinalIgnoreCase) >= 0;
            bool deviceRemoved = IsDeviceRemovedMessage(condition);
            if (!vegetation && !deviceRemoved) return;

            string group = string.IsNullOrEmpty(state.activeGroup) ? "NO_ACTIVE_GROUP" : state.activeGroup;
            string record = $"UNITY_LOG|UTC={DateTime.UtcNow:O}|Type={type}|Group={group}|Message={Escape(condition)}";
            AppendGroup(group, record);

            if (critical || deviceRemoved)
            {
                AppendDurable(Path.Combine(RunDirectory, $"{group}.ERROR.log"),
                    record + "|Stack=" + Escape(stackTrace));
                if (runner != null) runner.NotifyCritical(deviceRemoved);
            }
        }

        private static bool IsDeviceRemovedMessage(string message)
        {
            return message.IndexOf("device removed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("DXGI_ERROR_DEVICE_REMOVED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("D3D11 device", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("graphics device is null", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("failed to present", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Checkpoint(string group, string step, string details = "")
        {
            state.lastStep = step;
            SaveState();
            AppendGroup(group,
                $"TEST_STEP|Group={group}|Step={step}|UTC={DateTime.UtcNow:O}" +
                (string.IsNullOrEmpty(details) ? string.Empty : "|" + details));
        }

        private static void SaveState()
        {
            if (state == null) return;
            string directory = Path.GetDirectoryName(StatePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            lock (FileLock)
            {
                using (FileStream stream = new FileStream(StatePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.Write(JsonUtility.ToJson(state, true));
                    writer.Flush();
                    stream.Flush(true);
                }
            }
        }

        private static void AppendGroup(string group, string line)
        {
            AppendDurable(Path.Combine(RunDirectory, $"{SanitizeFileName(group)}.log"), line);
        }

        private static void AppendDurable(string path, string line)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                lock (FileLock)
                {
                    using (FileStream stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (StreamWriter writer = new StreamWriter(stream))
                    {
                        writer.WriteLine(line);
                        writer.Flush();
                        stream.Flush(true);
                    }
                }
            }
            catch
            {
                // The harness must never turn a logging failure into a rendering failure.
            }
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\r", " ").Replace("\n", " ").Replace("|", "/");
        }

        private sealed class GroupRunner
        {
            private sealed class Step
            {
                public readonly string name;
                public readonly double duration;
                public readonly Action action;
                public readonly bool capture;

                public Step(string name, double duration, Action action, bool capture = false)
                {
                    this.name = name;
                    this.duration = duration;
                    this.action = action;
                    this.capture = capture;
                }
            }

            private sealed class MarkerSample
            {
                public readonly string name;
                public readonly string category;
                public ProfilerRecorder recorder;
                public long count;
                public double totalMilliseconds;
                public double maximumMilliseconds;

                public MarkerSample(string name, string category, ProfilerRecorder recorder)
                {
                    this.name = name;
                    this.category = category;
                    this.recorder = recorder;
                }

                public void Sample()
                {
                    if (!recorder.Valid) return;
                    long nanoseconds = recorder.LastValue;
                    if (nanoseconds <= 0) return;
                    double milliseconds = nanoseconds / 1000000.0;
                    count++;
                    totalMilliseconds += milliseconds;
                    maximumMilliseconds = Math.Max(maximumMilliseconds, milliseconds);
                }

                public void Dispose()
                {
                    if (recorder.Valid) recorder.Dispose();
                }
            }

            private static readonly string[] MarkerNames =
            {
                "Vegetation Forward Total",
                "Vegetation Forward Cull",
                "Vegetation Forward CopyCounter",
                "Vegetation Forward Draw",
                "Vegetation Shadow Total",
                "Vegetation Shadow Cull",
                "Vegetation Shadow Draw"
            };

            private readonly GroupConfig config;
            private readonly List<Step> steps = new List<Step>();
            private readonly List<MarkerSample> markers = new List<MarkerSample>();
            private Camera gameCamera;
            private Transform gameCameraTransform;
            private Vector3 initialPosition;
            private Quaternion initialRotation;
            private SceneView sceneView;
            private Vector3 initialScenePivot;
            private Quaternion initialSceneRotation;
            private float initialSceneSize;
            private bool initialGameViewMaximized;
            private EditorWindow gameView;
            private RenderTexture fallbackTarget;
            private RenderTexture initialTarget;
            private int stepIndex = -1;
            private double stepEndTime;
            private double nextSceneRepaintTime;
            private int lastSampledFrame = -1;
            private bool initialized;
            private bool finishing;
            private int criticalCount;
            private bool deviceRemoved;
            private double referenceLuminance = -1.0;
            private bool suspectedBlack;
            private string profilerCapturePath;
            private bool originalProfilerEnabled;
            private bool originalBinaryLog;
            private string originalProfilerLogFile;

            public GroupRunner(GroupConfig config)
            {
                this.config = config;
            }

            public void NotifyCritical(bool isDeviceRemoved)
            {
                criticalCount++;
                deviceRemoved |= isDeviceRemoved;
            }

            public void Update()
            {
                if (finishing) return;
                if (!initialized)
                {
                    if (Time.frameCount < 2) return;
                    Initialize();
                    return;
                }

                if (config.sceneMode == SceneExerciseMode.ForcedSceneRepaint &&
                    sceneView != null && EditorApplication.timeSinceStartup >= nextSceneRepaintTime)
                {
                    sceneView.Repaint();
                    nextSceneRepaintTime = EditorApplication.timeSinceStartup + 0.1;
                }

                if (Time.frameCount != lastSampledFrame)
                {
                    lastSampledFrame = Time.frameCount;
                    for (int i = 0; i < markers.Count; i++) markers[i].Sample();
                }

                if (EditorApplication.timeSinceStartup < stepEndTime) return;

                if (stepIndex >= 0 && stepIndex < steps.Count && steps[stepIndex].capture)
                    CaptureFrame(steps[stepIndex].name);

                stepIndex++;
                if (stepIndex >= steps.Count)
                {
                    Finish();
                    return;
                }

                Step step = steps[stepIndex];
                Checkpoint(config.name, step.name, CameraContext());
                try
                {
                    step.action?.Invoke();
                }
                catch (Exception exception)
                {
                    AppendGroup(config.name,
                        $"STEP_EXCEPTION|Step={step.name}|Type={exception.GetType().Name}|Message={Escape(exception.Message)}");
                }
                stepEndTime = EditorApplication.timeSinceStartup + step.duration;
            }

            private void Initialize()
            {
                gameCamera = Camera.main;
                if (gameCamera == null)
                {
                    Camera[] cameras = UnityEngine.Object.FindObjectsOfType<Camera>(true);
                    for (int i = 0; i < cameras.Length; i++)
                    {
                        if (cameras[i].cameraType == CameraType.Game)
                        {
                            gameCamera = cameras[i];
                            break;
                        }
                    }
                }

                if (gameCamera == null)
                {
                    AppendDurable(Path.Combine(RunDirectory, $"{config.name}.ERROR.log"), "NO_GAME_CAMERA");
                    Finish();
                    return;
                }

                gameCameraTransform = gameCamera.transform;
                initialPosition = gameCameraTransform.position;
                initialRotation = gameCameraTransform.rotation;
                initialTarget = gameCamera.targetTexture;

                sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    initialScenePivot = sceneView.pivot;
                    initialSceneRotation = sceneView.rotation;
                    initialSceneSize = sceneView.size;
                }

                gameView = GameViewResolution.GetGameView();
                if (gameView != null)
                {
                    initialGameViewMaximized = gameView.maximized;
                    gameView.maximized = config.sceneMode == SceneExerciseMode.GameOnly;
                    gameView.Repaint();
                }

                ApplyConfiguration();
                StartProfilerCapture();
                BuildSteps();
                initialized = true;
                stepEndTime = EditorApplication.timeSinceStartup;
                AppendGroup(config.name,
                    $"ENVIRONMENT|Unity={Application.unityVersion}|GraphicsAPI={SystemInfo.graphicsDeviceType}|" +
                    $"GPU={Escape(SystemInfo.graphicsDeviceName)}|Driver={Escape(SystemInfo.graphicsDeviceVersion)}|" +
                    $"Camera={Escape(gameCamera.name)}|InitialPosition={initialPosition}|InitialRotation={initialRotation.eulerAngles}");
            }

            private void ApplyConfiguration()
            {
                VegetationRenderer[] renderers = UnityEngine.Object.FindObjectsOfType<VegetationRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    VegetationRenderer vegetation = renderers[i];
                    vegetation.forceGameCameraOnly = false;
                    vegetation.disableSceneForward = config.disableSceneForward;
                    vegetation.disableSceneShadow = config.disableSceneShadow;
                    vegetation.disableForwardRendering = config.disableForward;
                    vegetation.disableShadowRendering = config.disableShadow;
                    vegetation.disableGPUCulling = config.disableGpuCulling;
                    vegetation.diagnosticsMode = VegetationDiagnosticsMode.Verbose;
                    vegetation.diagnosticsVerboseInterval = 30;
                    vegetation.diagnosticsGPUReadbackInterval = 30;
                }

                AppendGroup(config.name, $"CONFIG_APPLIED|VegetationRendererCount={renderers.Length}");
            }

            private void BuildSteps()
            {
                steps.Add(new Step("STEP_01_IDLE", 2.0, RestoreGameCamera, true));
                steps.Add(new Step("STEP_02_YAW_RIGHT", 1.25,
                    () => gameCameraTransform.rotation = initialRotation * Quaternion.Euler(0f, 20f, 0f), true));
                steps.Add(new Step("STEP_03_YAW_LEFT", 1.25,
                    () => gameCameraTransform.rotation = initialRotation * Quaternion.Euler(0f, -20f, 0f), true));
                steps.Add(new Step("STEP_04_PITCH_UP", 1.25,
                    () => gameCameraTransform.rotation = initialRotation * Quaternion.Euler(-12f, 0f, 0f), true));
                steps.Add(new Step("STEP_05_PITCH_DOWN", 1.25,
                    () => gameCameraTransform.rotation = initialRotation * Quaternion.Euler(12f, 0f, 0f), true));
                steps.Add(new Step("STEP_06_SCENE_CAMERA", 1.5, MoveSceneCamera, false));
                steps.Add(new Step("STEP_07_720P", 2.0, () => SetResolution(1280, 720), true));
                steps.Add(new Step("STEP_08_1080P", 2.0, () => SetResolution(1920, 1080), true));
                steps.Add(new Step("STEP_09_1440P", 2.5, () => SetResolution(2560, 1440), true));
                steps.Add(new Step("STEP_10_RESTORE", 1.5, RestoreGameCamera, true));
            }

            private void RestoreGameCamera()
            {
                gameCameraTransform.SetPositionAndRotation(initialPosition, initialRotation);
            }

            private void MoveSceneCamera()
            {
                if (sceneView == null || config.sceneMode == SceneExerciseMode.GameOnly)
                {
                    AppendGroup(config.name, "SCENE_CAMERA|Available=false_or_suppressed");
                    return;
                }

                sceneView.LookAt(
                    initialScenePivot + new Vector3(8f, 3f, -6f),
                    initialSceneRotation * Quaternion.Euler(8f, 24f, 0f),
                    Mathf.Max(2f, initialSceneSize * 0.85f),
                    sceneView.orthographic,
                    true);
                sceneView.Repaint();
                AppendGroup(config.name, "SCENE_CAMERA|Available=true|DeterministicOffset=(8,3,-6)|Yaw=24|Pitch=8");
            }

            private void SetResolution(int width, int height)
            {
                ReleaseFallbackTarget();
                bool gameViewChanged = GameViewResolution.TrySetFixedResolution(width, height, out string detail);
                if (!gameViewChanged)
                {
                    fallbackTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                    {
                        name = $"VegetationDiagnostics_{width}x{height}"
                    };
                    fallbackTarget.Create();
                    gameCamera.targetTexture = fallbackTarget;
                }
                else
                {
                    gameCamera.targetTexture = initialTarget;
                }

                AppendGroup(config.name,
                    $"RESOLUTION_REQUEST|Requested={width}x{height}|Mode={(gameViewChanged ? "GameView" : "CameraTargetTextureFallback")}|Detail={Escape(detail)}");
            }

            private void CaptureFrame(string step)
            {
                Texture2D texture = null;
                RenderTexture previous = RenderTexture.active;
                try
                {
                    if (fallbackTarget != null)
                    {
                        RenderTexture.active = fallbackTarget;
                        texture = new Texture2D(fallbackTarget.width, fallbackTarget.height, TextureFormat.RGB24, false);
                        texture.ReadPixels(new Rect(0, 0, fallbackTarget.width, fallbackTarget.height), 0, 0, false);
                        texture.Apply(false, false);
                    }
                    else
                    {
                        Rect gameViewRect = gameView != null ? gameView.position : default;
                        if (gameView == null || gameCamera.pixelWidth > gameViewRect.width ||
                            gameCamera.pixelHeight > gameViewRect.height)
                        {
                            AppendGroup(config.name,
                                $"CAPTURE|Step={step}|Available=false|Reason=RequestedGameResolutionExceedsPhysicalGameView|" +
                                $"CameraPixels={gameCamera.pixelWidth}x{gameCamera.pixelHeight}|" +
                                $"GameViewWindow={gameViewRect.width:R}x{gameViewRect.height:R}");
                            return;
                        }
                        texture = ScreenCapture.CaptureScreenshotAsTexture();
                    }

                    if (texture == null)
                    {
                        AppendGroup(config.name, $"CAPTURE|Step={step}|Available=false");
                        return;
                    }

                    var pixels = texture.GetRawTextureData<Color32>();
                    int stride = Mathf.Max(1, pixels.Length / 4096);
                    long sampled = 0;
                    long black = 0;
                    double luminance = 0.0;
                    for (int i = 0; i < pixels.Length; i += stride)
                    {
                        Color32 color = pixels[i];
                        double value = (0.2126 * color.r + 0.7152 * color.g + 0.0722 * color.b) / 255.0;
                        luminance += value;
                        if (value < 0.005) black++;
                        sampled++;
                    }

                    double average = sampled > 0 ? luminance / sampled : 0.0;
                    double blackRatio = sampled > 0 ? black / (double)sampled : 1.0;
                    if (referenceLuminance < 0.0) referenceLuminance = average;
                    bool veryDark = average < 0.005 && blackRatio > 0.98;
                    bool regression = referenceLuminance > 0.02 && average < referenceLuminance * 0.1 && blackRatio > 0.98;
                    bool blackFrame = veryDark || regression;
                    suspectedBlack |= blackFrame;

                    AppendGroup(config.name,
                        $"CAPTURE|Step={step}|Available=true|Texture={texture.width}x{texture.height}|" +
                        $"CameraPixels={gameCamera.pixelWidth}x{gameCamera.pixelHeight}|Aspect={gameCamera.aspect:R}|" +
                        $"FOV={gameCamera.fieldOfView:R}|Near={gameCamera.nearClipPlane:R}|Far={gameCamera.farClipPlane:R}|" +
                        $"AverageLuminance={average:R}|BlackRatio={blackRatio:R}|SuspectedBlack={blackFrame}");

                    if (blackFrame)
                    {
                        string screenshot = Path.Combine(RunDirectory, $"{config.name}_{step}_BLACK.png");
                        File.WriteAllBytes(screenshot, texture.EncodeToPNG());
                        AppendDurable(Path.Combine(RunDirectory, $"{config.name}.ERROR.log"),
                            $"SUSPECTED_BLACK_FRAME|Step={step}|Screenshot={screenshot}|AverageLuminance={average:R}|BlackRatio={blackRatio:R}");
                    }
                }
                catch (Exception exception)
                {
                    AppendGroup(config.name,
                        $"CAPTURE_EXCEPTION|Step={step}|Type={exception.GetType().Name}|Message={Escape(exception.Message)}");
                }
                finally
                {
                    RenderTexture.active = previous;
                    if (texture != null) UnityEngine.Object.Destroy(texture);
                }
            }

            private void StartProfilerCapture()
            {
                originalProfilerEnabled = Profiler.enabled;
                originalBinaryLog = Profiler.enableBinaryLog;
                originalProfilerLogFile = Profiler.logFile;
                profilerCapturePath = Path.Combine(RunDirectory, $"{config.name}.raw");
                try
                {
                    Profiler.logFile = profilerCapturePath;
                    Profiler.enableBinaryLog = true;
                    Profiler.enabled = true;
                    TrySetProfilerDriverGpu(true);
                }
                catch (Exception exception)
                {
                    AppendGroup(config.name, $"PROFILER_CAPTURE_START_FAILED|Message={Escape(exception.Message)}");
                }

                for (int i = 0; i < MarkerNames.Length; i++)
                {
                    TryAddRecorder(ProfilerCategory.Render, MarkerNames[i], "Render");
                    TryAddRecorder(ProfilerCategory.Scripts, MarkerNames[i], "Scripts");
                }
            }

            private void TryAddRecorder(ProfilerCategory category, string marker, string categoryName)
            {
                try
                {
                    ProfilerRecorder recorder = ProfilerRecorder.StartNew(category, marker, 128);
                    if (recorder.Valid) markers.Add(new MarkerSample(marker, categoryName, recorder));
                    else recorder.Dispose();
                }
                catch (Exception exception)
                {
                    AppendGroup(config.name,
                        $"PROFILER_RECORDER_UNAVAILABLE|Marker={marker}|Category={categoryName}|Message={Escape(exception.Message)}");
                }
            }

            private void Finish()
            {
                finishing = true;
                RestoreGameCamera();
                gameCamera.targetTexture = initialTarget;
                ReleaseFallbackTarget();

                if (sceneView != null)
                {
                    sceneView.LookAt(initialScenePivot, initialSceneRotation, initialSceneSize, sceneView.orthographic, true);
                    sceneView.Repaint();
                }
                if (gameView != null)
                {
                    gameView.maximized = initialGameViewMaximized;
                    gameView.Repaint();
                }

                for (int i = 0; i < markers.Count; i++)
                {
                    MarkerSample marker = markers[i];
                    double average = marker.count > 0 ? marker.totalMilliseconds / marker.count : 0.0;
                    AppendGroup(config.name,
                        $"PROFILER_MARKER|Name={marker.name}|Category={marker.category}|Samples={marker.count}|" +
                        $"AverageMs={average:R}|MaximumMs={marker.maximumMilliseconds:R}");
                    marker.Dispose();
                }

                try
                {
                    Profiler.enabled = false;
                    Profiler.logFile = originalProfilerLogFile;
                    Profiler.enableBinaryLog = originalBinaryLog;
                    Profiler.enabled = originalProfilerEnabled;
                }
                catch (Exception exception)
                {
                    AppendGroup(config.name, $"PROFILER_CAPTURE_FINISH_FAILED|Message={Escape(exception.Message)}");
                }

                FileInfo capture = new FileInfo(profilerCapturePath);
                AppendGroup(config.name,
                    $"GROUP_RESULT|SuspectedBlack={suspectedBlack}|DeviceRemoved={deviceRemoved}|CriticalCount={criticalCount}|" +
                    $"ProfilerCapture={(capture.Exists ? capture.FullName : "Unavailable")}|ProfilerBytes={(capture.Exists ? capture.Length : 0)}");
                CompleteGroup(config.name);
            }

            private void ReleaseFallbackTarget()
            {
                if (fallbackTarget == null) return;
                if (gameCamera != null && gameCamera.targetTexture == fallbackTarget) gameCamera.targetTexture = initialTarget;
                fallbackTarget.Release();
                UnityEngine.Object.Destroy(fallbackTarget);
                fallbackTarget = null;
            }

            private string CameraContext()
            {
                if (gameCamera == null) return "Camera=<null>";
                return $"Camera={Escape(gameCamera.name)}|Resolution={gameCamera.pixelWidth}x{gameCamera.pixelHeight}|" +
                       $"Aspect={gameCamera.aspect:R}|FOV={gameCamera.fieldOfView:R}|Near={gameCamera.nearClipPlane:R}|Far={gameCamera.farClipPlane:R}";
            }

            private static void TrySetProfilerDriverGpu(bool enabled)
            {
                try
                {
                    Type type = typeof(Editor).Assembly.GetType("UnityEditorInternal.ProfilerDriver");
                    PropertyInfo property = type?.GetProperty("profileGPU", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    property?.SetValue(null, enabled);
                }
                catch
                {
                    // Raw capture remains useful even if this Editor version hides the GPU flag.
                }
            }

        }

        private static class GameViewResolution
        {
            private const string LabelPrefix = "[VegDiag] ";

            public static EditorWindow GetGameView()
            {
                Type type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                return type == null ? null : EditorWindow.GetWindow(type, false, "Game", false);
            }

            public static bool TrySetFixedResolution(int width, int height, out string detail)
            {
                try
                {
                    Type editorAssemblyType = typeof(Editor);
                    Assembly assembly = editorAssemblyType.Assembly;
                    Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                    Type groupEnumType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
                    Type sizeType = assembly.GetType("UnityEditor.GameViewSize");
                    Type sizeModeType = assembly.GetType("UnityEditor.GameViewSizeType");
                    Type gameViewType = assembly.GetType("UnityEditor.GameView");
                    if (sizesType == null || groupEnumType == null || sizeType == null || sizeModeType == null || gameViewType == null)
                        throw new MissingMemberException("Unity GameView reflection types are unavailable.");

                    Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                    object sizes = singletonType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
                    object standalone = Enum.Parse(groupEnumType, "Standalone");
                    object group = sizesType.GetMethod("GetGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?.Invoke(sizes, new[] { standalone });
                    if (group == null) throw new MissingMemberException("Standalone GameView size group unavailable.");

                    Type groupType = group.GetType();
                    MethodInfo getTotalCount = groupType.GetMethod("GetTotalCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    MethodInfo getSize = groupType.GetMethod("GetGameViewSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    int total = Convert.ToInt32(getTotalCount.Invoke(group, null));
                    int index = -1;
                    for (int i = 0; i < total; i++)
                    {
                        object size = getSize.Invoke(group, new object[] { i });
                        int itemWidth = ReadInt(size, "width");
                        int itemHeight = ReadInt(size, "height");
                        if (itemWidth == width && itemHeight == height)
                        {
                            index = i;
                            break;
                        }
                    }

                    if (index < 0)
                    {
                        object fixedResolution = Enum.Parse(sizeModeType, "FixedResolution");
                        ConstructorInfo constructor = sizeType.GetConstructor(
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                            null, new[] { sizeModeType, typeof(int), typeof(int), typeof(string) }, null);
                        if (constructor == null) throw new MissingMethodException("GameViewSize constructor unavailable.");
                        object custom = constructor.Invoke(new object[]
                        {
                            fixedResolution, width, height, $"{LabelPrefix}{width}x{height}"
                        });
                        groupType.GetMethod("AddCustomSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                            ?.Invoke(group, new[] { custom });
                        total = Convert.ToInt32(getTotalCount.Invoke(group, null));
                        index = total - 1;
                    }

                    EditorWindow gameView = GetGameView();
                    PropertyInfo selected = gameViewType.GetProperty("selectedSizeIndex",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (gameView == null || selected == null) throw new MissingMemberException("GameView selectedSizeIndex unavailable.");
                    selected.SetValue(gameView, index);
                    gameView.Repaint();
                    detail = $"SelectedSizeIndex={index}";
                    return true;
                }
                catch (Exception exception)
                {
                    detail = exception.GetType().Name + ": " + exception.Message;
                    return false;
                }
            }

            public static void RemoveDiagnosticSizes()
            {
                try
                {
                    Assembly assembly = typeof(Editor).Assembly;
                    Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                    Type groupEnumType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
                    if (sizesType == null || groupEnumType == null) return;
                    Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                    object sizes = singletonType.GetProperty("instance", BindingFlags.Static | BindingFlags.Public)?.GetValue(null);
                    object standalone = Enum.Parse(groupEnumType, "Standalone");
                    object group = sizesType.GetMethod("GetGroup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?.Invoke(sizes, new[] { standalone });
                    if (group == null) return;
                    Type groupType = group.GetType();
                    MethodInfo getTotal = groupType.GetMethod("GetTotalCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    MethodInfo getBuiltin = groupType.GetMethod("GetBuiltinCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    MethodInfo getSize = groupType.GetMethod("GetGameViewSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    MethodInfo remove = groupType.GetMethod("RemoveCustomSize", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    int builtin = Convert.ToInt32(getBuiltin.Invoke(group, null));
                    int total = Convert.ToInt32(getTotal.Invoke(group, null));
                    for (int i = total - 1; i >= builtin; i--)
                    {
                        object size = getSize.Invoke(group, new object[] { i });
                        string label = ReadString(size, "baseText");
                        if (!string.IsNullOrEmpty(label) && label.StartsWith(LabelPrefix, StringComparison.Ordinal))
                            remove.Invoke(group, new object[] { i });
                    }
                }
                catch
                {
                    // Cleanup is best effort and never affects the test result.
                }
            }

            private static int ReadInt(object target, string name)
            {
                PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null) return Convert.ToInt32(property.GetValue(target));
                FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return field != null ? Convert.ToInt32(field.GetValue(target)) : 0;
            }

            private static string ReadString(object target, string name)
            {
                PropertyInfo property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null) return Convert.ToString(property.GetValue(target));
                FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return field != null ? Convert.ToString(field.GetValue(target)) : string.Empty;
            }
        }
    }
}
#endif
