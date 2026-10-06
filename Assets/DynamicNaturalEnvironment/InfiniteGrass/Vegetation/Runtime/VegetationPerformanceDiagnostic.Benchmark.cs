using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed partial class VegetationPerformanceDiagnostic
{
    [Header("B1 Benchmark")]
    public Camera benchmarkCamera;
    public VegetationDatabase benchmarkSourceDatabase;
    public VegetationRendererFeature benchmarkRendererFeature;
    [Min(0.1f)] public float benchmarkWarmupSeconds = 5f;
    [Min(0.1f)] public float benchmarkSampleSeconds = 30f;
    public Vector3 benchmarkPathStart = new Vector3(352.45f, 50f, 234.35f);
    public Vector3 benchmarkPathEnd = new Vector3(270f, 50f, 300f);
    [Range(1f, 179f)] public float benchmarkFOV = 40f;
    [Min(1)] public int benchmarkWidth = 1920;
    [Min(1)] public int benchmarkHeight = 1080;
    public bool benchmarkIncludeOneMillion = true;
    public string benchmarkQualityLevel = "High Fidelity";

    private enum BenchmarkKind
    {
        Baseline, CPUChunk, GPUCull, LOD, Core,
        Lights0, Lights1, Lights4, Lights8,
        DepthOff, DepthOn, ShadowUnoptimized, ShadowOptimized,
        CoreCPUChunkOff, CoreCPUChunkOn
    }

    private struct BenchmarkCase
    {
        public string name;
        public string resultGroup;
        public BenchmarkKind kind;
        public int scale;
        public BenchmarkCase(string name, BenchmarkKind kind, int scale, string resultGroup = "")
        {
            this.name = name;
            this.resultGroup = resultGroup;
            this.kind = kind;
            this.scale = scale;
        }
    }

    private struct BenchmarkFrame
    {
        public int frame;
        public float pathT;
        public float frameMS, cpuMS, gpuMS;
        public long gcBytes;
        public int visibleRenderGroupChunkRanges, dispatches, draws, shadowDispatches, shadowDraws;
    }

    private sealed class LODReadbackFrame
    {
        public int groups;
        public long lod0, lod1, lod2, lod3;
        public long forwardVisibleInstances;
        public bool crossFade;
        public bool forwardVisibleValid = true;
        public bool valid = true;
    }

    private readonly List<BenchmarkFrame> benchmarkFrames = new List<BenchmarkFrame>(4096);
    private readonly Dictionary<int, LODReadbackFrame> benchmarkLODReadbacks = new Dictionary<int, LODReadbackFrame>();
    private readonly Dictionary<int, int> benchmarkShadowCandidateSlots = new Dictionary<int, int>();
    private readonly Dictionary<int, int> benchmarkUsedLights = new Dictionary<int, int>();
    private readonly Dictionary<int, int> benchmarkCameraLights = new Dictionary<int, int>();
    private readonly List<Light> benchmarkLights = new List<Light>(8);
    private GameObject benchmarkLightRoot;
    private readonly List<KeyValuePair<Light, bool>> savedLights = new List<KeyValuePair<Light, bool>>();
    private readonly List<KeyValuePair<Camera, bool>> savedCameras = new List<KeyValuePair<Camera, bool>>();
    private readonly List<KeyValuePair<VegetationRenderer, bool>> savedRenderers = new List<KeyValuePair<VegetationRenderer, bool>>();
    private VegetationDatabase benchmarkRuntimeDatabase;
    private Camera benchmarkSourceCamera;
    private GameObject benchmarkCameraObject;
    private VegetationDatabase savedDatabase;
    private Coroutine benchmarkCoroutine;
    private StreamWriter benchmarkSummaryWriter, benchmarkRawWriter, benchmarkReadbackWriter;
    private string benchmarkDirectory;
    private bool benchmarkRunning, benchmarkSampling, benchmarkSmoke, benchmarkSingleCore100k, benchmarkSingleGPUCull100k, benchmarkSupplemental, benchmarkQuitOnComplete;
    private readonly object benchmarkLogAuditLock = new object();
    private readonly List<string> benchmarkLogAuditMessages = new List<string>();
    private string benchmarkActiveAuditCase;
    private int benchmarkCaseWarningCount, benchmarkCaseErrorCount;
    private int benchmarkTotalWarningCount, benchmarkTotalErrorCount;
    private float benchmarkPathT;
    private int benchmarkSampleStartFrame, benchmarkSampleEndFrame;
    private int savedQualityLevel, savedVSync, savedTargetFrameRate, savedWidth, savedHeight;
    private FullScreenMode savedFullScreenMode;
    private RenderPipelineAsset benchmarkPipelineAsset;
    private string benchmarkRendererName;
    private float savedFOV;
    private Vector3 savedCameraPosition;
    private Quaternion savedCameraRotation;
    private Camera savedTargetCamera;
    private bool savedEnableDiagnostics, savedOverlay, savedWriteCSV;
    private VegetationDiagnosticsMode savedDiagnosticsMode;
    private bool savedGPUReadback, savedForceGameOnly;
    private int savedGPUReadbackInterval;
    private bool savedGPUCull, savedChunkCull, savedGPULOD;
    private bool savedDisableGPUCull;
    private bool savedShadowChunk, savedShadowInfluence, savedShadowWind, savedShadowAlpha;
    private int savedShadowLODOffset;
    private bool savedFeatureDepth, savedFeatureLights;
    private int savedFeatureLightCap;
    private string benchmarkTimestamp;
    private Light savedSun;
    private int benchmarkSourceRevision;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartCommandLineBenchmark()
    {
        string[] args = Environment.GetCommandLineArgs();
        bool requested = false;
        bool singleCore100k = false;
        bool singleGPUCull100k = false;
        bool supplemental = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "-vegetationBenchmark", StringComparison.OrdinalIgnoreCase)) requested = true;
            if (string.Equals(args[i], "-vegetationBenchmarkCore100k", StringComparison.OrdinalIgnoreCase))
            { requested = true; singleCore100k = true; }
            if (string.Equals(args[i], "-vegetationBenchmarkGPUCull100k", StringComparison.OrdinalIgnoreCase))
            { requested = true; singleGPUCull100k = true; }
            if (string.Equals(args[i], "-vegetationBenchmarkSupplemental", StringComparison.OrdinalIgnoreCase))
            { requested = true; supplemental = true; }
        }
        if (!requested) return;
        VegetationRenderer renderer = FindObjectOfType<VegetationRenderer>();
        if (renderer == null) { Debug.LogError("[VegetationBenchmark] No VegetationRenderer in the loaded scene."); return; }
        var diagnostic = renderer.GetComponent<VegetationPerformanceDiagnostic>();
        if (diagnostic == null) diagnostic = renderer.gameObject.AddComponent<VegetationPerformanceDiagnostic>();
        diagnostic.vegetationRenderer = renderer;
        diagnostic.commandLineStartPending = true;
        diagnostic.commandLineSingleCore100k = singleCore100k;
        diagnostic.commandLineSingleGPUCull100k = singleGPUCull100k;
        diagnostic.commandLineSupplemental = supplemental;
        diagnostic.benchmarkQuitOnComplete = true;
    }

    private bool commandLineStartPending, commandLineSingleCore100k, commandLineSingleGPUCull100k, commandLineSupplemental;

    private IEnumerator Start()
    {
        if (!commandLineStartPending) yield break;
        commandLineStartPending = false;
        yield return null;
        if (commandLineSupplemental) StartBenchmarkSupplemental();
        else if (commandLineSingleGPUCull100k) StartBenchmarkGPUCull100k();
        else if (commandLineSingleCore100k) StartBenchmarkCore100k();
        else StartBenchmark();
    }

    [ContextMenu("Run B1 Benchmark")]
    public void StartBenchmark()
    {
        BeginBenchmark(false, false, false, false);
    }

    public void StartBenchmarkSmoke()
    {
        BeginBenchmark(true, false, false, false);
    }

    public void StartBenchmarkCore100k()
    {
        BeginBenchmark(false, true, false, false);
    }

    public void StartBenchmarkGPUCull100k()
    {
        BeginBenchmark(false, false, true, false);
    }

    public void StartBenchmarkSupplemental()
    {
        BeginBenchmark(false, false, false, true);
    }

    private void BeginBenchmark(bool smoke, bool singleCore100k, bool singleGPUCull100k, bool supplemental)
    {
        if (benchmarkRunning) return;
        if (!Application.isPlaying) { Debug.LogError("[VegetationBenchmark] Enter Play Mode first.", this); return; }
        if (vegetationRenderer == null) vegetationRenderer = GetComponent<VegetationRenderer>();
        if (vegetationRenderer == null) { Debug.LogError("[VegetationBenchmark] VegetationRenderer is required.", this); return; }
        if (benchmarkSourceDatabase == null) benchmarkSourceDatabase = vegetationRenderer.database;
        if (benchmarkCamera == null) benchmarkCamera = vegetationRenderer.targetCamera != null ? vegetationRenderer.targetCamera : Camera.main;
        if (benchmarkRendererFeature == null)
        {
            foreach (var feature in Resources.FindObjectsOfTypeAll<VegetationRendererFeature>())
                if (feature != null && feature.isActive) { benchmarkRendererFeature = feature; break; }
        }
        if (benchmarkSourceDatabase == null || benchmarkCamera == null || benchmarkRendererFeature == null)
        {
            Debug.LogError("[VegetationBenchmark] Source database, camera, and active VegetationRendererFeature are required.", this);
            return;
        }
        benchmarkSourceCamera = benchmarkCamera;
        benchmarkCameraObject = new GameObject("B1 Deterministic Benchmark Camera");
        benchmarkCameraObject.hideFlags = HideFlags.DontSave;
        Camera isolatedCamera = benchmarkCameraObject.AddComponent<Camera>();
        isolatedCamera.CopyFrom(benchmarkSourceCamera);
        isolatedCamera.targetTexture = null;
        isolatedCamera.tag = "Untagged";
        var sourceURP = benchmarkSourceCamera.GetComponent<UniversalAdditionalCameraData>();
        var isolatedURP = benchmarkCameraObject.AddComponent<UniversalAdditionalCameraData>();
        if (sourceURP != null)
        {
            isolatedURP.renderPostProcessing = sourceURP.renderPostProcessing;
            isolatedURP.renderShadows = sourceURP.renderShadows;
        }
        benchmarkCamera = isolatedCamera;
        benchmarkSmoke = smoke;
        benchmarkSingleCore100k = singleCore100k;
        benchmarkSingleGPUCull100k = singleGPUCull100k;
        benchmarkSupplemental = supplemental;
        benchmarkRunning = true;
        benchmarkCoroutine = StartCoroutine(RunBenchmark());
    }

    private IEnumerator RunBenchmark()
    {
        try
        {
            SaveBenchmarkState();
            benchmarkSourceRevision = benchmarkSourceDatabase.DataRevision;
            ConfigureBenchmarkEnvironment();
            OpenBenchmarkCSV();
            ResetBenchmarkLogAudit();
            Application.logMessageReceivedThreaded += OnBenchmarkLogMessage;
            VegetationDiagnostics.BenchmarkAdditionalLights += OnBenchmarkAdditionalLights;
            VegetationDiagnostics.BenchmarkShadowCandidateSlots += OnBenchmarkShadowCandidateSlots;
            VegetationDiagnostics.BenchmarkForwardReadback += OnBenchmarkForwardReadback;

            if (benchmarkSupplemental)
            {
                yield return RunSupplementalBenchmark();
            }
            else
            {
                int[] scales = benchmarkSmoke ? new[] { 1000 } : benchmarkSingleCore100k || benchmarkSingleGPUCull100k ? new[] { 100000 } : benchmarkIncludeOneMillion
                    ? new[] { 100000, 500000, 1000000 } : new[] { 100000, 500000 };
                for (int scaleIndex = 0; scaleIndex < scales.Length; scaleIndex++)
                {
                    if (benchmarkSourceDatabase.DataRevision != benchmarkSourceRevision)
                        throw new InvalidOperationException("Source database changed during benchmark preparation.");
                    int scale = scales[scaleIndex];
                    yield return BuildRuntimeDatabase(scale);
                    BenchmarkCase[] cases = BuildCases(scale);
                    for (int caseIndex = 0; caseIndex < cases.Length; caseIndex++)
                        yield return RunCase(cases[caseIndex]);
                    ReleaseRuntimeDatabase();
                }
            }
            Debug.Log($"[VegetationBenchmark] Complete. Cases exported to {benchmarkDirectory}. " +
                (Application.isEditor ? "Editor results are validation only." : "Standalone results are ready for review."), this);
        }
        finally
        {
            StopBenchmarkAndRestore();
        }
        if (benchmarkQuitOnComplete && !Application.isEditor) Application.Quit(0);
    }

    private IEnumerator RunSupplementalBenchmark()
    {
        int[] replacementScales = { 100000, 500000, 1000000 };
        for (int scaleIndex = 0; scaleIndex < replacementScales.Length; scaleIndex++)
        {
            if (benchmarkSourceDatabase.DataRevision != benchmarkSourceRevision)
                throw new InvalidOperationException("Source database changed during supplemental benchmark preparation.");
            int scale = replacementScales[scaleIndex];
            yield return BuildRuntimeDatabase(scale);
            yield return RunCase(new BenchmarkCase("+GPUInstanceCull", BenchmarkKind.GPUCull, scale,
                "Replacement GPUCull rows"));
            ReleaseRuntimeDatabase();
        }

        yield return BuildRuntimeDatabase(500000);
        yield return RunCase(new BenchmarkCase("Core_CPUChunk_OFF", BenchmarkKind.CoreCPUChunkOff, 500000,
            "CPUChunk real-pipeline comparison"));
        yield return RunCase(new BenchmarkCase("Core_CPUChunk_ON", BenchmarkKind.CoreCPUChunkOn, 500000,
            "CPUChunk real-pipeline comparison"));
        ReleaseRuntimeDatabase();
    }

    private BenchmarkCase[] BuildCases(int scale)
    {
        if (benchmarkSmoke)
            return new[] { new BenchmarkCase("Baseline", BenchmarkKind.Baseline, scale),
                new BenchmarkCase("FullyOptimizedCore", BenchmarkKind.Core, scale) };
        if (benchmarkSingleCore100k)
            return new[] { new BenchmarkCase("FullyOptimizedCore", BenchmarkKind.Core, scale) };
        if (benchmarkSingleGPUCull100k)
            return new[] { new BenchmarkCase("+GPUInstanceCull", BenchmarkKind.GPUCull, scale) };
        var cases = new List<BenchmarkCase>
        {
            new BenchmarkCase("Baseline", BenchmarkKind.Baseline, scale),
            new BenchmarkCase("+CPUChunkCull", BenchmarkKind.CPUChunk, scale),
            new BenchmarkCase("+GPUInstanceCull", BenchmarkKind.GPUCull, scale),
            new BenchmarkCase("+MeshDensityLOD", BenchmarkKind.LOD, scale),
            new BenchmarkCase("FullyOptimizedCore", BenchmarkKind.Core, scale)
        };
        if (scale == 500000)
        {
            cases.Add(new BenchmarkCase("FullyOptimizedCore_AL_0", BenchmarkKind.Lights0, scale));
            cases.Add(new BenchmarkCase("FullyOptimizedCore_AL_1", BenchmarkKind.Lights1, scale));
            cases.Add(new BenchmarkCase("FullyOptimizedCore_AL_4", BenchmarkKind.Lights4, scale));
            cases.Add(new BenchmarkCase("FullyOptimizedCore_AL_8", BenchmarkKind.Lights8, scale));
            cases.Add(new BenchmarkCase("FullyOptimizedCore_DN_OFF", BenchmarkKind.DepthOff, scale));
            cases.Add(new BenchmarkCase("FullyOptimizedCore_DN_ON", BenchmarkKind.DepthOn, scale));
            cases.Add(new BenchmarkCase("FullyOptimizedCore_Shadow_OFF", BenchmarkKind.ShadowUnoptimized, scale));
            cases.Add(new BenchmarkCase("FullyOptimizedCore_Shadow_ON", BenchmarkKind.ShadowOptimized, scale));
        }
        return cases.ToArray();
    }

    private IEnumerator BuildRuntimeDatabase(int targetCount)
    {
        int validSourceCount = 0;
        foreach (VegetationChunkData chunk in benchmarkSourceDatabase.chunks)
        {
            if (chunk == null || chunk.instances == null) continue;
            foreach (VegetationInstance instance in chunk.instances)
                if (ResolveBenchmarkSpecies(instance) != null) validSourceCount++;
        }
        if (validSourceCount == 0) throw new InvalidOperationException("Benchmark source database has no active instances.");

        benchmarkRuntimeDatabase = ScriptableObject.CreateInstance<VegetationDatabase>();
        benchmarkRuntimeDatabase.hideFlags = HideFlags.DontSave;
        benchmarkRuntimeDatabase.chunkSize = benchmarkSourceDatabase.chunkSize;
        benchmarkRuntimeDatabase.species = new List<VegetationSpecies>(benchmarkSourceDatabase.species);
        benchmarkRuntimeDatabase.BeginBatchMutation();
        int sourceOrdinal = 0;
        int created = 0;
        try
        {
            foreach (VegetationChunkData chunk in benchmarkSourceDatabase.chunks)
            {
                if (chunk == null || chunk.instances == null) continue;
                foreach (VegetationInstance source in chunk.instances)
                {
                    VegetationSpecies species = ResolveBenchmarkSpecies(source);
                    if (species == null) continue;
                    int before = (int)((long)sourceOrdinal * targetCount / validSourceCount);
                    sourceOrdinal++;
                    int after = (int)((long)sourceOrdinal * targetCount / validSourceCount);
                    for (int copy = 0; copy < after - before; copy++)
                    {
                        // A repeated source stays in the same spatial distribution, with a
                        // fixed sub-metre offset to avoid perfectly overlapping geometry.
                        Vector3 position = source.position + (copy == 0 ? Vector3.zero
                            : new Vector3(0.73f * copy, 0f, 0.59f * copy));
                        benchmarkRuntimeDatabase.AddInstance(species, position, source.rotation, source.scale);
                        created++;
                    }
                    if (sourceOrdinal % 5000 == 0) yield return null;
                }
            }
        }
        finally
        {
            benchmarkRuntimeDatabase.EndBatchMutation();
        }
        if (created != targetCount || benchmarkRuntimeDatabase.TotalInstanceCount != targetCount)
            throw new InvalidOperationException($"Benchmark scale build produced {created}/{targetCount} instances.");

        vegetationRenderer.database = benchmarkRuntimeDatabase;
        vegetationRenderer.Rebuild();
        Debug.Log($"[VegetationBenchmark] Prepared {targetCount:N0} instances, {benchmarkRuntimeDatabase.ChunkCount} chunks.", this);
        yield return null;
    }

    private VegetationSpecies ResolveBenchmarkSpecies(VegetationInstance instance)
    {
        int index = instance.speciesID > 0
            ? benchmarkSourceDatabase.GetSpeciesIndexByID(instance.speciesID) : instance.speciesIndex;
        return index >= 0 && index < benchmarkSourceDatabase.SpeciesCount
            ? benchmarkSourceDatabase.species[index] : null;
    }

    private IEnumerator RunCase(BenchmarkCase test)
    {
        if (benchmarkRuntimeDatabase == null || benchmarkRuntimeDatabase.TotalInstanceCount != test.scale)
            throw new InvalidOperationException("Benchmark instance count changed before a case.");
        BeginBenchmarkCaseAudit(test.name);
        ApplyBenchmarkCase(test.kind);
        benchmarkFrames.Clear();
        benchmarkLODReadbacks.Clear();
        benchmarkShadowCandidateSlots.Clear();
        benchmarkUsedLights.Clear();
        benchmarkCameraLights.Clear();
        float warmup = EffectiveWarmupSeconds;
        float duration = EffectiveSampleSeconds;
        SetBenchmarkCamera(0f);
        yield return null;
        ValidateBenchmarkEnvironment();

        double phaseStart = Time.realtimeSinceStartupAsDouble;
        while (Time.realtimeSinceStartupAsDouble - phaseStart < warmup)
        {
            SetBenchmarkCamera((float)((Time.realtimeSinceStartupAsDouble - phaseStart) / duration));
            yield return null;
        }

        // Begin every measured path from the same transform. The next frame settles
        // the discontinuity before measured samples are accepted.
        SetBenchmarkCamera(0f);
        yield return null;
        benchmarkSampleStartFrame = Time.frameCount + 1;
        benchmarkSampling = true;
        phaseStart = Time.realtimeSinceStartupAsDouble;
        while (Time.realtimeSinceStartupAsDouble - phaseStart < duration)
        {
            SetBenchmarkCamera((float)((Time.realtimeSinceStartupAsDouble - phaseStart) / duration));
            yield return null;
        }
        benchmarkSampling = false;
        benchmarkSampleEndFrame = Time.frameCount;

        // FrameTimingManager reports completed frames with a fixed delay, and the
        // existing GPU counter diagnostics finish asynchronously.
        for (int frame = 0; frame < 8; frame++) yield return null;
        if (benchmarkSingleGPUCull100k)
            yield return CaptureBenchmarkValidationScreenshot();
        WriteBenchmarkCase(test);
        EndBenchmarkCaseAudit();
    }

    private IEnumerator CaptureBenchmarkValidationScreenshot()
    {
        string path = Path.Combine(benchmarkDirectory, "validation.png");
        ScreenCapture.CaptureScreenshot(path);
        for (int frame = 0; frame < 16; frame++)
        {
            yield return new WaitForEndOfFrame();
            if (File.Exists(path) && new FileInfo(path).Length > 0) yield break;
        }
    }

    private void SetBenchmarkCamera(float t)
    {
        benchmarkPathT = Mathf.Clamp01(t);
        Vector3 direction = benchmarkPathEnd - benchmarkPathStart;
        benchmarkCamera.transform.position = Vector3.LerpUnclamped(benchmarkPathStart, benchmarkPathEnd, benchmarkPathT);
        if (direction.sqrMagnitude > 0.0001f)
            benchmarkCamera.transform.rotation = Quaternion.LookRotation(direction.normalized + Vector3.down * 0.12f, Vector3.up);
        benchmarkCamera.fieldOfView = benchmarkFOV;
    }

    private void ApplyBenchmarkCase(BenchmarkKind kind)
    {
        bool cpuChunk = kind != BenchmarkKind.Baseline && kind != BenchmarkKind.CoreCPUChunkOff;
        bool gpuCull = kind != BenchmarkKind.Baseline && kind != BenchmarkKind.CPUChunk;
        bool lod = gpuCull && kind != BenchmarkKind.GPUCull;
        bool shadowOptimized = kind == BenchmarkKind.Core || kind == BenchmarkKind.DepthOff ||
            kind == BenchmarkKind.DepthOn || kind == BenchmarkKind.ShadowOptimized ||
            kind == BenchmarkKind.Lights0 || kind == BenchmarkKind.Lights1 ||
            kind == BenchmarkKind.Lights4 || kind == BenchmarkKind.Lights8 ||
            kind == BenchmarkKind.CoreCPUChunkOff || kind == BenchmarkKind.CoreCPUChunkOn;
        bool depthNormals = kind == BenchmarkKind.DepthOn;
        int lights = kind == BenchmarkKind.Lights1 ? 1 : kind == BenchmarkKind.Lights4 ? 4 :
            kind == BenchmarkKind.Lights8 ? 8 : 0;

        vegetationRenderer.enableChunkCulling = cpuChunk;
        vegetationRenderer.enableGPUCulling = gpuCull;
        vegetationRenderer.disableGPUCulling = false;
        vegetationRenderer.enableGPULOD = lod;
        vegetationRenderer.enableShadowChunkCulling = shadowOptimized;
        vegetationRenderer.enableShadowInfluenceCulling = shadowOptimized;
        vegetationRenderer.shadowLODOffset = shadowOptimized ? savedShadowLODOffset : 0;
        vegetationRenderer.enableShadowWindLOD = shadowOptimized && savedShadowWind;
        vegetationRenderer.enableShadowAlphaCutoffLOD = shadowOptimized && savedShadowAlpha;
        benchmarkRendererFeature.settings.enableDepthNormalsPass = depthNormals;
        benchmarkRendererFeature.settings.enableAdditionalLights = lights > 0;
        benchmarkRendererFeature.settings.maxVegetationAdditionalLights = lights;
        for (int i = 0; i < benchmarkLights.Count; i++) benchmarkLights[i].enabled = i < lights;
    }

    private void CaptureBenchmarkFrame()
    {
        if (!benchmarkSampling || vegetationRenderer == null) return;
        // Unity 2022.3 FrameTimingManager has a four-frame completion delay.
        if (Time.frameCount - benchmarkSampleStartFrame < 8) return;
        int index = (frameWriteIndex - 1 + FrameHistoryCapacity) % FrameHistoryCapacity;
        float frame = frameMilliseconds[index];
        if (!(frame > 0f) || float.IsNaN(frame) || float.IsInfinity(frame)) return;
        benchmarkFrames.Add(new BenchmarkFrame
        {
            frame = Time.frameCount,
            pathT = benchmarkPathT,
            frameMS = frame,
            cpuMS = cpuMilliseconds[index] > 0f ? cpuMilliseconds[index] : float.NaN,
            gpuMS = gpuMilliseconds[index] > 0f ? gpuMilliseconds[index] : float.NaN,
            gcBytes = gcAllocatedRecorder.Valid ? gcAllocatedRecorder.LastValue : -1,
            visibleRenderGroupChunkRanges = vegetationRenderer.VisibleChunkRangeCount,
            dispatches = vegetationRenderer.ForwardDispatchCount,
            draws = vegetationRenderer.ForwardDrawCount,
            shadowDispatches = vegetationRenderer.ShadowDispatchCount,
            shadowDraws = vegetationRenderer.ShadowDrawCount
        });
    }

    private void OnBenchmarkAdditionalLights(int frame, int cameraID, int cameraCount, int usedCount)
    {
        if (benchmarkCamera == null || cameraID != benchmarkCamera.GetInstanceID()) return;
        benchmarkUsedLights[frame] = usedCount;
        benchmarkCameraLights[frame] = cameraCount;
    }

    private void OnBenchmarkShadowCandidateSlots(int frame, int cameraID, int slotReferences, int dispatches)
    {
        if (benchmarkCamera == null || cameraID != benchmarkCamera.GetInstanceID()) return;
        benchmarkShadowCandidateSlots.TryGetValue(frame, out int previous);
        benchmarkShadowCandidateSlots[frame] = previous + slotReferences;
    }

    private void OnBenchmarkForwardReadback(int frame, int cameraID, long lod0, long lod1,
        long lod2, long lod3, bool crossFade, bool valid, long forwardVisibleInstances, bool forwardVisibleValid)
    {
        if (benchmarkCamera == null || cameraID != benchmarkCamera.GetInstanceID()) return;
        if (!benchmarkLODReadbacks.TryGetValue(frame, out LODReadbackFrame result))
        {
            result = new LODReadbackFrame();
            benchmarkLODReadbacks.Add(frame, result);
        }
        result.groups++;
        result.lod0 += lod0; result.lod1 += lod1; result.lod2 += lod2; result.lod3 += lod3;
        result.forwardVisibleInstances += forwardVisibleInstances;
        result.crossFade |= crossFade;
        result.forwardVisibleValid &= forwardVisibleValid;
        result.valid &= valid;
    }

    private void SaveBenchmarkState()
    {
        savedDatabase = vegetationRenderer.database;
        savedTargetCamera = vegetationRenderer.targetCamera;
        savedEnableDiagnostics = enableDiagnostics;
        savedOverlay = showRuntimeOverlay;
        savedWriteCSV = writeCSV;
        savedDiagnosticsMode = vegetationRenderer.diagnosticsMode;
        savedGPUReadback = vegetationRenderer.enableDiagnosticsGPUReadback;
        savedGPUReadbackInterval = vegetationRenderer.diagnosticsGPUReadbackInterval;
        savedForceGameOnly = vegetationRenderer.forceGameCameraOnly;
        savedGPUCull = vegetationRenderer.enableGPUCulling;
        savedDisableGPUCull = vegetationRenderer.disableGPUCulling;
        savedChunkCull = vegetationRenderer.enableChunkCulling;
        savedGPULOD = vegetationRenderer.enableGPULOD;
        savedShadowChunk = vegetationRenderer.enableShadowChunkCulling;
        savedShadowInfluence = vegetationRenderer.enableShadowInfluenceCulling;
        savedShadowLODOffset = vegetationRenderer.shadowLODOffset;
        savedShadowWind = vegetationRenderer.enableShadowWindLOD;
        savedShadowAlpha = vegetationRenderer.enableShadowAlphaCutoffLOD;
        savedFeatureDepth = benchmarkRendererFeature.settings.enableDepthNormalsPass;
        savedFeatureLights = benchmarkRendererFeature.settings.enableAdditionalLights;
        savedFeatureLightCap = benchmarkRendererFeature.settings.maxVegetationAdditionalLights;
        savedCameraPosition = benchmarkCamera.transform.position;
        savedCameraRotation = benchmarkCamera.transform.rotation;
        savedFOV = benchmarkCamera.fieldOfView;
        savedQualityLevel = QualitySettings.GetQualityLevel();
        savedVSync = QualitySettings.vSyncCount;
        savedTargetFrameRate = Application.targetFrameRate;
        savedWidth = Screen.width;
        savedHeight = Screen.height;
        savedFullScreenMode = Screen.fullScreenMode;
        savedSun = RenderSettings.sun;
        savedLights.Clear();
        foreach (Light light in FindObjectsOfType<Light>())
            savedLights.Add(new KeyValuePair<Light, bool>(light, light.enabled));
        savedCameras.Clear();
        foreach (Camera camera in FindObjectsOfType<Camera>())
            savedCameras.Add(new KeyValuePair<Camera, bool>(camera, camera.enabled));
        savedRenderers.Clear();
        foreach (VegetationRenderer renderer in VegetationRenderer.ActiveRenderers)
            savedRenderers.Add(new KeyValuePair<VegetationRenderer, bool>(renderer, renderer.enabled));
    }

    private void ConfigureBenchmarkEnvironment()
    {
        int qualityLevel = Array.FindIndex(QualitySettings.names,
            name => string.Equals(name, benchmarkQualityLevel, StringComparison.Ordinal));
        if (qualityLevel < 0)
            throw new InvalidOperationException($"Benchmark Quality Level '{benchmarkQualityLevel}' does not exist.");
        QualitySettings.SetQualityLevel(qualityLevel, true);
        benchmarkPipelineAsset = GraphicsSettings.currentRenderPipeline;
        benchmarkRendererName = benchmarkCamera.GetUniversalAdditionalCameraData().scriptableRenderer.GetType().FullName;
        if (benchmarkPipelineAsset == null || string.IsNullOrEmpty(benchmarkRendererName))
            throw new InvalidOperationException("Benchmark URP Asset or Renderer could not be resolved.");

        enableDiagnostics = true;
        showRuntimeOverlay = false;
        writeCSV = false;
        vegetationRenderer.targetCamera = benchmarkCamera;
        vegetationRenderer.forceGameCameraOnly = true;
        vegetationRenderer.diagnosticsMode = VegetationDiagnosticsMode.Normal;
        vegetationRenderer.enableDiagnosticsGPUReadback = true;
        vegetationRenderer.diagnosticsGPUReadbackInterval = benchmarkSmoke ? 2 : 120;
        foreach (var item in savedRenderers)
            if (item.Key != null && item.Key != vegetationRenderer) item.Key.enabled = false;
        foreach (var item in savedCameras)
            if (item.Key != null) item.Key.enabled = item.Key == benchmarkCamera;
        foreach (var item in savedLights)
            if (item.Key != null) item.Key.enabled = false;
        Light main = savedSun;
        if (main == null || main.type != LightType.Directional)
            foreach (var item in savedLights)
                if (item.Key != null && item.Key.type == LightType.Directional) { main = item.Key; break; }
        if (main != null) { main.enabled = true; RenderSettings.sun = main; }

        benchmarkLightRoot = new GameObject("B1 Benchmark Additional Lights");
        benchmarkLightRoot.hideFlags = HideFlags.DontSave;
        for (int i = 0; i < 8; i++)
        {
            var go = new GameObject("Additional " + i);
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(benchmarkLightRoot.transform, false);
            float t = (i + 0.5f) / 8f;
            Vector3 center = Vector3.Lerp(benchmarkPathStart, benchmarkPathEnd, t);
            go.transform.position = center + new Vector3((i % 2 == 0 ? -1f : 1f) * 12f, 7f, 0f);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 55f;
            light.intensity = 3f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            benchmarkLights.Add(light);
        }
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
        Screen.SetResolution(benchmarkWidth, benchmarkHeight, FullScreenMode.Windowed);
    }

    private void ValidateBenchmarkEnvironment()
    {
        string quality = QualitySettings.names[QualitySettings.GetQualityLevel()];
        string renderer = benchmarkCamera.GetUniversalAdditionalCameraData().scriptableRenderer.GetType().FullName;
        if (!string.Equals(quality, benchmarkQualityLevel, StringComparison.Ordinal) ||
            GraphicsSettings.currentRenderPipeline != benchmarkPipelineAsset ||
            !string.Equals(renderer, benchmarkRendererName, StringComparison.Ordinal) ||
            Screen.width != benchmarkWidth || Screen.height != benchmarkHeight ||
            QualitySettings.vSyncCount != 0)
        {
            throw new InvalidOperationException(
                $"Benchmark environment drifted: Quality={quality}, Pipeline={GraphicsSettings.currentRenderPipeline?.name}, " +
                $"Renderer={renderer}, Resolution={Screen.width}x{Screen.height}, VSync={QualitySettings.vSyncCount}.");
        }
    }

    private void ReleaseRuntimeDatabase()
    {
        if (vegetationRenderer != null && savedDatabase != null)
        {
            vegetationRenderer.database = savedDatabase;
            vegetationRenderer.Rebuild();
        }
        if (benchmarkRuntimeDatabase != null)
        {
            Destroy(benchmarkRuntimeDatabase);
            benchmarkRuntimeDatabase = null;
        }
    }

    private void StopBenchmarkAndRestore()
    {
        if (!benchmarkRunning) return;
        benchmarkSampling = false;
        benchmarkRunning = false;
        Application.logMessageReceivedThreaded -= OnBenchmarkLogMessage;
        VegetationDiagnostics.BenchmarkAdditionalLights -= OnBenchmarkAdditionalLights;
        VegetationDiagnostics.BenchmarkShadowCandidateSlots -= OnBenchmarkShadowCandidateSlots;
        VegetationDiagnostics.BenchmarkForwardReadback -= OnBenchmarkForwardReadback;
        WriteBenchmarkLogAudit();
        benchmarkSummaryWriter?.Dispose(); benchmarkSummaryWriter = null;
        benchmarkRawWriter?.Dispose(); benchmarkRawWriter = null;
        benchmarkReadbackWriter?.Dispose(); benchmarkReadbackWriter = null;

        if (vegetationRenderer != null)
        {
            vegetationRenderer.enableGPUCulling = savedGPUCull;
            vegetationRenderer.disableGPUCulling = savedDisableGPUCull;
            vegetationRenderer.enableChunkCulling = savedChunkCull;
            vegetationRenderer.enableGPULOD = savedGPULOD;
            vegetationRenderer.enableShadowChunkCulling = savedShadowChunk;
            vegetationRenderer.enableShadowInfluenceCulling = savedShadowInfluence;
            vegetationRenderer.shadowLODOffset = savedShadowLODOffset;
            vegetationRenderer.enableShadowWindLOD = savedShadowWind;
            vegetationRenderer.enableShadowAlphaCutoffLOD = savedShadowAlpha;
            vegetationRenderer.diagnosticsMode = savedDiagnosticsMode;
            vegetationRenderer.enableDiagnosticsGPUReadback = savedGPUReadback;
            vegetationRenderer.diagnosticsGPUReadbackInterval = savedGPUReadbackInterval;
            vegetationRenderer.forceGameCameraOnly = savedForceGameOnly;
            vegetationRenderer.targetCamera = savedTargetCamera;
        }
        ReleaseRuntimeDatabase();
        if (benchmarkRendererFeature != null)
        {
            benchmarkRendererFeature.settings.enableDepthNormalsPass = savedFeatureDepth;
            benchmarkRendererFeature.settings.enableAdditionalLights = savedFeatureLights;
            benchmarkRendererFeature.settings.maxVegetationAdditionalLights = savedFeatureLightCap;
        }
        if (benchmarkCamera != null)
        {
            benchmarkCamera.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
            benchmarkCamera.fieldOfView = savedFOV;
        }
        foreach (var item in savedCameras) if (item.Key != null) item.Key.enabled = item.Value;
        foreach (var item in savedLights) if (item.Key != null) item.Key.enabled = item.Value;
        foreach (var item in savedRenderers) if (item.Key != null) item.Key.enabled = item.Value;
        RenderSettings.sun = savedSun;
        if (benchmarkLightRoot != null) Destroy(benchmarkLightRoot);
        benchmarkLightRoot = null;
        benchmarkLights.Clear();
        enableDiagnostics = savedEnableDiagnostics;
        showRuntimeOverlay = savedOverlay;
        writeCSV = savedWriteCSV;
        QualitySettings.SetQualityLevel(savedQualityLevel, true);
        QualitySettings.vSyncCount = savedVSync;
        Application.targetFrameRate = savedTargetFrameRate;
        Screen.SetResolution(savedWidth, savedHeight, savedFullScreenMode);
        if (benchmarkCameraObject != null) Destroy(benchmarkCameraObject);
        benchmarkCameraObject = null;
        benchmarkCamera = benchmarkSourceCamera;
        benchmarkSourceCamera = null;
        benchmarkCoroutine = null;
    }

    private void OpenBenchmarkCSV()
    {
        benchmarkTimestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        benchmarkDirectory = Path.Combine(Application.persistentDataPath, "VegetationBenchmark", benchmarkTimestamp);
        Directory.CreateDirectory(benchmarkDirectory);
        benchmarkSummaryWriter = NewCSV("summary.csv");
        benchmarkRawWriter = NewCSV("raw_frames.csv");
        benchmarkReadbackWriter = NewCSV("readbacks.csv");
        benchmarkSummaryWriter.WriteLine("TestName,InstanceScale,TimestampUTC,BuildOrEditor,Resolution,ResolutionMatch,Quality,URPRenderer,FeatureFlags,SampleCount,AverageFrameMS,P50FrameMS,P95FrameMS,P99FrameMS,AverageCPUFrameMS,CPUCoverage,AverageGPUFrameMS,GPUCoverage,AverageFPS,AverageGCAllocBytes,DatabaseInstanceCount,ActiveSpeciesCount,ChunkCount,AverageVisibleRenderGroupChunkRangeCount,AverageForwardVisibleInstanceCount,AverageForwardLODReferences,AverageLOD0,AverageLOD1,AverageLOD2,AverageLOD3,LODReadbackFrames,AverageCullDispatchCount,AverageIndirectDrawCount,AverageShadowCandidateCount,AverageShadowCandidateSlotReferenceCount,AverageShadowVisibleCount,AverageShadowDispatchCount,AverageShadowDrawCount,AverageCameraAdditionalLights,AverageUsedAdditionalLights,ResultGroup,BenchmarkWarningCount,BenchmarkErrorCount,PortfolioEligible");
        benchmarkRawWriter.WriteLine("TestName,InstanceScale,Frame,PathT,FrameMS,CPUFrameMS,GPUFrameMS,GCAllocBytes,VisibleRenderGroupChunkRangeCount,ForwardCullDispatchCount,ForwardIndirectDrawCount,ShadowDispatchCount,ShadowIndirectDrawCount,CameraAdditionalLights,UsedAdditionalLights,ShadowCandidateSlotReferenceCount");
        benchmarkReadbackWriter.WriteLine("TestName,InstanceScale,Frame,GroupCount,Complete,LOD0,LOD1,LOD2,LOD3,LODReferences,CrossFade,ForwardVisibleInstanceCount");
        File.WriteAllText(Path.Combine(benchmarkDirectory, "method.txt"),
            "B1 Vegetation Benchmark | Unity " + Application.unityVersion + Environment.NewLine +
            "Source database: " + benchmarkSourceDatabase.name + Environment.NewLine +
            "Quality: " + QualitySettings.names[QualitySettings.GetQualityLevel()] +
            "; URP Asset: " + benchmarkPipelineAsset.name +
            "; Renderer: " + benchmarkRendererName + Environment.NewLine +
            "Path: " + benchmarkPathStart.ToString("F3") + " -> " + benchmarkPathEnd.ToString("F3") +
            ", FOV " + benchmarkFOV.ToString("F2", CultureInfo.InvariantCulture) + Environment.NewLine +
            "Warm-up " + EffectiveWarmupSeconds.ToString("F2", CultureInfo.InvariantCulture) +
            "s; sample " + EffectiveSampleSeconds.ToString("F2", CultureInfo.InvariantCulture) +
            "s; first 8 frame samples discarded for FrameTimingManager latency." + Environment.NewLine +
            "Frame percentiles use sorted nearest rank: ceil(p*n)-1. FrameMS uses Time.unscaledDeltaTime." + Environment.NewLine +
            "CPU/GPU use FrameTimingManager completed frames; GPU is N/A below 80% valid coverage." + Environment.NewLine +
            "GPU per-LOD counters reuse VegetationDiagnostics asynchronous readback every 120 passes (2 in smoke)." + Environment.NewLine +
            "When GPU LOD is disabled, ForwardVisibleInstanceCount is the direct CSCullOnly LOD0 append count; it is not inferred from the LOD distribution columns." + Environment.NewLine +
            "Forward LOD references can double-count one instance during crossfade. Unique visible is N/A then." + Environment.NewLine +
            "Visible render-group chunk ranges are summed per RenderGroup and are not unique Database chunks." + Environment.NewLine +
            "Shadow candidate slot references are dispatch workload including reserved slots, not unique live instances. Exact candidate and visible counts are N/A." + Environment.NewLine +
            "Player log audit classifies Unity compute-property-not-set messages as warnings even when their text has no Warning prefix." + Environment.NewLine +
            "Editor results validate the harness only; use a standalone player for portfolio numbers." + Environment.NewLine,
            new UTF8Encoding(false));
    }

    private void ResetBenchmarkLogAudit()
    {
        lock (benchmarkLogAuditLock)
        {
            benchmarkLogAuditMessages.Clear();
            benchmarkActiveAuditCase = null;
            benchmarkCaseWarningCount = benchmarkCaseErrorCount = 0;
            benchmarkTotalWarningCount = benchmarkTotalErrorCount = 0;
        }
    }

    private void BeginBenchmarkCaseAudit(string caseName)
    {
        lock (benchmarkLogAuditLock)
        {
            benchmarkActiveAuditCase = caseName;
            benchmarkCaseWarningCount = benchmarkCaseErrorCount = 0;
        }
    }

    private void EndBenchmarkCaseAudit()
    {
        lock (benchmarkLogAuditLock) benchmarkActiveAuditCase = null;
    }

    private void OnBenchmarkLogMessage(string condition, string stackTrace, LogType type)
    {
        string message = condition == null ? string.Empty : condition.TrimEnd();
        bool computePropertyNotSet = message.StartsWith("Compute shader (", StringComparison.Ordinal) &&
            message.IndexOf("): Property (", StringComparison.Ordinal) >= 0 &&
            message.IndexOf(" at kernel index (", StringComparison.Ordinal) >= 0 &&
            message.EndsWith(" is not set", StringComparison.Ordinal);
        bool warning = type == LogType.Warning || computePropertyNotSet;
        bool error = !computePropertyNotSet &&
            (type == LogType.Error || type == LogType.Assert || type == LogType.Exception);
        if (!warning && !error) return;

        lock (benchmarkLogAuditLock)
        {
            string caseName = string.IsNullOrEmpty(benchmarkActiveAuditCase) ? "OutsideCase" : benchmarkActiveAuditCase;
            if (error) { benchmarkCaseErrorCount++; benchmarkTotalErrorCount++; }
            else { benchmarkCaseWarningCount++; benchmarkTotalWarningCount++; }
            benchmarkLogAuditMessages.Add((error ? "Error" : "Warning") + "," + caseName + "," + message.Replace('\r', ' ').Replace('\n', ' '));
        }
    }

    private void GetBenchmarkCaseAuditCounts(out int warnings, out int errors)
    {
        lock (benchmarkLogAuditLock)
        {
            warnings = benchmarkCaseWarningCount;
            errors = benchmarkCaseErrorCount;
        }
    }

    private void WriteBenchmarkLogAudit()
    {
        if (string.IsNullOrEmpty(benchmarkDirectory)) return;
        lock (benchmarkLogAuditLock)
        {
            var report = new StringBuilder();
            report.Append("Warnings=").Append(benchmarkTotalWarningCount).AppendLine();
            report.Append("Errors=").Append(benchmarkTotalErrorCount).AppendLine();
            report.AppendLine("Severity,Case,Message");
            for (int i = 0; i < benchmarkLogAuditMessages.Count; i++) report.AppendLine(benchmarkLogAuditMessages[i]);
            File.WriteAllText(Path.Combine(benchmarkDirectory, "player_log_audit.txt"), report.ToString(), new UTF8Encoding(false));
        }
    }

    private StreamWriter NewCSV(string name)
    {
        return new StreamWriter(Path.Combine(benchmarkDirectory, name), false, new UTF8Encoding(false), 16384);
    }

    private float EffectiveWarmupSeconds => benchmarkSmoke ? 0.25f : benchmarkWarmupSeconds;
    private float EffectiveSampleSeconds => benchmarkSmoke ? 0.75f : benchmarkSampleSeconds;

    private static string Number(double value)
    {
        return double.IsNaN(value) || double.IsInfinity(value)
            ? "N/A" : value.ToString("F3", CultureInfo.InvariantCulture);
    }

    private static string CSVText(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static double Percentile(List<BenchmarkFrame> samples, double percentile)
    {
        if (samples.Count == 0) return double.NaN;
        float[] sorted = new float[samples.Count];
        for (int i = 0; i < samples.Count; i++) sorted[i] = samples[i].frameMS;
        Array.Sort(sorted);
        int index = Mathf.Clamp((int)Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1);
        return sorted[index];
    }

    private void WriteBenchmarkCase(BenchmarkCase test)
    {
        if (benchmarkSummaryWriter == null) return;
        int n = benchmarkFrames.Count;
        double frameSum = 0, cpuSum = 0, gpuSum = 0, gcSum = 0;
        double visibleRenderGroupChunkRanges = 0, dispatches = 0, draws = 0, shadowDispatches = 0, shadowDraws = 0;
        int cpuCount = 0, gpuCount = 0, gcCount = 0;
        int lightCount = 0, shadowCount = 0;
        double usedLights = 0, cameraLights = 0, shadowCandidateSlots = 0;

        foreach (BenchmarkFrame frame in benchmarkFrames)
        {
            frameSum += frame.frameMS;
            if (!float.IsNaN(frame.cpuMS)) { cpuSum += frame.cpuMS; cpuCount++; }
            if (!float.IsNaN(frame.gpuMS)) { gpuSum += frame.gpuMS; gpuCount++; }
            if (frame.gcBytes >= 0) { gcSum += frame.gcBytes; gcCount++; }
            visibleRenderGroupChunkRanges += frame.visibleRenderGroupChunkRanges;
            dispatches += frame.dispatches;
            draws += frame.draws;
            shadowDispatches += frame.shadowDispatches;
            shadowDraws += frame.shadowDraws;
            if (benchmarkUsedLights.TryGetValue(frame.frame, out int used))
            {
                usedLights += used;
                cameraLights += benchmarkCameraLights[frame.frame];
                lightCount++;
            }
            if (benchmarkShadowCandidateSlots.TryGetValue(frame.frame, out int candidateSlots))
            {
                shadowCandidateSlots += candidateSlots;
                shadowCount++;
            }
            benchmarkRawWriter.WriteLine(CSVText(test.name) + "," + test.scale + "," + frame.frame + "," +
                Number(frame.pathT) + "," + Number(frame.frameMS) + "," + Number(frame.cpuMS) + "," +
                Number(frame.gpuMS) + "," + (frame.gcBytes >= 0 ? frame.gcBytes.ToString() : "N/A") + "," +
                frame.visibleRenderGroupChunkRanges + "," + frame.dispatches + "," + frame.draws + "," +
                frame.shadowDispatches + "," + frame.shadowDraws + "," +
                (benchmarkCameraLights.TryGetValue(frame.frame, out int visibleLights) ? visibleLights.ToString() : "N/A") + "," +
                (benchmarkUsedLights.TryGetValue(frame.frame, out used) ? used.ToString() : "N/A") + "," +
                (benchmarkShadowCandidateSlots.TryGetValue(frame.frame, out candidateSlots) ? candidateSlots.ToString() : "N/A"));
        }

        int lodFrames = 0;
        double lod0Sum = 0, lod1Sum = 0, lod2Sum = 0, lod3Sum = 0;
        int forwardVisibleFrames = 0;
        double forwardVisibleSum = 0;
        foreach (var item in benchmarkLODReadbacks)
        {
            int frame = item.Key;
            if (frame < benchmarkSampleStartFrame || frame > benchmarkSampleEndFrame) continue;
            LODReadbackFrame data = item.Value;
            bool complete = data.valid && data.groups == vegetationRenderer.RenderGroupCount;
            long references = data.lod0 + data.lod1 + data.lod2 + data.lod3;
            benchmarkReadbackWriter.WriteLine(CSVText(test.name) + "," + test.scale + "," + frame + "," +
                data.groups + "," + complete + "," + (complete ? data.lod0.ToString() : "N/A") + "," +
                (complete ? data.lod1.ToString() : "N/A") + "," +
                (complete ? data.lod2.ToString() : "N/A") + "," +
                (complete ? data.lod3.ToString() : "N/A") + "," +
                (complete ? references.ToString() : "N/A") + "," + data.crossFade + "," +
                (complete && data.forwardVisibleValid ? data.forwardVisibleInstances.ToString() : "N/A"));
            if (!complete) continue;
            lodFrames++;
            lod0Sum += data.lod0; lod1Sum += data.lod1; lod2Sum += data.lod2; lod3Sum += data.lod3;
            if (data.forwardVisibleValid)
            {
                forwardVisibleFrames++;
                forwardVisibleSum += data.forwardVisibleInstances;
            }
        }

        double avgFrame = n > 0 ? frameSum / n : double.NaN;
        double avgCPU = cpuCount > 0 ? cpuSum / cpuCount : double.NaN;
        bool gpuReliable = n > 0 && FrameTimingManager.IsFeatureEnabled() && gpuCount >= n * 0.8;
        double avgGPU = gpuReliable ? gpuSum / gpuCount : double.NaN;
        double avgRefs = lodFrames > 0 ? (lod0Sum + lod1Sum + lod2Sum + lod3Sum) / lodFrames : double.NaN;
        double avgForwardVisible = forwardVisibleFrames > 0 ? forwardVisibleSum / forwardVisibleFrames : double.NaN;
        string build = Application.isEditor ? "Editor" : Debug.isDebugBuild ? "Development" : "Release";
        string quality = QualitySettings.names[QualitySettings.GetQualityLevel()];
        string rendererName = benchmarkCamera.GetUniversalAdditionalCameraData().scriptableRenderer.GetType().Name +
            "/" + (GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "N/A");
        string resolution = Screen.width + "x" + Screen.height;
        bool resolutionMatch = Screen.width == benchmarkWidth && Screen.height == benchmarkHeight;
        string flags = $"CPUChunk={vegetationRenderer.enableChunkCulling};GPUCull={vegetationRenderer.enableGPUCulling};" +
            $"MeshDensityLOD={vegetationRenderer.enableGPULOD};ShadowChunk={vegetationRenderer.enableShadowChunkCulling};" +
            $"ShadowInfluence={vegetationRenderer.enableShadowInfluenceCulling};ShadowLODOffset={vegetationRenderer.shadowLODOffset};" +
            $"ShadowWindLOD={vegetationRenderer.enableShadowWindLOD};ShadowAlphaLOD={vegetationRenderer.enableShadowAlphaCutoffLOD};" +
            $"DepthNormals={benchmarkRendererFeature.settings.enableDepthNormalsPass};AdditionalLightsCap={benchmarkRendererFeature.settings.maxVegetationAdditionalLights};" +
            $"DiagnosticReadbackInterval={vegetationRenderer.diagnosticsGPUReadbackInterval}";
        GetBenchmarkCaseAuditCounts(out int benchmarkWarnings, out int benchmarkErrors);
        bool portfolioEligible = !Application.isEditor && resolutionMatch && n >= 100 && gpuReliable &&
            benchmarkWarnings == 0 && benchmarkErrors == 0;

        var row = new StringBuilder(640);
        row.Append(CSVText(test.name)).Append(',').Append(test.scale).Append(',')
            .Append(CSVText(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture))).Append(',')
            .Append(build).Append(',').Append(resolution).Append(',').Append(resolutionMatch).Append(',')
            .Append(CSVText(quality)).Append(',').Append(CSVText(rendererName)).Append(',').Append(CSVText(flags)).Append(',')
            .Append(n).Append(',').Append(Number(avgFrame)).Append(',').Append(Number(Percentile(benchmarkFrames, 0.50))).Append(',')
            .Append(Number(Percentile(benchmarkFrames, 0.95))).Append(',').Append(Number(Percentile(benchmarkFrames, 0.99))).Append(',')
            .Append(Number(avgCPU)).Append(',').Append(Number(n > 0 ? cpuCount / (double)n : double.NaN)).Append(',')
            .Append(Number(avgGPU)).Append(',').Append(Number(n > 0 ? gpuCount / (double)n : double.NaN)).Append(',')
            .Append(Number(avgFrame > 0 ? 1000.0 / avgFrame : double.NaN)).Append(',')
            .Append(Number(gcCount > 0 ? gcSum / gcCount : double.NaN)).Append(',')
            .Append(benchmarkRuntimeDatabase.TotalInstanceCount).Append(',').Append(benchmarkRuntimeDatabase.SpeciesCount).Append(',')
            .Append(benchmarkRuntimeDatabase.ChunkCount).Append(',').Append(Number(n > 0 ? visibleRenderGroupChunkRanges / n : double.NaN)).Append(',')
            .Append(Number(avgForwardVisible)).Append(',').Append(Number(avgRefs)).Append(',')
            .Append(Number(lodFrames > 0 ? lod0Sum / lodFrames : double.NaN)).Append(',')
            .Append(Number(lodFrames > 0 ? lod1Sum / lodFrames : double.NaN)).Append(',')
            .Append(Number(lodFrames > 0 ? lod2Sum / lodFrames : double.NaN)).Append(',')
            .Append(Number(lodFrames > 0 ? lod3Sum / lodFrames : double.NaN)).Append(',')
            .Append(lodFrames).Append(',').Append(Number(n > 0 ? dispatches / n : double.NaN)).Append(',')
            .Append(Number(n > 0 ? draws / n : double.NaN)).Append(',')
            .Append("N/A,").Append(Number(shadowCount > 0 ? shadowCandidateSlots / shadowCount : double.NaN)).Append(',')
            .Append("N/A,").Append(Number(n > 0 ? shadowDispatches / n : double.NaN)).Append(',')
            .Append(Number(n > 0 ? shadowDraws / n : double.NaN)).Append(',')
            .Append(Number(lightCount > 0 ? cameraLights / lightCount : double.NaN)).Append(',')
            .Append(Number(lightCount > 0 ? usedLights / lightCount : double.NaN)).Append(',')
            .Append(CSVText(test.resultGroup)).Append(',')
            .Append(benchmarkWarnings).Append(',').Append(benchmarkErrors).Append(',')
            .Append(portfolioEligible);
        benchmarkSummaryWriter.WriteLine(row.ToString());
        benchmarkSummaryWriter.Flush(); benchmarkRawWriter.Flush(); benchmarkReadbackWriter.Flush();
        Debug.Log($"[VegetationBenchmark] {test.name} {test.scale:N0}: mean={Number(avgFrame)} ms, " +
            $"P95={Number(Percentile(benchmarkFrames, 0.95))} ms, GPU={Number(avgGPU)} ms, " +
            $"samples={n}, LOD readbacks={lodFrames}. {benchmarkDirectory}", this);
    }
}
