using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum VegetationDiagnosticsMode
{
    Off,
    Normal,
    Verbose
}

internal struct VegetationDispatchStats
{
    public int selectedChunkCount;
    public int dispatchInstanceCount;
    public int dispatchCallCount;
    public int totalGroupX;
    public int maxGroupX;
    public long theoreticalThreadCount;
}

internal static class VegetationDiagnostics
{
    private enum CameraStage
    {
        None,
        Begin,
        Culling,
        CopyCounter,
        Draw
    }

    private static readonly HashSet<string> emittedErrors = new HashSet<string>();
    private static readonly HashSet<string> emittedNotices = new HashSet<string>();
    private static readonly Dictionary<int, Queue<long>> pendingAddInvocations = new Dictionary<int, Queue<long>>();
    private static readonly Dictionary<int, int> lastCameraFrame = new Dictionary<int, int>();
    private static readonly Dictionary<int, long> lastCameraSequence = new Dictionary<int, long>();
    private static readonly Dictionary<int, Vector2Int> lastCameraResolution = new Dictionary<int, Vector2Int>();
    private static readonly Dictionary<string, ResourceState> resourceStates = new Dictionary<string, ResourceState>();
    private static readonly List<string> resourceKeysToRelease = new List<string>();
    private static readonly HashSet<long> resizedPasses = new HashSet<long>();
    private static readonly Dictionary<long, PassWorkload> passWorkloads = new Dictionary<long, PassWorkload>();
    private static readonly Dictionary<long, ShadowWorkload> shadowWorkloads = new Dictionary<long, ShadowWorkload>();
    private static readonly Dictionary<int, ShadowWorkload> pendingShadowWorkloads = new Dictionary<int, ShadowWorkload>();
    private static readonly Dictionary<int, int> cameraRenderCountFrames = new Dictionary<int, int>();
    private static readonly Dictionary<int, int> cameraRenderCounts = new Dictionary<int, int>();
    private static readonly Dictionary<int, double> lastWorkloadSummaryTimes = new Dictionary<int, double>();
    private static readonly Dictionary<int, int> workloadSequencesSinceSummary = new Dictionary<int, int>();
    private const double MinimumWorkloadSummaryIntervalSeconds = 1.0;
    private const double VerboseLogBudgetWindowSeconds = 1.0;
    private const int VerboseLogBudgetPerWindow = 16;
    private static double verboseLogBudgetWindowStart;
    private static int verboseLogBudgetUsed;
    private static long nextAddInvocationID;
    private static long nextRenderSequenceID;
    private static long activePassID;
    private static long activeAddInvocationID;
    private static long nextShadowInvocationID;
    private static int activeFrame = -1;
    private static int activeCameraID;
    private static string activeCameraName;
    private static CameraType activeCameraType;
    private static CameraStage activeStage;

    private sealed class ResourceState
    {
        public long passID;
        public string stage;
        public string species;
    }

    private sealed class PassWorkload
    {
        public int frame;
        public long passID;
        public string cameraName;
        public int cameraID;
        public CameraType cameraType;
        public int width;
        public int height;
        public int renderCount;
        public int sequencesSinceSummary;
        public int speciesCount;
        public int dispatchCount;
        public int indirectDrawCount;
        public int forwardSourceInstances;
        public int shadowSpeciesCount;
        public int shadowDispatchCount;
        public int shadowIndirectDrawCount;
        public int shadowSourceInstances;
        public int pendingReadbacks;
        public long visibleForwardInstances;
        public bool ended;
        public bool emitSummary;
    }

    private sealed class ShadowWorkload
    {
        public int speciesCount;
        public int dispatchCount;
        public int indirectDrawCount;
        public int sourceInstances;

        public void Add(ShadowWorkload other)
        {
            if (other == null) return;
            speciesCount += other.speciesCount;
            dispatchCount += other.dispatchCount;
            indirectDrawCount += other.indirectDrawCount;
            sourceInstances += other.sourceInstances;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        emittedErrors.Clear();
        emittedNotices.Clear();
        pendingAddInvocations.Clear();
        lastCameraFrame.Clear();
        lastCameraSequence.Clear();
        lastCameraResolution.Clear();
        resourceStates.Clear();
        resourceKeysToRelease.Clear();
        resizedPasses.Clear();
        passWorkloads.Clear();
        shadowWorkloads.Clear();
        pendingShadowWorkloads.Clear();
        cameraRenderCountFrames.Clear();
        cameraRenderCounts.Clear();
        lastWorkloadSummaryTimes.Clear();
        workloadSequencesSinceSummary.Clear();
        nextAddInvocationID = 0;
        nextRenderSequenceID = 0;
        activePassID = 0;
        activeAddInvocationID = 0;
        nextShadowInvocationID = 0;
        activeFrame = -1;
        activeCameraID = 0;
        activeCameraName = null;
        activeCameraType = CameraType.Game;
        activeStage = CameraStage.None;
        verboseLogBudgetWindowStart = 0.0;
        verboseLogBudgetUsed = 0;
    }

    public static bool ShouldReadback(int interval)
    {
        return Time.frameCount % Mathf.Max(1, interval) == 0;
    }

    public static bool ShouldReadback(int interval, long passID)
    {
        return resizedPasses.Contains(passID) || passID % Mathf.Max(1, interval) == 0;
    }

    public static bool ShouldWriteVerbose(VegetationDiagnosticsMode mode, int interval)
    {
        return mode == VegetationDiagnosticsMode.Verbose && ShouldReadback(interval) && TryTakeVerboseLogSlot();
    }

    public static void AddRenderPasses(
        Camera camera,
        CameraData cameraData,
        int featureInstanceID,
        string featureName,
        bool enqueued,
        string skipReason,
        VegetationDiagnosticsMode mode,
        int verboseInterval)
    {
        if (mode == VegetationDiagnosticsMode.Off) return;
        if (camera == null) return;

        long addID = ++nextAddInvocationID;
        if (enqueued)
        {
            int cameraID = camera.GetInstanceID();
            if (!pendingAddInvocations.TryGetValue(cameraID, out Queue<long> queue))
            {
                queue = new Queue<long>();
                pendingAddInvocations.Add(cameraID, queue);
            }
            queue.Enqueue(addID);
        }

        if (!ShouldWriteVerbose(mode, verboseInterval)) return;
        Debug.Log($"[VegetationDiag][AddRenderPasses] AddID={addID}, Feature={featureName}, FeatureID={featureInstanceID}, " +
                  $"Enqueued={enqueued}, SkipReason={skipReason ?? "None"}, {CameraText(camera)}, {CameraDataText(camera, cameraData)}");
    }

    public static long BeginExecute(
        Camera camera,
        CameraData cameraData,
        VegetationDiagnosticsMode mode,
        int verboseInterval)
    {
        if (mode == VegetationDiagnosticsMode.Off) return 0;
        if (camera == null) return 0;

        int frame = Time.frameCount;
        int cameraID = camera.GetInstanceID();
        long passID = ++nextRenderSequenceID;
        long addID = ConsumeAddInvocation(cameraID);

        if (activePassID != 0)
        {
            CriticalOnce(
                $"camera-interleave-{activePassID}-{passID}",
                $"Camera pass interleaving detected. Previous=[PassID={activePassID}, Frame={activeFrame}, " +
                $"Camera={activeCameraName}, ID={activeCameraID}, Type={activeCameraType}, Stage={activeStage}] " +
                $"Next=[PassID={passID}, AddID={addID}, Frame={frame}, Camera={camera.name}, ID={cameraID}, Type={camera.cameraType}]"
            );
        }

        if (mode == VegetationDiagnosticsMode.Verbose &&
            lastCameraFrame.TryGetValue(cameraID, out int previousFrame) && previousFrame == frame)
        {
            long previousSequence = lastCameraSequence[cameraID];
            if (emittedNotices.Add($"full-camera-repeat-{cameraID}"))
            {
                Debug.LogWarning(
                    $"[VegetationDiag][Notice] Full camera render repeated within one Time.frameCount " +
                    $"(recorded, not classified as an error). Frame={frame}, PreviousRenderSequence={previousSequence}, " +
                    $"RenderSequence={passID}, AddID={addID}, Camera={camera.name}, CameraID={cameraID}, " +
                    $"CameraType={camera.cameraType}, {CameraDataText(camera, cameraData)}"
                );
            }
        }

        lastCameraFrame[cameraID] = frame;
        lastCameraSequence[cameraID] = passID;
        CheckResolutionChange(camera, passID, mode);

        int renderCount = 1;
        if (cameraRenderCountFrames.TryGetValue(cameraID, out int countFrame) && countFrame == frame)
            renderCount = cameraRenderCounts[cameraID] + 1;
        cameraRenderCountFrames[cameraID] = frame;
        cameraRenderCounts[cameraID] = renderCount;

        int sequencesSinceSummary = workloadSequencesSinceSummary.TryGetValue(cameraID, out int suppressed)
            ? suppressed + 1
            : 1;
        bool emitSummary = mode == VegetationDiagnosticsMode.Verbose &&
                           ShouldEmitWorkloadSummary(cameraID, passID, verboseInterval);
        workloadSequencesSinceSummary[cameraID] = emitSummary ? 0 : sequencesSinceSummary;

        PassWorkload workload = new PassWorkload
        {
            frame = frame,
            passID = passID,
            cameraName = camera.name,
            cameraID = cameraID,
            cameraType = camera.cameraType,
            width = camera.pixelWidth,
            height = camera.pixelHeight,
            renderCount = renderCount,
            sequencesSinceSummary = sequencesSinceSummary,
            emitSummary = emitSummary
        };

        if (pendingShadowWorkloads.TryGetValue(cameraID, out ShadowWorkload shadow))
        {
            workload.shadowSpeciesCount = shadow.speciesCount;
            workload.shadowDispatchCount = shadow.dispatchCount;
            workload.shadowIndirectDrawCount = shadow.indirectDrawCount;
            workload.shadowSourceInstances = shadow.sourceInstances;
            pendingShadowWorkloads.Remove(cameraID);
        }
        passWorkloads[passID] = workload;

        activePassID = passID;
        activeAddInvocationID = addID;
        activeFrame = frame;
        activeCameraID = cameraID;
        activeCameraName = camera.name;
        activeCameraType = camera.cameraType;
        activeStage = CameraStage.Begin;

        if (ShouldWriteVerbose(mode, verboseInterval))
        {
            VerboseCameraStage(camera, passID, mode, verboseInterval,
                $"Pass.Execute Begin AddID={addID} {CameraDataText(camera, cameraData)}");
        }
        return passID;
    }

    public static void Culling(Camera camera, long passID, VegetationDiagnosticsMode mode, int verboseInterval)
    {
        if (mode == VegetationDiagnosticsMode.Off || passID == 0) return;
        Transition(camera, passID, CameraStage.Culling, mode, verboseInterval, "Culling");
    }

    public static void CopyCounter(Camera camera, long passID, VegetationDiagnosticsMode mode, int verboseInterval)
    {
        if (mode == VegetationDiagnosticsMode.Off || passID == 0) return;
        Transition(camera, passID, CameraStage.CopyCounter, mode, verboseInterval, "CopyCounter");
    }

    public static void Draw(Camera camera, long passID, VegetationDiagnosticsMode mode, int verboseInterval)
    {
        if (mode == VegetationDiagnosticsMode.Off || passID == 0) return;
        Transition(camera, passID, CameraStage.Draw, mode, verboseInterval, "Draw");
    }

    public static void EndCamera(Camera camera, long passID, VegetationDiagnosticsMode mode, int verboseInterval)
    {
        if (mode == VegetationDiagnosticsMode.Off || passID == 0) return;
        if (!MatchesActiveCamera(camera, passID))
        {
            ReportCameraMismatch(camera, passID, "End Vegetation Camera");
            return;
        }

        VerboseCameraStage(camera, passID, mode, verboseInterval, "Pass.Execute End");
        EndWorkload(passID);
        ReleasePassResources(passID);
        resizedPasses.Remove(passID);
        activePassID = 0;
        activeAddInvocationID = 0;
        activeFrame = -1;
        activeCameraID = 0;
        activeCameraName = null;
        activeStage = CameraStage.None;
    }

    private static void Transition(Camera camera, long passID, CameraStage next, VegetationDiagnosticsMode mode, int verboseInterval, string label)
    {
        if (!MatchesActiveCamera(camera, passID))
        {
            ReportCameraMismatch(camera, passID, label);
            return;
        }

        bool valid = next == CameraStage.Culling
            ? activeStage == CameraStage.Begin || activeStage == CameraStage.Draw
            : next == CameraStage.CopyCounter
                ? activeStage == CameraStage.Culling
                : activeStage == CameraStage.CopyCounter;

        if (!valid)
        {
            ErrorOnce(
                $"camera-stage-{activeCameraID}-{activeStage}-{next}",
                $"Camera order anomaly: invalid stage transition {activeStage} -> {next}. " +
                $"PassID={passID}, {CameraText(camera)}"
            );
        }

        activeStage = next;
        VerboseCameraStage(camera, passID, mode, verboseInterval, label);
    }

    private static bool MatchesActiveCamera(Camera camera, long passID)
    {
        return camera != null && activePassID == passID && activeCameraID == camera.GetInstanceID();
    }

    private static void ReportCameraMismatch(Camera camera, long passID, string requestedStage)
    {
        string current = camera == null ? "Camera=<null>" : CameraText(camera);
        ErrorOnce(
            $"camera-mismatch-{activePassID}-{passID}-{requestedStage}",
            $"Camera order anomaly at {requestedStage}. Active=[PassID={activePassID}, AddID={activeAddInvocationID}, " +
            $"Frame={activeFrame}, Camera={activeCameraName}, ID={activeCameraID}, Type={activeCameraType}, Stage={activeStage}] " +
            $"Requested=[PassID={passID}, {current}]"
        );
    }

    private static void VerboseCameraStage(Camera camera, long passID, VegetationDiagnosticsMode mode, int interval, string stage)
    {
        if (!ShouldWriteVerbose(mode, interval)) return;
        Debug.Log($"[VegetationDiag][Camera] PassID={passID}, {stage} | {CameraText(camera)}");
    }

    public static void ForwardPrepareStarted(
        Camera camera,
        string species,
        int totalInstanceCount,
        long passID,
        long lastPreparePassID)
    {
        if (passID == 0) return;
        if (lastPreparePassID != passID) return;

        CriticalOnce(
            $"duplicate-forward-pass-{passID}-{species}",
            $"Category B: the same Species was prepared twice inside one Camera Pass. PassID={passID}, " +
            $"{CameraText(camera)}, Species={species}, totalInstanceCount={totalInstanceCount}"
        );
    }

    public static void VerifyResetBeforeDispatch(
        Camera camera,
        string species,
        long passID,
        string bufferNames,
        long resetPassID)
    {
        if (passID == 0) return;
        if (resetPassID == passID) return;

        CriticalOnce(
            $"missing-reset-{passID}-{species}",
            $"Dispatch occurred before SetCounterValue(0) was recorded. PassID={passID}, {CameraText(camera)}, " +
            $"Species={species}, Buffers={bufferNames}, ResetPassID={resetPassID}"
        );
    }

    public static void VerifyShadowResetBeforeDispatch(
        Camera camera,
        string species,
        long shadowInvocationID,
        string bufferNames,
        long resetShadowInvocationID)
    {
        if (shadowInvocationID == 0) return;
        if (resetShadowInvocationID == shadowInvocationID) return;

        CriticalOnce(
            $"missing-shadow-reset-{shadowInvocationID}-{species}",
            $"Shadow Dispatch occurred before SetCounterValue(0) was recorded. ShadowInvocation={shadowInvocationID}, " +
            $"{CameraText(camera)}, Species={species}, Buffers={bufferNames}, " +
            $"ResetShadowInvocation={resetShadowInvocationID}"
        );
    }

    public static void GpuCommand(
        long passID,
        Camera camera,
        string resourceKey,
        string species,
        string stage,
        VegetationDiagnosticsMode mode,
        int verboseInterval)
    {
        if (mode == VegetationDiagnosticsMode.Off || passID == 0) return;
        if (string.IsNullOrEmpty(resourceKey)) resourceKey = "<unknown>";
        bool isReset = stage.StartsWith("Reset", StringComparison.Ordinal);

        if (!resourceStates.TryGetValue(resourceKey, out ResourceState state))
        {
            state = new ResourceState();
            resourceStates.Add(resourceKey, state);
        }

        if (isReset)
        {
            if (state.passID != 0 && state.passID != passID)
            {
                CriticalOnce(
                    $"resource-cross-reset-{resourceKey}-{state.passID}-{passID}",
                    $"Category D: another Camera Pass reset a Forward resource before its previous Pass ended. " +
                    $"PreviousPassID={state.passID}, PreviousStage={state.stage}, CurrentPassID={passID}, " +
                    $"Resource={resourceKey}, Species={species}, {CameraText(camera)}"
                );
            }

            state.passID = passID;
            state.stage = stage;
            state.species = species;
        }
        else
        {
            if (state.passID != passID)
            {
                CriticalOnce(
                    $"resource-cross-stage-{resourceKey}-{state.passID}-{passID}-{stage}",
                    $"Forward resource command belongs to a different active Pass. ExpectedPassID={state.passID}, " +
                    $"ActualPassID={passID}, Stage={stage}, Resource={resourceKey}, Species={species}, {CameraText(camera)}"
                );
            }

            bool isDispatch = stage.StartsWith("Dispatch", StringComparison.Ordinal);
            bool isCopy = stage.StartsWith("Copy", StringComparison.Ordinal);
            bool isDraw = stage.StartsWith("Draw", StringComparison.Ordinal);
            bool validTransition = isDispatch
                ? state.stage != null && (state.stage.StartsWith("Reset", StringComparison.Ordinal) || state.stage.StartsWith("Dispatch", StringComparison.Ordinal))
                : isCopy
                    ? state.stage != null && (state.stage.StartsWith("Reset", StringComparison.Ordinal) || state.stage.StartsWith("Dispatch", StringComparison.Ordinal))
                    : isDraw && state.stage != null && state.stage.StartsWith("Copy", StringComparison.Ordinal);

            if (!validTransition)
            {
                CriticalOnce(
                    $"resource-stage-{resourceKey}-{passID}-{state.stage}-{stage}",
                    $"Invalid Forward GPU command order. PassID={passID}, Resource={resourceKey}, Species={species}, " +
                    $"PreviousStage={state.stage ?? "None"}, CurrentStage={stage}, {CameraText(camera)}"
                );
            }

            state.passID = passID;
            state.stage = stage;
            state.species = species;
        }

        if (ShouldWriteVerbose(mode, verboseInterval))
        {
            Debug.Log($"[VegetationDiag][GPUCommand] PassID={passID}, Stage={stage}, Resource={resourceKey}, " +
                      $"Species={species}, {CameraText(camera)}");
        }
    }

    public static void ValidateDisableCullingInput(
        long passID,
        Camera camera,
        string species,
        int sourceInstanceCount,
        int matrixCapacity,
        int visibleCapacity,
        int knownSafeInstanceCount)
    {
        if (passID == 0) return;
        if (sourceInstanceCount <= matrixCapacity && sourceInstanceCount <= visibleCapacity &&
            knownSafeInstanceCount == sourceInstanceCount) return;

        CriticalOnce(
            $"disable-culling-capacity-{camera.GetInstanceID()}-{species}",
            $"DisableGPUCulling input required clamping. PassID={passID}, {CameraText(camera)}, Species={species}, " +
            $"SourceInstanceCount={sourceInstanceCount}, MatrixCapacity={matrixCapacity}, " +
            $"VisibleBufferCapacity={visibleCapacity}, KnownSafeInstanceCount={knownSafeInstanceCount}"
        );
    }

    public static void ValidateGroupDispatchTotals(
        long passID,
        Camera camera,
        string species,
        int sourceInstanceCount,
        bool chunkCulling,
        VegetationDispatchStats dispatch)
    {
        if (passID == 0) return;
        bool exceedsSource = dispatch.dispatchInstanceCount > sourceInstanceCount;
        bool unexpectedMultipleDispatch = !chunkCulling && dispatch.dispatchCallCount > 1;
        if (!exceedsSource && !unexpectedMultipleDispatch) return;

        CriticalOnce(
            $"duplicate-dispatch-pass-{passID}-{species}",
            $"Category C: suspicious repeated Dispatch inside one Camera Pass. PassID={passID}, {CameraText(camera)}, " +
            $"Species={species}, SourceInstanceCount={sourceInstanceCount}, DispatchInstanceCount={dispatch.dispatchInstanceCount}, " +
            $"DispatchCalls={dispatch.dispatchCallCount}, CPUSelectedChunks={dispatch.selectedChunkCount}, " +
            $"ChunkCulling={chunkCulling}"
        );
    }

    public static long BeginShadow(Camera camera, VegetationDiagnosticsMode mode)
    {
        if (mode == VegetationDiagnosticsMode.Off) return 0;
        long invocationID = ++nextShadowInvocationID;
        shadowWorkloads[invocationID] = new ShadowWorkload();
        if (mode == VegetationDiagnosticsMode.Verbose && camera != null &&
            emittedNotices.Add($"shadow-callback-{camera.GetInstanceID()}"))
        {
            Debug.LogWarning($"[VegetationDiag][Notice] Shadow callback active. ShadowInvocation={invocationID}, " +
                             $"{CameraText(camera)}");
        }
        return invocationID;
    }

    public static void RecordShadowPrepare(long invocationID, int sourceInstances, int dispatchCount)
    {
        if (invocationID == 0) return;
        if (!shadowWorkloads.TryGetValue(invocationID, out ShadowWorkload workload)) return;
        workload.speciesCount++;
        workload.sourceInstances += Mathf.Max(0, sourceInstances);
        workload.dispatchCount += Mathf.Max(0, dispatchCount);
    }

    public static void RecordShadowDraw(long invocationID, int indirectDrawCount)
    {
        if (invocationID == 0) return;
        if (!shadowWorkloads.TryGetValue(invocationID, out ShadowWorkload workload)) return;
        workload.indirectDrawCount += Mathf.Max(0, indirectDrawCount);
    }

    public static void EndShadow(long invocationID, Camera camera)
    {
        if (invocationID == 0) return;
        if (!shadowWorkloads.TryGetValue(invocationID, out ShadowWorkload workload)) return;
        shadowWorkloads.Remove(invocationID);
        if (camera == null) return;

        int cameraID = camera.GetInstanceID();
        if (!pendingShadowWorkloads.TryGetValue(cameraID, out ShadowWorkload pending))
        {
            pending = new ShadowWorkload();
            pendingShadowWorkloads.Add(cameraID, pending);
        }
        pending.Add(workload);
    }

    public static void RecordForwardPrepare(long passID, int sourceInstances, int dispatchCount)
    {
        if (passID == 0) return;
        if (!passWorkloads.TryGetValue(passID, out PassWorkload workload)) return;
        workload.speciesCount++;
        workload.forwardSourceInstances += Mathf.Max(0, sourceInstances);
        workload.dispatchCount += Mathf.Max(0, dispatchCount);
    }

    public static void RecordForwardDraw(long passID, int indirectDrawCount)
    {
        if (passID == 0) return;
        if (!passWorkloads.TryGetValue(passID, out PassWorkload workload)) return;
        workload.indirectDrawCount += Mathf.Max(0, indirectDrawCount);
    }

    private static void RegisterForwardReadback(long passID)
    {
        if (passWorkloads.TryGetValue(passID, out PassWorkload workload)) workload.pendingReadbacks++;
    }

    private static void CompleteForwardReadback(long passID, long visibleInstances)
    {
        if (!passWorkloads.TryGetValue(passID, out PassWorkload workload)) return;
        workload.visibleForwardInstances += Math.Max(0L, visibleInstances);
        workload.pendingReadbacks = Mathf.Max(0, workload.pendingReadbacks - 1);

        if (!workload.ended || workload.pendingReadbacks != 0) return;
        if (workload.emitSummary)
        {
            Debug.Log($"[VegetationDiag][WorkloadGPU] Frame={workload.frame}, RenderSequence={workload.passID}, " +
                      $"Camera={workload.cameraName}, CameraID={workload.cameraID}, CameraType={workload.cameraType}, " +
                      $"Resolution={workload.width}x{workload.height}, VisibleForwardInstances={workload.visibleForwardInstances}, " +
                      $"ShadowSourceInstances={workload.shadowSourceInstances}");
        }
        passWorkloads.Remove(passID);
    }

    private static void EndWorkload(long passID)
    {
        if (!passWorkloads.TryGetValue(passID, out PassWorkload workload)) return;
        workload.ended = true;

        if (workload.emitSummary)
        {
            Debug.Log($"[VegetationDiag][Workload] Frame={workload.frame}, RenderSequence={workload.passID}, " +
                      $"Camera={workload.cameraName}, CameraID={workload.cameraID}, CameraType={workload.cameraType}, " +
                      $"Resolution={workload.width}x{workload.height}, CameraRenderCountThisFrame={workload.renderCount}, " +
                      $"RenderSequencesSinceLastSummary={workload.sequencesSinceSummary}, " +
                      $"SpeciesCount={workload.speciesCount}, DispatchCount={workload.dispatchCount}, " +
                      $"IndirectDrawCount={workload.indirectDrawCount}, ShadowSpeciesCount={workload.shadowSpeciesCount}, " +
                      $"ShadowDispatchCount={workload.shadowDispatchCount}, ShadowIndirectDrawCount={workload.shadowIndirectDrawCount}, " +
                      $"ForwardSourceInstanceCount={workload.forwardSourceInstances}, " +
                      $"ShadowSourceInstanceCount={workload.shadowSourceInstances}, PendingGPUReadbacks={workload.pendingReadbacks}");
        }

        if (workload.pendingReadbacks == 0) passWorkloads.Remove(passID);
    }

    private static long ConsumeAddInvocation(int cameraID)
    {
        if (!pendingAddInvocations.TryGetValue(cameraID, out Queue<long> queue) || queue.Count == 0) return 0;
        long addID = queue.Dequeue();
        if (queue.Count == 0) pendingAddInvocations.Remove(cameraID);
        return addID;
    }

    private static void CheckResolutionChange(Camera camera, long passID, VegetationDiagnosticsMode mode)
    {
        int cameraID = camera.GetInstanceID();
        Vector2Int current = new Vector2Int(camera.pixelWidth, camera.pixelHeight);

        if (mode == VegetationDiagnosticsMode.Verbose &&
            lastCameraResolution.TryGetValue(cameraID, out Vector2Int previous) && previous != current)
        {
            resizedPasses.Add(passID);
            Matrix4x4 projection = camera.projectionMatrix;
            Debug.Log($"[VegetationDiag][Resize] Frame={Time.frameCount}, RenderSequence={passID}, Camera={camera.name}, " +
                      $"CameraID={cameraID}, CameraType={camera.cameraType}, OldResolution={previous.x}x{previous.y}, " +
                      $"NewResolution={current.x}x{current.y}, Aspect={camera.aspect}, FOV={camera.fieldOfView}, " +
                      $"Near={camera.nearClipPlane}, Far={camera.farClipPlane}, Orthographic={camera.orthographic}, " +
                      $"OrthographicSize={camera.orthographicSize}, Projection=[{projection.m00},{projection.m01},{projection.m02},{projection.m03};" +
                      $"{projection.m10},{projection.m11},{projection.m12},{projection.m13};" +
                      $"{projection.m20},{projection.m21},{projection.m22},{projection.m23};" +
                      $"{projection.m30},{projection.m31},{projection.m32},{projection.m33}]");
        }

        lastCameraResolution[cameraID] = current;
    }

    private static bool ShouldEmitWorkloadSummary(int cameraID, long passID, int verboseInterval)
    {
        if (resizedPasses.Contains(passID)) return true;
        if (passID % Mathf.Max(1, verboseInterval) != 0) return false;

        double now = DiagnosticTime;
        if (lastWorkloadSummaryTimes.TryGetValue(cameraID, out double last) &&
            now - last < MinimumWorkloadSummaryIntervalSeconds)
            return false;

        lastWorkloadSummaryTimes[cameraID] = now;
        return true;
    }

    private static bool TryTakeVerboseLogSlot()
    {
        double now = DiagnosticTime;
        if (now - verboseLogBudgetWindowStart >= VerboseLogBudgetWindowSeconds)
        {
            verboseLogBudgetWindowStart = now;
            verboseLogBudgetUsed = 0;
        }

        if (verboseLogBudgetUsed >= VerboseLogBudgetPerWindow) return false;
        verboseLogBudgetUsed++;
        return true;
    }

    private static double DiagnosticTime
    {
        get
        {
#if UNITY_EDITOR
            return EditorApplication.timeSinceStartup;
#else
            return Time.realtimeSinceStartupAsDouble;
#endif
        }
    }

    private static string CameraDataText(Camera camera, CameraData cameraData)
    {
        UniversalAdditionalCameraData additional = camera != null
            ? camera.GetComponent<UniversalAdditionalCameraData>()
            : null;
        string renderType = additional != null ? additional.renderType.ToString() : cameraData.renderType.ToString();
        int rendererIndex = GetRendererIndex(additional);
        ScriptableRenderer renderer = cameraData.renderer;
        string rendererIdentity = renderer == null
            ? "<null>"
            : $"{renderer.GetType().Name}#{RuntimeHelpers.GetHashCode(renderer)}";
        string stack = GetCameraStackText(additional);
        string target = camera != null && camera.targetTexture != null
            ? $"{camera.targetTexture.name}#{camera.targetTexture.GetInstanceID()}"
            : "Backbuffer";

        return $"URPRenderType={renderType}, Renderer={rendererIdentity}, RendererIndex={rendererIndex}, " +
               $"CameraStack={stack}, Resolution={camera.pixelWidth}x{camera.pixelHeight}, TargetTexture={target}, " +
               $"AllowHDR={camera.allowHDR}, AllowMSAA={camera.allowMSAA}, ActualMSAA={cameraData.cameraTargetDescriptor.msaaSamples}, " +
               $"IsPlaying={Application.isPlaying}";
    }

    private static string GetCameraStackText(UniversalAdditionalCameraData additional)
    {
        if (additional == null) return "NoAdditionalData";
        if (additional.renderType != CameraRenderType.Base) return "Overlay";

        List<Camera> stack = additional.cameraStack;
        if (stack == null || stack.Count == 0) return "Base[0]";

        StringBuilder builder = new StringBuilder("Base[");
        builder.Append(stack.Count).Append(':');
        for (int i = 0; i < stack.Count; i++)
        {
            if (i > 0) builder.Append('|');
            Camera item = stack[i];
            builder.Append(item != null ? $"{item.name}#{item.GetInstanceID()}" : "null");
        }
        return builder.Append(']').ToString();
    }

    private static int GetRendererIndex(UniversalAdditionalCameraData additional)
    {
        if (additional == null) return -1;
#if UNITY_EDITOR
        SerializedObject serialized = new SerializedObject(additional);
        SerializedProperty property = serialized.FindProperty("m_RendererIndex");
        return property != null ? property.intValue : -1;
#else
        return -1;
#endif
    }

    private static void ReleasePassResources(long passID)
    {
        resourceKeysToRelease.Clear();
        foreach (KeyValuePair<string, ResourceState> pair in resourceStates)
        {
            if (pair.Value.passID == passID) resourceKeysToRelease.Add(pair.Key);
        }

        for (int i = 0; i < resourceKeysToRelease.Count; i++)
            resourceStates.Remove(resourceKeysToRelease[i]);
        resourceKeysToRelease.Clear();
    }

    public static void ValidateDispatch(
        Camera camera,
        string species,
        string phase,
        string kernel,
        int workCount,
        int groupX,
        int groupY,
        int groupZ,
        uint threadX,
        uint threadY,
        uint threadZ,
        int totalInstanceCount,
        int safeLimit,
        long passID = 0)
    {
        if (passID == 0) return;
        long threadCount = (long)groupX * groupY * groupZ * threadX * threadY * threadZ;
        bool undersized = threadCount < workCount;
        bool oversized = threadCount > Math.Max((long)safeLimit * 2L, 1024L) || workCount > safeLimit;
        bool invalid = groupX <= 0 || groupY <= 0 || groupZ <= 0 || workCount < 0 || workCount > totalInstanceCount;

        if (!undersized && !oversized && !invalid) return;

        ErrorOnce(
            $"dispatch-{camera.GetInstanceID()}-{species}-{phase}-{kernel}",
            $"Unsafe compute dispatch. PassID={passID}, {CameraText(camera)}, Phase={phase}, Species={species}, Kernel={kernel}, " +
            $"numthreads=({threadX},{threadY},{threadZ}), DispatchGroups=({groupX},{groupY},{groupZ}), " +
            $"TheoreticalThreads={threadCount}, WorkCount={workCount}, totalInstanceCount={totalInstanceCount}, SafeLimit={safeLimit}"
        );
    }

    public static void KernelMetadata(
        Camera camera,
        VegetationDiagnosticsMode mode,
        int verboseInterval,
        string kernel,
        uint threadX,
        uint threadY,
        uint threadZ,
        bool hasBoundsGuard)
    {
        if (mode == VegetationDiagnosticsMode.Off) return;
        if (!hasBoundsGuard)
        {
            ErrorOnce(
                $"kernel-bounds-{kernel}",
                $"Compute kernel has no verified SV_DispatchThreadID bounds guard. Kernel={kernel}"
            );
        }

        if (!ShouldWriteVerbose(mode, verboseInterval)) return;
        Debug.Log($"[VegetationDiag][Kernel] {CameraText(camera)}, Kernel={kernel}, " +
                  $"numthreads=({threadX},{threadY},{threadZ}), BoundsGuardBeforeBufferRead={hasBoundsGuard}");
    }

    public static GroupReadback BeginGroupReadback(
        Camera camera,
        VegetationDiagnosticsMode mode,
        int verboseInterval,
        string species,
        int totalInstanceCount,
        int totalChunkCount,
        int resetFrame,
        VegetationDispatchStats dispatch,
        string kernel,
        uint threadX,
        uint threadY,
        uint threadZ,
        int safeLimit,
        bool gpuCulling,
        bool gpuLODEnabled,
        long passID,
        int lod0Capacity,
        int lod1Capacity,
        int lod2Capacity,
        int lod3Capacity)
    {
        RegisterForwardReadback(passID);
        return new GroupReadback(
            camera,
            mode,
            verboseInterval,
            species,
            totalInstanceCount,
            totalChunkCount,
            resetFrame,
            dispatch,
            kernel,
            threadX,
            threadY,
            threadZ,
            safeLimit,
            gpuCulling,
            gpuLODEnabled,
            passID,
            lod0Capacity,
            lod1Capacity,
            lod2Capacity,
            lod3Capacity,
            resizedPasses.Contains(passID) ||
            (passWorkloads.TryGetValue(passID, out PassWorkload workload) && workload.emitSummary)
        );
    }

    public static string CameraText(Camera camera)
    {
        if (camera == null) return $"Frame={Time.frameCount}, Camera=<null>";
        return $"Frame={Time.frameCount}, Camera={camera.name}, CameraID={camera.GetInstanceID()}, CameraType={camera.cameraType}";
    }

    public static void ErrorOnce(string key, string message)
    {
        if (!emittedErrors.Add(key)) return;
        Debug.LogError($"[VegetationDiag][Error] {message}");
    }

    public static void CriticalOnce(string key, string message)
    {
        if (!emittedErrors.Add($"critical-{key}")) return;
        Debug.LogError($"[VegetationDiag][CRITICAL] {message}");
    }

    internal sealed class GroupReadback
    {
        private struct ArgsResult
        {
            public int lod;
            public string mesh;
            public int part;
            public uint a0;
            public uint a1;
            public uint a2;
            public uint a3;
            public uint a4;
            public uint expectedA0;
            public uint expectedA2;
            public uint expectedA3;
            public uint expectedA4;
        }

        private readonly int frame;
        private readonly string cameraName;
        private readonly int cameraID;
        private readonly CameraType cameraType;
        private readonly VegetationDiagnosticsMode mode;
        private readonly int verboseInterval;
        private readonly string species;
        private readonly int totalInstanceCount;
        private readonly int totalChunkCount;
        private readonly int resetFrame;
        private readonly VegetationDispatchStats dispatch;
        private readonly string kernel;
        private readonly uint threadX;
        private readonly uint threadY;
        private readonly uint threadZ;
        private readonly int safeLimit;
        private readonly bool gpuCulling;
        private readonly bool gpuLODEnabled;
        private readonly long passID;
        private readonly bool forceSummary;
        private readonly long[] visibleCounters = { -1, -1, -1, -1 };
        private readonly int[] visibleCapacities = new int[4];
        private readonly List<ArgsResult> argsResults;
        private int pending;
        private bool sealedBatch;

        public GroupReadback(
            Camera camera,
            VegetationDiagnosticsMode mode,
            int verboseInterval,
            string species,
            int totalInstanceCount,
            int totalChunkCount,
            int resetFrame,
            VegetationDispatchStats dispatch,
            string kernel,
            uint threadX,
            uint threadY,
            uint threadZ,
            int safeLimit,
            bool gpuCulling,
            bool gpuLODEnabled,
            long passID,
            int lod0Capacity,
            int lod1Capacity,
            int lod2Capacity,
            int lod3Capacity,
            bool forceSummary)
        {
            frame = Time.frameCount;
            cameraName = camera != null ? camera.name : "<null>";
            cameraID = camera != null ? camera.GetInstanceID() : 0;
            cameraType = camera != null ? camera.cameraType : CameraType.Game;
            this.mode = mode;
            this.verboseInterval = Mathf.Max(1, verboseInterval);
            this.species = species;
            this.totalInstanceCount = totalInstanceCount;
            this.totalChunkCount = totalChunkCount;
            this.resetFrame = resetFrame;
            this.dispatch = dispatch;
            this.kernel = kernel;
            this.threadX = threadX;
            this.threadY = threadY;
            this.threadZ = threadZ;
            this.safeLimit = safeLimit;
            this.gpuCulling = gpuCulling;
            this.gpuLODEnabled = gpuLODEnabled;
            this.passID = passID;
            this.forceSummary = forceSummary;
            visibleCapacities[0] = lod0Capacity;
            visibleCapacities[1] = lod1Capacity;
            visibleCapacities[2] = lod2Capacity;
            visibleCapacities[3] = lod3Capacity;
            argsResults = new List<ArgsResult>();
        }

        public void RequestCounter(CommandBuffer cmd, ComputeBuffer buffer, int lod)
        {
            if (cmd == null || buffer == null) return;
            pending++;
            cmd.RequestAsyncReadback(buffer, sizeof(uint), 0, request => CompleteCounter(request, lod));
        }

        public void SetCounterValue(int lod, long value)
        {
            if (lod < 0 || lod >= visibleCounters.Length) return;
            visibleCounters[lod] = value;
        }

        public void RequestArgs(
            CommandBuffer cmd,
            ComputeBuffer buffer,
            int lod,
            string mesh,
            int part,
            uint expectedIndexCount,
            uint expectedIndexStart,
            uint expectedBaseVertex,
            uint expectedStartInstance)
        {
            if (cmd == null || buffer == null) return;
            pending++;
            cmd.RequestAsyncReadback(buffer, sizeof(uint) * 5, 0, request => CompleteArgs(
                request,
                lod,
                mesh,
                part,
                expectedIndexCount,
                expectedIndexStart,
                expectedBaseVertex,
                expectedStartInstance
            ));
        }

        public void Seal()
        {
            sealedBatch = true;
            TryFinish();
        }

        private void CompleteCounter(AsyncGPUReadbackRequest request, int lod)
        {
            if (request.hasError || request.GetData<uint>().Length < 1)
            {
                ErrorOnce($"counter-readback-{cameraID}-{species}-{lod}",
                    $"Append counter readback failed. {ContextText()}, Species={species}, LOD={lod}");
            }
            else
            {
                uint value = request.GetData<uint>()[0];
                visibleCounters[lod] = value;

                bool exceedsCapacity = value > visibleCapacities[lod];
                bool exceedsMaximum = value > totalInstanceCount;
                bool inactiveLODHasData = (!gpuCulling || !gpuLODEnabled) && lod > 0 && value != 0;
                bool bypassCounterNotZero = !gpuCulling && value != 0;

                if (exceedsCapacity || exceedsMaximum || inactiveLODHasData || bypassCounterNotZero)
                {
                    CriticalOnce($"counter-invariant-{cameraID}-{species}-{lod}",
                        $"Visible append counter invariant failed. {ContextText()}, Species={species}, LOD={lod}, " +
                        $"MeshPart=N/A, args[1]=N/A, VisibleCounter={value}, VisibleBufferCapacity={visibleCapacities[lod]}, " +
                        $"SourceInstanceCount={totalInstanceCount}, GPUCulling={gpuCulling}, GPULOD={gpuLODEnabled}");
                }
                else if (value > safeLimit)
                {
                    ErrorOnce($"counter-safety-limit-{cameraID}-{species}-{lod}",
                        $"VisibleCounter exceeds configured diagnostic safety limit. {ContextText()}, Species={species}, " +
                        $"LOD={lod}, VisibleCounter={value}, SafeLimit={safeLimit}");
                }
            }

            pending--;
            TryFinish();
        }

        private void CompleteArgs(
            AsyncGPUReadbackRequest request,
            int lod,
            string mesh,
            int part,
            uint expectedIndexCount,
            uint expectedIndexStart,
            uint expectedBaseVertex,
            uint expectedStartInstance)
        {
            if (request.hasError || request.GetData<uint>().Length < 5)
            {
                ErrorOnce($"args-readback-{cameraID}-{species}-{lod}-{part}",
                    $"Indirect args readback failed. {ContextText()}, Species={species}, LOD={lod}, Mesh={mesh}, MeshPart={part}");
            }
            else
            {
                var data = request.GetData<uint>();
                uint a0 = data[0];
                uint a1 = data[1];
                uint a2 = data[2];
                uint a3 = data[3];
                uint a4 = data[4];

                argsResults.Add(new ArgsResult
                {
                    lod = lod,
                    mesh = mesh,
                    part = part,
                    a0 = a0,
                    a1 = a1,
                    a2 = a2,
                    a3 = a3,
                    a4 = a4,
                    expectedA0 = expectedIndexCount,
                    expectedA2 = expectedIndexStart,
                    expectedA3 = expectedBaseVertex,
                    expectedA4 = expectedStartInstance
                });
            }

            pending--;
            TryFinish();
        }

        private void TryFinish()
        {
            if (!sealedBatch || pending != 0) return;

            for (int i = 0; i < argsResults.Count; i++)
            {
                ArgsResult result = argsResults[i];
                int capacity = visibleCapacities[result.lod];
                bool countExceedsCapacity = result.a1 > capacity;
                bool countExceedsMaximum = result.a1 > totalInstanceCount;
                bool countExceedsSafetyLimit = result.a1 > safeLimit;
                bool inactiveLODHasArgs = (!gpuCulling || !gpuLODEnabled) && result.lod > 0 && result.a1 != 0;
                bool layoutCorrupt = result.a0 != result.expectedA0 || result.a2 != result.expectedA2 ||
                                     result.a3 != result.expectedA3 || result.a4 != result.expectedA4;
                if (!countExceedsCapacity && !countExceedsMaximum && !countExceedsSafetyLimit &&
                    !inactiveLODHasArgs && !layoutCorrupt) continue;

                string visible = visibleCounters[result.lod] >= 0
                    ? visibleCounters[result.lod].ToString()
                    : "unavailable";
                CriticalOnce($"args-invalid-{cameraID}-{species}-{result.lod}-{result.part}",
                    $"Indirect args invariant failed. {ContextText()}, Species={species}, LOD={result.lod}, Mesh={result.mesh}, " +
                    $"MeshPart={result.part}, totalInstanceCount={totalInstanceCount}, visibleCount={visible}, " +
                    $"VisibleBufferCapacity={capacity}, SourceInstanceCount={totalInstanceCount}, args[1]={result.a1}, " +
                    $"args[0..4]=[{result.a0},{result.a1},{result.a2},{result.a3},{result.a4}], " +
                    $"ExpectedArgs0/2/3/4=[{result.expectedA0},{result.expectedA2},{result.expectedA3},{result.expectedA4}], " +
                    $"GPUCulling={gpuCulling}, GPULOD={gpuLODEnabled}, SafeLimit={safeLimit}");
            }

            long visibleSum = 0;
            for (int i = 0; i < visibleCounters.Length; i++)
            {
                if (visibleCounters[i] >= 0) visibleSum += visibleCounters[i];
            }

            if (gpuCulling && visibleSum > totalInstanceCount)
            {
                CriticalOnce($"lod-sum-{cameraID}-{species}",
                    $"Sum of LOD append counters exceeds the Species instance count. {ContextText()}, Species={species}, " +
                    $"totalInstanceCount={totalInstanceCount}, LOD0={visibleCounters[0]}, LOD1={visibleCounters[1]}, " +
                    $"LOD2={visibleCounters[2]}, LOD3={visibleCounters[3]}, Sum={visibleSum}");
            }

            CompleteForwardReadback(passID, gpuCulling ? visibleSum : totalInstanceCount);

            if (!forceSummary) return;

            StringBuilder builder = new StringBuilder(512);
            builder.Append("[VegetationDiag][Summary] ").Append(ContextText())
                .Append(", Species=").Append(species)
                .Append(", totalInstanceCount=").Append(totalInstanceCount)
                .Append(", CPUSelectedChunks=").Append(dispatch.selectedChunkCount).Append('/').Append(totalChunkCount)
                .Append(", ResetFrame=").Append(resetFrame)
                .Append(", ResetBuffers=VisibleLOD0/1/2/3+DummyLOD1/2/3")
                .Append(", GPUCulling=").Append(gpuCulling)
                .Append(", GPULOD=").Append(gpuLODEnabled)
                .Append(", Kernel=").Append(kernel)
                .Append(", numthreads=(").Append(threadX).Append(',').Append(threadY).Append(',').Append(threadZ).Append(')')
                .Append(", DispatchInstances=").Append(dispatch.dispatchInstanceCount)
                .Append(", DispatchCalls=").Append(dispatch.dispatchCallCount)
                .Append(", DispatchGroupsTotalX=").Append(dispatch.totalGroupX)
                .Append(", DispatchGroupMaxXYZ=(").Append(dispatch.maxGroupX).Append(",1,1)")
                .Append(", TheoreticalThreads=").Append(dispatch.theoreticalThreadCount)
                .Append(", VisibleLOD=[").Append(visibleCounters[0]).Append(',').Append(visibleCounters[1]).Append(',')
                .Append(visibleCounters[2]).Append(',').Append(visibleCounters[3]).Append(']')
                .Append(", VisibleCapacity=[").Append(visibleCapacities[0]).Append(',').Append(visibleCapacities[1]).Append(',')
                .Append(visibleCapacities[2]).Append(',').Append(visibleCapacities[3]).Append(']');

            if (argsResults.Count > 0)
            {
                builder.Append(", Args={");
                for (int i = 0; i < argsResults.Count; i++)
                {
                    if (i > 0) builder.Append("; ");
                    ArgsResult result = argsResults[i];
                    builder.Append("LOD").Append(result.lod).Append('/').Append(result.mesh).Append("/Part").Append(result.part)
                        .Append(": [").Append(result.a0).Append(',').Append(result.a1).Append(',').Append(result.a2)
                        .Append(',').Append(result.a3).Append(',').Append(result.a4).Append(']');
                }
                builder.Append('}');
            }

            Debug.Log(builder.ToString());
        }

        private string ContextText()
        {
            return $"Frame={frame}, RenderSequence={passID}, PassID={passID}, Camera={cameraName}, CameraID={cameraID}, CameraType={cameraType}";
        }
    }
}
