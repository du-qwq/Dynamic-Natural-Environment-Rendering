using System;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
[AddComponentMenu("Vegetation/Vegetation Performance Diagnostic")]
public sealed class VegetationPerformanceDiagnostic : MonoBehaviour
{
    [Header("General")]
    public bool enableDiagnostics = true;
    [Min(0.1f)] public float sampleInterval = 1f;
    [Min(1f)] public float rollingAverageSeconds = 5f;
    public bool showRuntimeOverlay = true;
    public bool writeCSV;
    [Min(0.1f)] public float csvSampleInterval = 1f;

    [Header("Vegetation")]
    public VegetationRenderer vegetationRenderer;
    public VegetationDatabase vegetationDatabase;
    public VegetationColliderManager vegetationColliderManager;

    [Header("Warnings")]
    [Min(1f)] public float frameTimeWarning = 20f;
    public bool databaseRevisionWarning = true;
    [Min(1)] public int cameraCountWarning = 2;
    [Min(1)] public int colliderCountWarning = 500;
    public bool memoryGrowthWarning = true;
    [Min(1f)] public float memoryGrowthWarningMB = 16f;

    private const int FrameHistoryCapacity = 2048;
    private const int MemoryHistoryCapacity = 128;
    private const double StartupWindowSeconds = 10.0;
    private const double DegradationAnalysisStartSeconds = 60.0;
    private const double MemoryTrendSeconds = 30.0;
    private const double Megabyte = 1024.0 * 1024.0;

    private readonly FrameTiming[] frameTimings = new FrameTiming[1];
    private readonly double[] frameTimes = new double[FrameHistoryCapacity];
    private readonly float[] frameMilliseconds = new float[FrameHistoryCapacity];
    private readonly float[] cpuMilliseconds = new float[FrameHistoryCapacity];
    private readonly float[] gpuMilliseconds = new float[FrameHistoryCapacity];
    private readonly double[] memoryTimes = new double[MemoryHistoryCapacity];
    private readonly long[] memoryValues = new long[MemoryHistoryCapacity];
    private readonly StringBuilder overlayBuilder = new StringBuilder(4096);

    private ProfilerRecorder gcAllocatedRecorder;
    private ProfilerRecorder gcUsedRecorder;
    private ProfilerRecorder totalReservedRecorder;
    private ProfilerRecorder totalUsedRecorder;
    private ProfilerRecorder monoUsedRecorder;
    private ProfilerRecorder monoHeapRecorder;
    private ProfilerRecorder gfxUsedRecorder;

    private Camera[] cameras = new Camera[8];
    private GUIStyle overlayStyle;
    private string overlayText = "Performance Diagnostic\nWaiting for first sample...";
    private float overlayHeight = 120f;
    private StreamWriter csvWriter;
    private string csvPath;
    private int csvWritesSinceFlush;

    private int frameWriteIndex;
    private int frameHistoryCount;
    private int memoryWriteIndex;
    private int memoryHistoryCount;
    private double startTime;
    private double nextSampleTime;
    private double nextCSVTime;
    private double startupFrameSum;
    private double startupCpuSum;
    private double startupGpuSum;
    private int startupFrameCount;
    private int startupCpuCount;
    private int startupGpuCount;
    private bool startupCaptured;
    private float startupFPS;
    private float startupCPUms;
    private float startupGPUms;

    private bool initialStateCaptured;
    private bool initialDatabaseCaptured;
    private long initialMemoryUsed;
    private int initialDatabaseRevision;
    private int initialInstanceCount;
    private int initialCameraCount;
    private int initialShadowStateCount;
    private int initialActiveColliderCount;
    private int initialPooledColliderCount;
    private int lastDatabaseRevision;
    private int lastInstanceCount;
    private bool databaseChangedDuringPlay;
    private float databaseChangeTime;
    private int databaseChangeOldRevision;
    private int databaseChangeNewRevision;
    private int instanceChangeOldCount;
    private int instanceChangeNewCount;
    private bool memoryGrowing;
    private Snapshot snapshot;

    private struct TimingSummary
    {
        public float frameCurrent, frameAverage, frameMinimum, frameMaximum;
        public float cpuCurrent, cpuAverage, cpuMinimum, cpuMaximum;
        public float gpuCurrent, gpuAverage, gpuMinimum, gpuMaximum;
        public int frameCount, cpuCount, gpuCount;
    }

    private struct Snapshot
    {
        public double playTime;
        public TimingSummary timing;
        public float fps;
        public long gcAllocated, gcUsed, totalReserved, totalUsed, monoUsed, monoHeap, gfxUsed;
        public int dataRevision, instanceCount, speciesCount, chunkCount, renderGroupCount, rendererChunkRanges, visibleRanges;
        public int forwardCameras, shadowStates, forwardDispatches, forwardDraws, shadowDispatches, shadowDraws;
        public int cameraCount, activeColliders, pooledColliders, scannedInstances, scannedChunks;
    }

    private void OnEnable()
    {
        ResetSession();
        ResolveReferences();
        StartRecorders();
        startTime = Time.realtimeSinceStartupAsDouble;
        nextSampleTime = startTime;
        nextCSVTime = startTime;
        initialDatabaseRevision = vegetationDatabase != null ? vegetationDatabase.DataRevision : 0;
        initialInstanceCount = vegetationDatabase != null ? vegetationDatabase.TotalInstanceCount : 0;
        initialDatabaseCaptured = vegetationDatabase != null;
        lastDatabaseRevision = initialDatabaseRevision;
        lastInstanceCount = initialInstanceCount;
    }

    private void ResetSession()
    {
        frameWriteIndex = 0;
        frameHistoryCount = 0;
        memoryWriteIndex = 0;
        memoryHistoryCount = 0;
        startupFrameSum = 0.0;
        startupCpuSum = 0.0;
        startupGpuSum = 0.0;
        startupFrameCount = 0;
        startupCpuCount = 0;
        startupGpuCount = 0;
        startupCaptured = false;
        startupFPS = 0f;
        startupCPUms = 0f;
        startupGPUms = 0f;
        initialStateCaptured = false;
        initialDatabaseCaptured = false;
        databaseChangedDuringPlay = false;
        memoryGrowing = false;
        csvPath = null;
        snapshot = default;
    }

    private void OnDisable()
    {
        CloseCSV();
        DisposeRecorder(ref gcAllocatedRecorder);
        DisposeRecorder(ref gcUsedRecorder);
        DisposeRecorder(ref totalReservedRecorder);
        DisposeRecorder(ref totalUsedRecorder);
        DisposeRecorder(ref monoUsedRecorder);
        DisposeRecorder(ref monoHeapRecorder);
        DisposeRecorder(ref gfxUsedRecorder);
    }

    private void OnValidate()
    {
        sampleInterval = Mathf.Max(0.1f, sampleInterval);
        csvSampleInterval = Mathf.Max(0.1f, csvSampleInterval);
        rollingAverageSeconds = Mathf.Clamp(rollingAverageSeconds, 1f, 30f);
        frameTimeWarning = Mathf.Max(1f, frameTimeWarning);
        cameraCountWarning = Mathf.Max(1, cameraCountWarning);
        colliderCountWarning = Mathf.Max(1, colliderCountWarning);
        memoryGrowthWarningMB = Mathf.Max(1f, memoryGrowthWarningMB);
    }

    private void Update()
    {
        if (!enableDiagnostics)
        {
            if (csvWriter != null) CloseCSV();
            return;
        }

        CaptureFrameTiming();
        double now = Time.realtimeSinceStartupAsDouble;
        bool overlayDue = now >= nextSampleTime;
        bool csvDue = writeCSV && now >= nextCSVTime;
        if (!overlayDue && !csvDue) return;

        CollectSnapshot(now);
        if (overlayDue)
        {
            nextSampleTime = now + sampleInterval;
            BuildOverlay();
        }
        if (csvDue)
        {
            nextCSVTime = now + csvSampleInterval;
            WriteCSVRow();
        }
        if (!writeCSV && csvWriter != null) CloseCSV();
    }

    private void CaptureFrameTiming()
    {
        FrameTimingManager.CaptureFrameTimings();
        float cpu = 0f;
        float gpu = 0f;
        if (FrameTimingManager.GetLatestTimings(1, frameTimings) > 0)
        {
            cpu = (float)frameTimings[0].cpuFrameTime;
            gpu = (float)frameTimings[0].gpuFrameTime;
        }

        float frame = Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime * 1000f : 0f;
        double now = Time.realtimeSinceStartupAsDouble;
        frameTimes[frameWriteIndex] = now;
        frameMilliseconds[frameWriteIndex] = frame;
        cpuMilliseconds[frameWriteIndex] = cpu;
        gpuMilliseconds[frameWriteIndex] = gpu;
        frameWriteIndex = (frameWriteIndex + 1) % FrameHistoryCapacity;
        if (frameHistoryCount < FrameHistoryCapacity) frameHistoryCount++;

        if (!startupCaptured && now - startTime <= StartupWindowSeconds)
        {
            if (frame > 0f) { startupFrameSum += frame; startupFrameCount++; }
            if (cpu > 0f) { startupCpuSum += cpu; startupCpuCount++; }
            if (gpu > 0f) { startupGpuSum += gpu; startupGpuCount++; }
        }
        else if (!startupCaptured)
        {
            startupCaptured = true;
            float startupFrame = startupFrameCount > 0 ? (float)(startupFrameSum / startupFrameCount) : 0f;
            startupFPS = startupFrame > 0f ? 1000f / startupFrame : 0f;
            startupCPUms = startupCpuCount > 0 ? (float)(startupCpuSum / startupCpuCount) : 0f;
            startupGPUms = startupGpuCount > 0 ? (float)(startupGpuSum / startupGpuCount) : 0f;
        }
    }

    private void CollectSnapshot(double now)
    {
        ResolveDatabaseReference();
        snapshot.playTime = now - startTime;
        snapshot.timing = CalculateTimingSummary(now);
        snapshot.fps = snapshot.timing.frameAverage > 0f ? 1000f / snapshot.timing.frameAverage : 0f;
        snapshot.gcAllocated = ReadRecorder(gcAllocatedRecorder, 0L);
        snapshot.gcUsed = ReadRecorder(gcUsedRecorder, GC.GetTotalMemory(false));
        snapshot.totalReserved = ReadRecorder(totalReservedRecorder, Profiler.GetTotalReservedMemoryLong());
        snapshot.totalUsed = ReadRecorder(totalUsedRecorder, Profiler.GetTotalAllocatedMemoryLong());
        snapshot.monoUsed = ReadRecorder(monoUsedRecorder, Profiler.GetMonoUsedSizeLong());
        snapshot.monoHeap = ReadRecorder(monoHeapRecorder, Profiler.GetMonoHeapSizeLong());
        snapshot.gfxUsed = ReadRecorder(gfxUsedRecorder, -1L);

        if (vegetationDatabase != null)
        {
            snapshot.dataRevision = vegetationDatabase.DataRevision;
            snapshot.instanceCount = vegetationRenderer != null && vegetationRenderer.UploadedInstanceCount > 0
                ? vegetationRenderer.UploadedInstanceCount : vegetationDatabase.TotalInstanceCount;
            snapshot.speciesCount = vegetationDatabase.SpeciesCount;
            snapshot.chunkCount = vegetationDatabase.ChunkCount;
        }
        else
        {
            snapshot.dataRevision = 0;
            snapshot.instanceCount = vegetationRenderer != null ? vegetationRenderer.UploadedInstanceCount : 0;
            snapshot.speciesCount = 0;
            snapshot.chunkCount = 0;
        }

        if (vegetationRenderer != null)
        {
            snapshot.renderGroupCount = vegetationRenderer.RenderGroupCount;
            snapshot.rendererChunkRanges = vegetationRenderer.ChunkRangeCount;
            snapshot.visibleRanges = vegetationRenderer.VisibleChunkRangeCount;
            snapshot.forwardCameras = vegetationRenderer.ForwardCameraCount;
            snapshot.shadowStates = vegetationRenderer.ShadowCameraStateCount;
            snapshot.forwardDispatches = vegetationRenderer.ForwardDispatchCount;
            snapshot.forwardDraws = vegetationRenderer.ForwardDrawCount;
            snapshot.shadowDispatches = vegetationRenderer.ShadowDispatchCount;
            snapshot.shadowDraws = vegetationRenderer.ShadowDrawCount;
        }

        SampleCameras();
        if (vegetationColliderManager != null)
        {
            snapshot.activeColliders = vegetationColliderManager.ActiveColliderCount;
            snapshot.pooledColliders = vegetationColliderManager.PooledColliderCount;
            snapshot.scannedInstances = vegetationColliderManager.ScannedInstanceCount;
            snapshot.scannedChunks = vegetationColliderManager.ScannedChunkCount;
        }

        // ProfilerRecorder memory counters can report zero on their first frame. Do not
        // lock the session delta baseline until a real total-memory sample is available.
        if (!initialStateCaptured && snapshot.totalUsed > 0)
        {
            initialStateCaptured = true;
            initialMemoryUsed = snapshot.totalUsed;
            initialCameraCount = snapshot.cameraCount;
            initialShadowStateCount = snapshot.shadowStates;
            initialActiveColliderCount = snapshot.activeColliders;
            initialPooledColliderCount = snapshot.pooledColliders;
            if (!initialDatabaseCaptured)
            {
                initialDatabaseCaptured = true;
                initialDatabaseRevision = snapshot.dataRevision;
                initialInstanceCount = snapshot.instanceCount;
                lastDatabaseRevision = snapshot.dataRevision;
                lastInstanceCount = snapshot.instanceCount;
            }
        }

        if (initialDatabaseCaptured && (snapshot.dataRevision != lastDatabaseRevision || snapshot.instanceCount != lastInstanceCount))
        {
            databaseChangedDuringPlay = true;
            databaseChangeTime = (float)snapshot.playTime;
            databaseChangeOldRevision = lastDatabaseRevision;
            databaseChangeNewRevision = snapshot.dataRevision;
            instanceChangeOldCount = lastInstanceCount;
            instanceChangeNewCount = snapshot.instanceCount;
            lastDatabaseRevision = snapshot.dataRevision;
            lastInstanceCount = snapshot.instanceCount;
        }

        memoryTimes[memoryWriteIndex] = now;
        memoryValues[memoryWriteIndex] = snapshot.totalUsed;
        memoryWriteIndex = (memoryWriteIndex + 1) % MemoryHistoryCapacity;
        if (memoryHistoryCount < MemoryHistoryCapacity) memoryHistoryCount++;
        memoryGrowing = DetectMemoryGrowth(now);
    }

    private TimingSummary CalculateTimingSummary(double now)
    {
        TimingSummary result = default;
        result.frameMinimum = result.cpuMinimum = result.gpuMinimum = float.MaxValue;
        double frameSum = 0.0;
        double cpuSum = 0.0;
        double gpuSum = 0.0;
        double oldest = now - rollingAverageSeconds;

        for (int offset = 0; offset < frameHistoryCount; offset++)
        {
            int index = (frameWriteIndex - 1 - offset + FrameHistoryCapacity) % FrameHistoryCapacity;
            if (frameTimes[index] < oldest) break;
            float frame = frameMilliseconds[index];
            float cpu = cpuMilliseconds[index];
            float gpu = gpuMilliseconds[index];
            if (offset == 0)
            {
                result.frameCurrent = frame;
                result.cpuCurrent = cpu;
                result.gpuCurrent = gpu;
            }
            if (frame > 0f)
            {
                frameSum += frame;
                result.frameCount++;
                result.frameMinimum = Mathf.Min(result.frameMinimum, frame);
                result.frameMaximum = Mathf.Max(result.frameMaximum, frame);
            }
            if (cpu > 0f)
            {
                cpuSum += cpu;
                result.cpuCount++;
                result.cpuMinimum = Mathf.Min(result.cpuMinimum, cpu);
                result.cpuMaximum = Mathf.Max(result.cpuMaximum, cpu);
            }
            if (gpu > 0f)
            {
                gpuSum += gpu;
                result.gpuCount++;
                result.gpuMinimum = Mathf.Min(result.gpuMinimum, gpu);
                result.gpuMaximum = Mathf.Max(result.gpuMaximum, gpu);
            }
        }

        result.frameAverage = result.frameCount > 0 ? (float)(frameSum / result.frameCount) : 0f;
        result.cpuAverage = result.cpuCount > 0 ? (float)(cpuSum / result.cpuCount) : 0f;
        result.gpuAverage = result.gpuCount > 0 ? (float)(gpuSum / result.gpuCount) : 0f;
        if (result.frameMinimum == float.MaxValue) result.frameMinimum = 0f;
        if (result.cpuMinimum == float.MaxValue) result.cpuMinimum = 0f;
        if (result.gpuMinimum == float.MaxValue) result.gpuMinimum = 0f;
        return result;
    }

    private void SampleCameras()
    {
        int cameraCount = Camera.allCamerasCount;
        if (cameras.Length < cameraCount) Array.Resize(ref cameras, Mathf.NextPowerOfTwo(cameraCount));
        snapshot.cameraCount = Camera.GetAllCameras(cameras);
    }

    private bool DetectMemoryGrowth(double now)
    {
        if (!memoryGrowthWarning || memoryHistoryCount < 2) return false;
        int newestIndex = (memoryWriteIndex - 1 + MemoryHistoryCapacity) % MemoryHistoryCapacity;
        int oldestIndex = newestIndex;
        int sampleCount = 1;
        int risingSteps = 0;
        long newerValue = memoryValues[newestIndex];

        for (int offset = 1; offset < memoryHistoryCount; offset++)
        {
            int index = (memoryWriteIndex - 1 - offset + MemoryHistoryCapacity) % MemoryHistoryCapacity;
            if (newerValue >= memoryValues[index]) risingSteps++;
            newerValue = memoryValues[index];
            oldestIndex = index;
            sampleCount++;
            if (now - memoryTimes[index] >= MemoryTrendSeconds) break;
        }

        double duration = now - memoryTimes[oldestIndex];
        long growth = memoryValues[newestIndex] - memoryValues[oldestIndex];
        return duration >= MemoryTrendSeconds && growth >= memoryGrowthWarningMB * Megabyte && risingSteps >= (sampleCount - 1) * 0.65f;
    }

    private void BuildOverlay()
    {
        overlayBuilder.Clear();
        TimingSummary timing = snapshot.timing;
        float fpsCurrent = timing.frameCurrent > 0f ? 1000f / timing.frameCurrent : 0f;
        float fpsMinimum = timing.frameMaximum > 0f ? 1000f / timing.frameMaximum : 0f;
        float fpsMaximum = timing.frameMinimum > 0f ? 1000f / timing.frameMinimum : 0f;

        overlayBuilder.AppendLine("Performance Diagnostic");
        overlayBuilder.Append("FPS C/Avg/Min/Max  ").AppendFormat(CultureInfo.InvariantCulture, "{0:F1} / {1:F1} / {2:F1} / {3:F1}\n", fpsCurrent, snapshot.fps, fpsMinimum, fpsMaximum);
        AppendTiming("Frame", timing.frameCurrent, timing.frameAverage, timing.frameMinimum, timing.frameMaximum, true);
        AppendTiming("CPU Frame", timing.cpuCurrent, timing.cpuAverage, timing.cpuMinimum, timing.cpuMaximum, timing.cpuCount > 0);
        AppendTiming("GPU Frame", timing.gpuCurrent, timing.gpuAverage, timing.gpuMinimum, timing.gpuMaximum, timing.gpuCount > 0);
        overlayBuilder.Append("Bottleneck          ").AppendLine(GetBottleneck());
        overlayBuilder.AppendLine("--------------------------------");
        overlayBuilder.Append("VSync              ").Append(QualitySettings.vSyncCount).AppendLine();
        overlayBuilder.Append("Target FPS         ").Append(Application.targetFrameRate).AppendLine();
        overlayBuilder.Append("Resolution         ").Append(Screen.width).Append('x').Append(Screen.height).Append(" (display ").Append(Screen.currentResolution.width).Append('x').Append(Screen.currentResolution.height).AppendLine(")");
        overlayBuilder.Append("MSAA               ").Append(QualitySettings.antiAliasing).AppendLine("x");
        overlayBuilder.AppendLine("--------------------------------");
        AppendBytes("Memory Used", snapshot.totalUsed);
        AppendSignedBytes("Memory Delta", snapshot.totalUsed - initialMemoryUsed);
        AppendBytes("Memory Reserved", snapshot.totalReserved);
        AppendBytes("GC Alloc/frame", snapshot.gcAllocated);
        AppendBytes("GC Used", snapshot.gcUsed);
        AppendBytes("Mono Used / Heap", snapshot.monoUsed, snapshot.monoHeap);
        if (snapshot.gfxUsed >= 0) AppendBytes("Gfx Used", snapshot.gfxUsed);
        overlayBuilder.AppendLine("--------------------------------");
        overlayBuilder.AppendLine("Vegetation");
        overlayBuilder.Append("Instances          ").Append(snapshot.instanceCount).Append(" (initial ").Append(initialInstanceCount).AppendLine(")");
        overlayBuilder.Append("Render Groups      ").Append(snapshot.renderGroupCount).Append("   Species ").Append(snapshot.speciesCount).AppendLine();
        overlayBuilder.Append("Chunks             ").Append(snapshot.chunkCount).Append("   Visible Ranges ").Append(snapshot.visibleRanges).AppendLine();
        overlayBuilder.Append("Forward Dispatches ").Append(snapshot.forwardDispatches).Append("   Draws ").Append(snapshot.forwardDraws).AppendLine();
        overlayBuilder.Append("Shadow Dispatches  ").Append(snapshot.shadowDispatches).Append("   Draws ").Append(snapshot.shadowDraws).AppendLine();
        overlayBuilder.Append("Database Rev       ").Append(snapshot.dataRevision).Append(" (initial ").Append(initialDatabaseRevision).AppendLine(")");
        overlayBuilder.Append("Current Camera     ").AppendLine(vegetationRenderer != null && vegetationRenderer.CurrentCamera != null ? vegetationRenderer.CurrentCamera.name : "N/A");
        overlayBuilder.AppendLine("--------------------------------");
        overlayBuilder.Append("Cameras            ").Append(snapshot.cameraCount).Append("   Forward ").Append(snapshot.forwardCameras).AppendLine();
        overlayBuilder.Append("Shadow States      ").Append(snapshot.shadowStates).AppendLine();
        AppendCameraDetails();
        overlayBuilder.AppendLine("--------------------------------");
        overlayBuilder.AppendLine("Colliders");
        overlayBuilder.Append("Active             ").Append(snapshot.activeColliders).Append("   Pool ").Append(snapshot.pooledColliders).AppendLine();
        overlayBuilder.Append("Scanned Instances  ").Append(snapshot.scannedInstances).Append("   Chunks ").Append(snapshot.scannedChunks).AppendLine();
        overlayBuilder.AppendLine("--------------------------------");
        overlayBuilder.Append("Play Time          ").AppendFormat(CultureInfo.InvariantCulture, "{0:F1}s\n", snapshot.playTime);
        if (!string.IsNullOrEmpty(csvPath)) overlayBuilder.Append("CSV                ").AppendLine(csvPath);
        AppendWarnings();
        AppendDegradationAnalysis();
        overlayText = overlayBuilder.ToString();
        overlayHeight = Mathf.Min(Screen.height - 20f, 22f + CountLines(overlayText) * 17f);
    }

    private void AppendTiming(string label, float current, float average, float minimum, float maximum, bool available)
    {
        overlayBuilder.Append(label.PadRight(19));
        if (!available) { overlayBuilder.AppendLine("N/A"); return; }
        overlayBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} / {1:F2} / {2:F2} / {3:F2} ms\n", current, average, minimum, maximum);
    }

    private void AppendBytes(string label, long value)
    {
        overlayBuilder.Append(label.PadRight(19)).AppendFormat(CultureInfo.InvariantCulture, "{0:F2} MB\n", value / Megabyte);
    }

    private void AppendBytes(string label, long first, long second)
    {
        overlayBuilder.Append(label.PadRight(19)).AppendFormat(CultureInfo.InvariantCulture, "{0:F2} / {1:F2} MB\n", first / Megabyte, second / Megabyte);
    }

    private void AppendSignedBytes(string label, long value)
    {
        overlayBuilder.Append(label.PadRight(19)).AppendFormat(CultureInfo.InvariantCulture, "{0:+0.00;-0.00;0.00} MB\n", value / Megabyte);
    }

    private void AppendCameraDetails()
    {
        for (int i = 0; i < snapshot.cameraCount; i++)
        {
            Camera camera = cameras[i];
            if (camera == null) continue;
            UniversalAdditionalCameraData data = camera.GetComponent<UniversalAdditionalCameraData>();
            string renderType = data != null ? data.renderType.ToString() : "No URP Data";
            bool accepted = vegetationRenderer != null && vegetationRenderer.ShouldRenderForCamera(camera);
            overlayBuilder.Append("  - ").Append(camera.name).Append(" | ").Append(camera.cameraType).Append(" | enabled=").Append(camera.enabled)
                .Append(" | active=").Append(camera.gameObject.activeInHierarchy).Append(" | ").Append(renderType)
                .Append(" | vegetation=").Append(accepted).AppendLine();
        }
    }

    private void AppendWarnings()
    {
        overlayBuilder.AppendLine("--------------------------------\nWarnings");
        overlayBuilder.Append(databaseChangedDuringPlay && databaseRevisionWarning ? "[RED] DATABASE CHANGED DURING PLAY" : "[GREEN] Database stable").AppendLine();
        if (databaseChangedDuringPlay)
        {
            overlayBuilder.Append("      t=").AppendFormat(CultureInfo.InvariantCulture, "{0:F1}s, Rev {1} -> {2}, Instances {3} -> {4}\n", databaseChangeTime, databaseChangeOldRevision, databaseChangeNewRevision, instanceChangeOldCount, instanceChangeNewCount);
        }
        bool camerasGrowing = snapshot.cameraCount >= Mathf.Max(cameraCountWarning, initialCameraCount + 1) || snapshot.shadowStates > initialShadowStateCount;
        overlayBuilder.AppendLine(camerasGrowing ? "[RED] CAMERA STATE GROWING" : "[GREEN] Camera count stable");
        bool collidersGrowing = snapshot.activeColliders >= initialActiveColliderCount + colliderCountWarning || snapshot.pooledColliders >= initialPooledColliderCount + colliderCountWarning;
        overlayBuilder.AppendLine(collidersGrowing ? "[RED] COLLIDER COUNT GROWING" : "[GREEN] Collider count stable");
        overlayBuilder.AppendLine(memoryGrowing ? "[RED] MEMORY GROWING" : "[GREEN] Memory trend stable");
        if (snapshot.timing.frameAverage >= frameTimeWarning) overlayBuilder.AppendLine("[YELLOW] Frame time high");
        if (snapshot.timing.gpuCount > 0 && snapshot.timing.gpuAverage >= frameTimeWarning) overlayBuilder.AppendLine("[YELLOW] GPU frame time high");
        if (vegetationRenderer != null && vegetationRenderer.targetCamera == null) overlayBuilder.AppendLine("[YELLOW] TARGET CAMERA NOT ASSIGNED");
    }

    private void AppendDegradationAnalysis()
    {
        if (!startupCaptured) return;
        overlayBuilder.AppendLine("--------------------------------\nFPS Degradation Analysis");
        overlayBuilder.Append("Startup FPS / Current ").AppendFormat(CultureInfo.InvariantCulture, "{0:F1} / {1:F1}\n", startupFPS, snapshot.fps);
        overlayBuilder.Append("FPS degradation     ").AppendFormat(CultureInfo.InvariantCulture, "{0:+0.0;-0.0;0.0}%\n", PercentageChange(startupFPS, snapshot.fps));
        AppendChange("CPU increase", startupCPUms, snapshot.timing.cpuAverage);
        AppendChange("GPU increase", startupGPUms, snapshot.timing.gpuAverage);
        if (snapshot.playTime < DegradationAnalysisStartSeconds) { overlayBuilder.AppendLine("Conclusion          Waiting for 60s sample"); return; }

        float cpuIncrease = PercentageChange(startupCPUms, snapshot.timing.cpuAverage);
        float gpuIncrease = PercentageChange(startupGPUms, snapshot.timing.gpuAverage);
        string conclusion = "No dominant growth signal";
        if (snapshot.cameraCount > initialCameraCount || snapshot.shadowStates > initialShadowStateCount) conclusion = "Duplicate camera rendering / camera state growth";
        else if (databaseChangedDuringPlay) conclusion = "Vegetation database rebuilding/growing";
        else if (snapshot.activeColliders >= initialActiveColliderCount + colliderCountWarning || snapshot.pooledColliders >= initialPooledColliderCount + colliderCountWarning) conclusion = "Physics/collider workload growth";
        else if (memoryGrowing && snapshot.gcAllocated > 0) conclusion = "Possible allocation / memory problem";
        else if (gpuIncrease > 30f && cpuIncrease < gpuIncrease * 0.5f) conclusion = "GPU workload / clock / thermal / rendering workload. Check GPU temperature/clock externally";
        else if (cpuIncrease > 30f && gpuIncrease < cpuIncrease * 0.5f) conclusion = "CPU runtime workload growth";
        overlayBuilder.Append("Conclusion          ").AppendLine(conclusion);
    }

    private void AppendChange(string label, float initial, float current)
    {
        overlayBuilder.Append(label.PadRight(20));
        if (initial <= 0f || current <= 0f) { overlayBuilder.AppendLine("N/A"); return; }
        overlayBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0:F2} -> {1:F2} ms ({2:+0.0;-0.0;0.0}%)\n", initial, current, PercentageChange(initial, current));
    }

    private string GetBottleneck()
    {
        TimingSummary timing = snapshot.timing;
        if (IsPossibleFrameLimit()) return "Possible Frame Rate / VSync Limit";
        if (timing.cpuCount == 0 && timing.gpuCount == 0) return "N/A";
        if (timing.gpuCount > 0 && timing.gpuAverage > timing.cpuAverage * 1.15f) return "GPU";
        if (timing.cpuCount > 0 && (timing.gpuCount == 0 || timing.cpuAverage > timing.gpuAverage * 1.15f)) return "CPU";
        return "Balanced / Unknown";
    }

    private bool IsPossibleFrameLimit()
    {
        float fps = snapshot.fps;
        if (fps <= 0f) return false;
        bool timingsLow = (snapshot.timing.cpuCount == 0 || snapshot.timing.cpuAverage < snapshot.timing.frameAverage * 0.75f) &&
                          (snapshot.timing.gpuCount == 0 || snapshot.timing.gpuAverage < snapshot.timing.frameAverage * 0.75f);
        if (!timingsLow) return false;
        if (Application.targetFrameRate > 0 && Mathf.Abs(fps - Application.targetFrameRate) <= Mathf.Max(2f, Application.targetFrameRate * 0.05f)) return true;
        if (QualitySettings.vSyncCount <= 0) return false;
        int refresh = Screen.currentResolution.refreshRate;
        float expected = refresh > 0 ? refresh / (float)QualitySettings.vSyncCount : 0f;
        return expected > 0f && Mathf.Abs(fps - expected) <= Mathf.Max(2f, expected * 0.05f);
    }

    private void WriteCSVRow()
    {
        EnsureCSV();
        if (csvWriter == null) return;
        TimingSummary timing = snapshot.timing;
        csvWriter.Write(snapshot.playTime.ToString("F3", CultureInfo.InvariantCulture));
        WriteCSVValue(timing.frameCurrent > 0f ? 1000f / timing.frameCurrent : 0f);
        WriteCSVValue(snapshot.fps);
        WriteCSVValue(timing.frameCurrent); WriteCSVValue(timing.frameAverage);
        if (timing.cpuCount > 0) WriteCSVValue(timing.cpuCurrent); else csvWriter.Write(',');
        if (timing.cpuCount > 0) WriteCSVValue(timing.cpuAverage); else csvWriter.Write(',');
        if (timing.gpuCount > 0) WriteCSVValue(timing.gpuCurrent); else csvWriter.Write(',');
        if (timing.gpuCount > 0) WriteCSVValue(timing.gpuAverage); else csvWriter.Write(',');
        WriteCSVValue(snapshot.gcAllocated); WriteCSVValue(snapshot.totalUsed); WriteCSVValue(snapshot.totalUsed - initialMemoryUsed);
        WriteCSVValue(snapshot.gcUsed); WriteCSVValue(snapshot.totalReserved); WriteCSVValue(snapshot.monoUsed);
        WriteCSVValue(snapshot.monoHeap); WriteCSVValue(snapshot.gfxUsed);
        WriteCSVValue(snapshot.dataRevision); WriteCSVValue(snapshot.instanceCount); WriteCSVValue(snapshot.speciesCount);
        WriteCSVValue(snapshot.renderGroupCount); WriteCSVValue(snapshot.chunkCount);
        WriteCSVValue(snapshot.rendererChunkRanges); WriteCSVValue(snapshot.visibleRanges);
        WriteCSVValue(snapshot.forwardCameras); WriteCSVValue(snapshot.cameraCount); WriteCSVValue(snapshot.shadowStates);
        WriteCSVValue(snapshot.forwardDispatches); WriteCSVValue(snapshot.forwardDraws);
        WriteCSVValue(snapshot.shadowDispatches); WriteCSVValue(snapshot.shadowDraws);
        WriteCSVValue(snapshot.activeColliders); WriteCSVValue(snapshot.pooledColliders);
        WriteCSVValue(snapshot.scannedInstances); WriteCSVValue(snapshot.scannedChunks);
        WriteCSVValue(QualitySettings.vSyncCount); WriteCSVValue(Application.targetFrameRate);
        WriteCSVValue(Screen.width); WriteCSVValue(Screen.height); WriteCSVValue(Screen.currentResolution.refreshRate);
        WriteCSVValue(QualitySettings.antiAliasing);
        csvWriter.WriteLine();
        csvWritesSinceFlush++;
        if (csvWritesSinceFlush >= 10) { csvWriter.Flush(); csvWritesSinceFlush = 0; }
    }

    private void EnsureCSV()
    {
        if (csvWriter != null || !writeCSV) return;
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            csvPath = Path.Combine(Application.persistentDataPath, "VegetationPerformance_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");
            csvWriter = new StreamWriter(csvPath, false, new UTF8Encoding(false), 16384);
            csvWriter.WriteLine("Time,FPSCurrent,FPSRollingAverage,FrameMSCurrent,FrameMSRollingAverage,CPUFrameMSCurrent,CPUFrameMSRollingAverage,GPUFrameMSCurrent,GPUFrameMSRollingAverage,GCAllocated,MemoryUsed,MemoryDelta,GCUsed,MemoryReserved,MonoUsed,MonoHeap,GfxUsed,DatabaseRevision,InstanceCount,SpeciesCount,RenderGroupCount,DatabaseChunkCount,RendererChunkRangeCount,VisibleChunkRanges,ForwardCameraCount,CameraCount,ShadowCameraStateCount,ForwardDispatchCount,ForwardDrawCount,ShadowDispatchCount,ShadowDrawCount,ActiveColliderCount,PooledColliderCount,ScannedInstanceCount,ScannedChunkCount,VSyncCount,TargetFrameRate,ScreenWidth,ScreenHeight,RefreshRate,MSAA");
        }
        catch (Exception exception)
        {
            Debug.LogWarning("VegetationPerformanceDiagnostic could not open CSV: " + exception.Message, this);
            writeCSV = false;
            CloseCSV();
        }
    }

    private void WriteCSVValue(float value) { csvWriter.Write(','); csvWriter.Write(value.ToString("F3", CultureInfo.InvariantCulture)); }
    private void WriteCSVValue(long value) { csvWriter.Write(','); csvWriter.Write(value); }

    private void CloseCSV()
    {
        if (csvWriter == null) return;
        csvWriter.Flush();
        csvWriter.Dispose();
        csvWriter = null;
        csvWritesSinceFlush = 0;
    }

    private void OnGUI()
    {
        if (!enableDiagnostics || !showRuntimeOverlay || string.IsNullOrEmpty(overlayText)) return;
        if (overlayStyle == null)
        {
            overlayStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.UpperLeft, richText = false };
            overlayStyle.normal.textColor = Color.white;
        }
        Rect rect = new Rect(10f, 10f, Mathf.Min(720f, Screen.width - 20f), overlayHeight);
        GUI.Box(rect, GUIContent.none);
        GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, rect.width - 20f, rect.height - 16f), overlayText, overlayStyle);
    }

    private void ResolveReferences()
    {
        if (vegetationRenderer == null)
        {
            // Prefer the renderer that owns this diagnostic component. ActiveRenderers can
            // contain editor/preview renderers before the scene renderer during OnEnable.
            vegetationRenderer = GetComponent<VegetationRenderer>();
            if (vegetationRenderer == null)
            {
                var renderers = VegetationRenderer.ActiveRenderers;
                if (renderers.Count > 0) vegetationRenderer = renderers[0];
            }
        }
        ResolveDatabaseReference();
        if (vegetationColliderManager == null) vegetationColliderManager = FindObjectOfType<VegetationColliderManager>();
    }

    private void ResolveDatabaseReference()
    {
        if (vegetationDatabase == null && vegetationRenderer != null) vegetationDatabase = vegetationRenderer.database;
        if (vegetationDatabase == null && vegetationColliderManager != null) vegetationDatabase = vegetationColliderManager.database;
    }

    private void StartRecorders()
    {
        gcAllocatedRecorder = StartRecorder("GC Allocated In Frame");
        gcUsedRecorder = StartRecorder("GC Used Memory");
        totalReservedRecorder = StartRecorder("Total Reserved Memory");
        totalUsedRecorder = StartRecorder("Total Used Memory");
        monoUsedRecorder = StartRecorder("Mono Used Size");
        monoHeapRecorder = StartRecorder("Mono Heap Size");
        gfxUsedRecorder = StartRecorder("Gfx Used Memory");
    }

    private static ProfilerRecorder StartRecorder(string marker)
    {
        try { return ProfilerRecorder.StartNew(ProfilerCategory.Memory, marker, 1); }
        catch { return default; }
    }

    private static long ReadRecorder(ProfilerRecorder recorder, long fallback)
    {
        return recorder.Valid && recorder.LastValue >= 0 ? recorder.LastValue : fallback;
    }

    private static void DisposeRecorder(ref ProfilerRecorder recorder)
    {
        if (recorder.Valid) recorder.Dispose();
        recorder = default;
    }

    private static float PercentageChange(float initial, float current)
    {
        return initial > 0f ? (current - initial) / initial * 100f : 0f;
    }

    private static int CountLines(string text)
    {
        int count = 1;
        for (int i = 0; i < text.Length; i++) if (text[i] == '\n') count++;
        return count;
    }
}
