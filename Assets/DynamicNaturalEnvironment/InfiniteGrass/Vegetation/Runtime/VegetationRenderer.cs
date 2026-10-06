using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Profiling;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class VegetationRenderer : MonoBehaviour
{
    private static readonly List<VegetationRenderer> activeRenderers = new List<VegetationRenderer>();
    public static IReadOnlyList<VegetationRenderer> ActiveRenderers => activeRenderers;

    [Header("数据")]
    public VegetationDatabase database;

    [Header("增量 GPU 更新")]
    [Tooltip("Database 发生实例级增删改时优先做增量 GPU 更新；无法安全增量处理时只重建受影响 Species。")]
    public bool enableIncrementalUpdates = true;

    [Min(0), Tooltip("完整构建/Species 重建时，每个 Species × Chunk 至少预留的空槽数量。用于吸收运行时新增和跨 Chunk 移动。0=只使用比例预留。")]
    public int incrementalReserveSlotsPerChunk = 8;

    [Range(0f, 1f), Tooltip("每个 Species × Chunk 按当前实例数额外预留的比例。默认 10%。例如 1000 个实例额外预留约 100 个 Slot。")]
    public float incrementalReserveRatio = 0.1f;

    [Min(1), Tooltip("只有混合增删改或大量 Transform 更新超过该数量时才回退 Species Rebuild。纯新增/纯删除会走批量 GPU 上传，不受该值限制。")]
    public int incrementalMaxChangesPerSpecies = 1024;

    [Header("GPU Culling")]
    public ComputeShader cullingCompute;

    [Tooltip("Play模式使用的Culling Camera。手动指定后编辑模式也会优先使用该Camera")]
    public Camera targetCamera;

    public bool enableGPUCulling = true;
    public bool enableChunkCulling = true;
    [Tooltip("阴影路径是否先按Chunk和Shadow Cull Distance筛选，再Dispatch实例级Compute Culling。建议保持开启。")]
    public bool enableShadowChunkCulling = true;
    [Tooltip("使用Camera可见区域沿Directional Light反方向拉伸出的Shadow Influence Volume继续剔除阴影Chunk和实例。没有有效Directional Light时会自动退回距离剔除。")]
    public bool enableShadowInfluenceCulling = true;
    [Tooltip("可选阴影方向灯。为空时使用RenderSettings.sun。建议指定URP Main Directional Light。")]
    public Light shadowDirectionalLight;
    [Min(0f), Tooltip("Shadow Influence Bounds额外安全边距，单位米。风摆较大或树冠较宽时可适当增加。")]
    public float shadowInfluencePadding = 5f;
    public bool enableFrustumCulling = true;
    public bool enableCullingInEditMode = false;
    
    [Tooltip("Scene视图是否使用Target Camera进行植被剔除。关闭时使用Scene视图自己的Camera")]
    public bool sceneViewUseTargetCameraForCulling = true;

    [Tooltip("Scene视图是否提交植被阴影。关闭后只影响Scene视图，不影响Game视图阴影")]
    public bool renderSceneViewShadows = true;

    [Tooltip("Play模式下Scene视图是否继续提交植被阴影。性能测试时建议关闭。")]
    public bool renderSceneViewShadowsDuringPlay = false;

    [Header("GPU Diagnostics")]
    [InspectorName("ForceGameCameraOnly")]
    [Tooltip("诊断开关：只禁止非Game Camera的Vegetation Forward Culling/Draw，不影响阴影提交。")]
    public bool forceGameCameraOnly;

    [InspectorName("DisableGPUCulling")]
    [Tooltip("诊断开关：跳过Vegetation Compute Culling，但保留Indirect Draw。")]
    public bool disableGPUCulling;

    [InspectorName("DisableSceneForward")]
    [Tooltip("诊断开关：只禁用Scene Camera的Vegetation Forward路径。")]
    public bool disableSceneForward;

    [InspectorName("DisableSceneShadow")]
    [Tooltip("诊断开关：只禁用Scene Camera的Vegetation Shadow路径。")]
    public bool disableSceneShadow;

    [InspectorName("DisableForwardRendering")]
    [Tooltip("诊断开关：禁用所有Camera的Vegetation Forward路径。")]
    public bool disableForwardRendering;

    [InspectorName("DisableShadowRendering")]
    [Tooltip("诊断开关：禁用所有Camera的Vegetation Shadow路径。")]
    public bool disableShadowRendering;

    public VegetationDiagnosticsMode diagnosticsMode = VegetationDiagnosticsMode.Off;

    [Tooltip("是否启用GPU Append Counter / Indirect Args异步回读。正常游戏建议关闭，仅排查剔除问题时开启。")]
    public bool enableDiagnosticsGPUReadback = false;

    [Min(1)]
    [Tooltip("Verbose模式下每N帧输出Camera顺序和Species摘要。")]
    public int diagnosticsVerboseInterval = 120;

    [Min(1)]
    [Tooltip("每N帧异步读取Append Counter和Indirect Args。设为1可逐帧取证，但开销更高。")]
    public int diagnosticsGPUReadbackInterval = 30;

    [Min(1)]
    [Tooltip("超过此实例/线程工作量时立即输出一次异常。")]
    public int diagnosticsSafeInstanceLimit = 5000000;

    [Header("GPU LOD")]
    [Tooltip("开启树木Mesh LOD以及草地Density LOD")]
    public bool enableGPULOD = true;

    [Header("Forward Wind LOD")]
    [Tooltip("开启后正常可见植被按LOD降低风场成本。Mesh植被按Draw LOD分级；Grass复用Density LOD距离自动分级。不会改变LOD距离。")]
    public bool enableForwardWindLOD = true;

    [Range(0, 3), Tooltip("LOD0正常画面的风质量。0=静态，1=主摆动，2=简化风，3=完整风。建议3。")]
    public int forwardWindLOD0Quality = 3;

    [Range(0, 3), Tooltip("LOD1正常画面的风质量。建议2。")]
    public int forwardWindLOD1Quality = 2;

    [Range(0, 3), Tooltip("LOD2正常画面的风质量。建议1。")]
    public int forwardWindLOD2Quality = 1;

    [Range(0, 3), Tooltip("LOD3正常画面的风质量。建议1；如果最远LOD很小也可测试0。")]
    public int forwardWindLOD3Quality = 1;

    [Range(0, 3), Tooltip("阴影专用LOD偏移。0=与正常LOD一致，1=阴影强制至少降低一级，2=至少降低两级。只影响有Mesh LOD的植被，草的Density LOD不受影响。建议先测试1。")]
    public int shadowLODOffset = 1;

    [Tooltip("开启后ShadowCaster按每个Species的阴影LOD距离自动降级风场：近处完整→中距离简化→远处主摆动→最远静态。只影响阴影，不影响Forward画面。")]
    public bool enableShadowWindLOD = true;

    [Range(0, 3), Tooltip("关闭Shadow Wind LOD时使用的固定阴影风质量。0=静态，1=主摆动，2=简化风，3=完整风。用于画质/性能对照。")]
    public int shadowWindQuality = 3;

    [Header("Shadow Alpha LOD")]
    [Tooltip("开启后ShadowCaster会复用Shadow Wind LOD距离逐级提高Alpha Cutoff。近处保持材质原值，中远距离减少细碎叶片/草影。只影响阴影，不影响Forward和Depth。")]
    public bool enableShadowAlphaCutoffLOD = true;

    [Range(0f, 0.25f), Tooltip("中距离Shadow Alpha Cutoff额外增加值。默认0.04。")]
    public float shadowAlphaCutoffSimplifiedBias = 0.04f;

    [Range(0f, 0.25f), Tooltip("远距离Shadow Alpha Cutoff额外增加值。默认0.08。")]
    public float shadowAlphaCutoffMainBendBias = 0.08f;

    [Range(0f, 0.25f), Tooltip("最远静态阴影区Shadow Alpha Cutoff额外增加值。默认0.12。")]
    public float shadowAlphaCutoffStaticBias = 0.12f;

    [Header("Chunk Debug")]
    public bool showChunkBounds = false;
    public bool showOnlyVisibleChunks = false;
    public bool showChunkLabels = true;

    [Header("GPU LOD Debug")]
    [Tooltip("是否在Scene视图显示LOD距离范围")]
    public bool showLODDistance = false;

    [Tooltip("查看哪个Species的LOD范围。为空时自动选择第一个有效Species")]
    public VegetationSpecies lodDebugSpecies;

    [Tooltip("是否显示LOD距离和密度文字")]
    public bool showLODLabels = true;

    [Header("调试")]
    [SerializeField] private int uploadedInstanceCount;
    [SerializeField] private int drawCallCount;
    [SerializeField] private int chunkRangeCount;
    [SerializeField] private int visibleChunkRangeCount;
    [SerializeField] private int cullingDispatchCount;
    [SerializeField] private int incrementalSlotUpdateCount;
    [SerializeField] private int incrementalSpeciesRebuildCount;

    public int UploadedInstanceCount => uploadedInstanceCount;
    public int RenderGroupCount => renderGroups.Count;
    public int ChunkRangeCount => chunkRangeCount;
    public int VisibleChunkRangeCount => visibleChunkRangeCount;
    public int CullingDispatchCount => cullingDispatchCount;
    public int IncrementalSlotUpdateCount => incrementalSlotUpdateCount;
    public int IncrementalSpeciesRebuildCount => incrementalSpeciesRebuildCount;
    public int ForwardCameraCount => completedStatsFrame >= 0 ? completedForwardCameraCount : forwardCameraIDs.Count;
    public int ForwardDispatchCount => completedStatsFrame >= 0 ? completedForwardDispatchCount : currentForwardDispatchCount;
    public int ForwardDrawCount => completedStatsFrame >= 0 ? completedForwardDrawCount : currentForwardDrawCount;
    public int ShadowDispatchCount => completedStatsFrame >= 0 ? completedShadowDispatchCount : currentShadowDispatchCount;
    public int ShadowDrawCount => completedStatsFrame >= 0 ? completedShadowDrawCount : currentShadowDrawCount;
    public Camera CurrentCamera => currentCamera;
    public int ShadowCameraStateCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < renderGroups.Count; i++) count = Mathf.Max(count, renderGroups[i].shadowStates.Count);
            return count;
        }
    }

    private readonly List<RenderGroup> renderGroups = new List<RenderGroup>();
    private readonly Dictionary<int, RenderGroup> renderGroupBySpeciesIndex = new Dictionary<int, RenderGroup>();
    private readonly HashSet<RenderGroup> incrementalDirtyGroups = new HashSet<RenderGroup>();
    private readonly List<VegetationDatabaseChangeSet> pendingChangeSets = new List<VegetationDatabaseChangeSet>();
    private readonly Plane[] frustumPlanes = new Plane[6];
    private readonly Vector3[] shadowNearCorners = new Vector3[4];
    private readonly Vector3[] shadowFarCorners = new Vector3[4];

    private int uploadedDataRevision = int.MinValue;
    private VegetationDatabase uploadedDatabase;
    private int cullOnlyKernel = -1;
    private int cullLODKernel = -1;
    private uint cullOnlyThreadX = 64;
    private uint cullOnlyThreadY = 1;
    private uint cullOnlyThreadZ = 1;
    private uint cullLODThreadX = 64;
    private uint cullLODThreadY = 1;
    private uint cullLODThreadZ = 1;
    private bool needsRebuild = true;
    private bool preparedWithCulling;
    private readonly HashSet<int> forwardCameraIDs = new HashSet<int>();
    private int runtimeStatsFrame = -1;
    private int completedStatsFrame = -1;
    private int currentForwardDispatchCount;
    private int currentForwardDrawCount;
    private int currentShadowDispatchCount;
    private int currentShadowDrawCount;
    private int completedForwardCameraCount;
    private int completedForwardDispatchCount;
    private int completedForwardDrawCount;
    private int completedShadowDispatchCount;
    private int completedShadowDrawCount;
    private Camera currentCamera;
    private readonly Matrix4x4[] singleMatrixUpload = new Matrix4x4[1];
    private readonly Vector4[] singleBoundsUpload = new Vector4[1];
    private readonly uint[] singlePersistentIDUpload = new uint[1];
    private Matrix4x4[] batchMatrixUpload = new Matrix4x4[256];
    private Vector4[] batchBoundsUpload = new Vector4[256];
    private uint[] batchPersistentIDUpload = new uint[256];
    private uint[] batchZeroPersistentIDUpload = new uint[256];

    private static readonly int VegetationMatricesID = Shader.PropertyToID("_VegetationMatrices");
    private static readonly int VegetationVisibleIndicesID = Shader.PropertyToID("_VegetationVisibleIndices");
    private static readonly int VegetationUseVisibleIndicesID = Shader.PropertyToID("_VegetationUseVisibleIndices");

    private static readonly int SourceBoundsID = Shader.PropertyToID("_SourceBounds");
    private static readonly int SourceMatricesID = Shader.PropertyToID("_SourceMatrices");
    private static readonly int PersistentIDsID = Shader.PropertyToID("_PersistentIDs");
    private static readonly int VisibleLOD0ID = Shader.PropertyToID("_VisibleLOD0");
    private static readonly int VisibleLOD1ID = Shader.PropertyToID("_VisibleLOD1");
    private static readonly int VisibleLOD2ID = Shader.PropertyToID("_VisibleLOD2");
    private static readonly int VisibleLOD3ID = Shader.PropertyToID("_VisibleLOD3");

    private static readonly int StartIndexID = Shader.PropertyToID("_StartIndex");
    private static readonly int InstanceCountID = Shader.PropertyToID("_InstanceCount");

    private static readonly int EnableFrustumCullingID = Shader.PropertyToID("_EnableFrustumCulling");
    private static readonly int EnableShadowInfluenceCullingID = Shader.PropertyToID("_EnableShadowInfluenceCulling");
    private static readonly int ShadowInfluenceMinID = Shader.PropertyToID("_ShadowInfluenceMin");
    private static readonly int ShadowInfluenceMaxID = Shader.PropertyToID("_ShadowInfluenceMax");
    private static readonly int IsOrthographicID = Shader.PropertyToID("_IsOrthographic");

    private static readonly int HasLOD1ID = Shader.PropertyToID("_HasLOD1");
    private static readonly int HasLOD2ID = Shader.PropertyToID("_HasLOD2");
    private static readonly int HasLOD3ID = Shader.PropertyToID("_HasLOD3");
    private static readonly int UseDensityLODID = Shader.PropertyToID("_UseDensityLOD");
    private static readonly int LODOffsetID = Shader.PropertyToID("_LODOffset");

    private static readonly int CameraPositionWSID = Shader.PropertyToID("_CameraPositionWS");
    private static readonly int CameraForwardWSID = Shader.PropertyToID("_CameraForwardWS");
    private static readonly int CameraRightWSID = Shader.PropertyToID("_CameraRightWS");
    private static readonly int CameraUpWSID = Shader.PropertyToID("_CameraUpWS");

    // DrawMeshInstancedIndirect has no Renderer from which Unity can populate the
    // per-draw light-probe constants. Bind the scene ambient probe explicitly so
    // SAMPLE_GI / SampleSH never inherit zero or stale unity_SH values per camera.
    private static readonly int UnitySHArID = Shader.PropertyToID("unity_SHAr");
    private static readonly int UnitySHAgID = Shader.PropertyToID("unity_SHAg");
    private static readonly int UnitySHAbID = Shader.PropertyToID("unity_SHAb");
    private static readonly int UnitySHBrID = Shader.PropertyToID("unity_SHBr");
    private static readonly int UnitySHBgID = Shader.PropertyToID("unity_SHBg");
    private static readonly int UnitySHBbID = Shader.PropertyToID("unity_SHBb");
    private static readonly int UnitySHCID = Shader.PropertyToID("unity_SHC");
    private static readonly int UnityProbesOcclusionID = Shader.PropertyToID("unity_ProbesOcclusion");
    private static readonly int VegetationAdditionalLightsCountID = Shader.PropertyToID("_VegetationAdditionalLightsCount");
    private static readonly int VegetationReceiveShadowsID = Shader.PropertyToID("_VegetationReceiveShadows");
    private static readonly int VegetationForwardWindLODEnabledID = Shader.PropertyToID("_VegetationForwardWindLODEnabled");
    private static readonly int VegetationForwardWindQualityID = Shader.PropertyToID("_VegetationForwardWindQuality");
    private static readonly int VegetationForwardWindUseDistanceLODID = Shader.PropertyToID("_VegetationForwardWindUseDistanceLOD");
    private static readonly int VegetationForwardWindLODDistanceSqID = Shader.PropertyToID("_VegetationForwardWindLODDistanceSq");
    private static readonly int VegetationShadowWindQualityID = Shader.PropertyToID("_VegetationShadowWindQuality");
    private static readonly int VegetationShadowWindLODEnabledID = Shader.PropertyToID("_VegetationShadowWindLODEnabled");
    private static readonly int VegetationShadowWindLODDistanceSqID = Shader.PropertyToID("_VegetationShadowWindLODDistanceSq");
    private static readonly int VegetationShadowAlphaCutoffLODEnabledID = Shader.PropertyToID("_VegetationShadowAlphaCutoffLODEnabled");
    private static readonly int VegetationShadowAlphaCutoffBiasID = Shader.PropertyToID("_VegetationShadowAlphaCutoffBias");

    private static readonly int LOD0DistanceID = Shader.PropertyToID("_LOD0Distance");
    private static readonly int LOD1DistanceID = Shader.PropertyToID("_LOD1Distance");
    private static readonly int LOD2DistanceID = Shader.PropertyToID("_LOD2Distance");
    private static readonly int CullDistanceID = Shader.PropertyToID("_CullDistance");
    private static readonly int LODCrossFadeWidthsID = Shader.PropertyToID("_LODCrossFadeWidths");
    private static readonly int VegetationLODIndexID = Shader.PropertyToID("_VegetationLODIndex");
    private static readonly int VegetationLODDistancesID = Shader.PropertyToID("_VegetationLODDistances");
    private static readonly int VegetationLODCrossFadeParamsID = Shader.PropertyToID("_VegetationLODCrossFadeParams");
    private static readonly int VegetationLODReferencePositionID = Shader.PropertyToID("_VegetationLODReferencePosition");
    private static readonly int VegetationLODOffsetID = Shader.PropertyToID("_VegetationLODOffset");

    private static readonly int MidDensityID = Shader.PropertyToID("_MidDensity");
    private static readonly int FarDensityID = Shader.PropertyToID("_FarDensity");

    private static readonly int TanHalfFovID = Shader.PropertyToID("_TanHalfFov");
    private static readonly int AspectID = Shader.PropertyToID("_Aspect");
    private static readonly int NearClipID = Shader.PropertyToID("_NearClip");
    private static readonly int OrthographicSizeID = Shader.PropertyToID("_OrthographicSize");

    private static readonly ProfilerMarker ShadowTotalProfilerMarker = new ProfilerMarker("Vegetation Shadow Total");
    private static readonly ProfilerMarker ShadowCullProfilerMarker = new ProfilerMarker("Vegetation Shadow Cull");
    private static readonly ProfilerMarker ShadowDrawProfilerMarker = new ProfilerMarker("Vegetation Shadow Draw");
    private static readonly ProfilerMarker RebuildProfilerMarker = new ProfilerMarker("Vegetation.Rebuild");
    private static readonly ProfilerMarker IncrementalUpdateProfilerMarker = new ProfilerMarker("Vegetation.IncrementalUpdate");
    private static readonly ProfilerMarker SpeciesRebuildProfilerMarker = new ProfilerMarker("Vegetation.RebuildSpecies");
    private static readonly ProfilerMarker ForwardPrepareProfilerMarker = new ProfilerMarker("Vegetation.Forward.Prepare");
    private static readonly ProfilerMarker ForwardCullingProfilerMarker = new ProfilerMarker("Vegetation.Forward.Culling");
    private static readonly ProfilerMarker ForwardDrawCpuProfilerMarker = new ProfilerMarker("Vegetation.Forward.Draw");
    private static readonly ProfilerMarker ShadowPrepareProfilerMarker = new ProfilerMarker("Vegetation.Shadow.Prepare");
    private static readonly ProfilerMarker ShadowCullingCpuProfilerMarker = new ProfilerMarker("Vegetation.Shadow.Culling");
    private static readonly ProfilerMarker ShadowDrawCpuProfilerMarker = new ProfilerMarker("Vegetation.Shadow.Draw");

    private struct PendingSlotWrite
    {
        public int slot;
        public VegetationInstance instance;
        public ChunkRange range;

        public PendingSlotWrite(int slot, VegetationInstance instance, ChunkRange range)
        {
            this.slot = slot;
            this.instance = instance;
            this.range = range;
        }
    }

    private struct LODCrossFadeData
    {
        public Vector4 distances;
        public Vector4 widths;
        public Vector4 referencePosition;
        public int offset;
    }

    private class ChunkRange
    {
        public Vector2Int coordinate;
        public Bounds bounds;
        public int startIndex;
        public int count;
        public int activeCount;
        public bool visibleLastFrame;
        public readonly Stack<int> freeSlots = new Stack<int>();
    }

    private class BuildData
    {
        public int speciesIndex;
        public VegetationSpecies species;
        public readonly List<VegetationInstance> instances = new List<VegetationInstance>();
        public readonly List<ChunkRange> chunkRanges = new List<ChunkRange>();
    }

    private class RenderPart
    {
        public int subMeshIndex;
        public Material material;
        public ComputeBuffer argsBuffer;
        public uint[] fullArgs;

        public void RestoreFullInstanceCount()
        {
            if (argsBuffer == null || fullArgs == null) return;
            argsBuffer.SetData(fullArgs);
        }

        public void SetInstanceCount(CommandBuffer cmd, uint instanceCount)
        {
            if (cmd == null || argsBuffer == null || fullArgs == null || fullArgs.Length < 5) return;
            fullArgs[1] = instanceCount;
            cmd.SetBufferData(argsBuffer, fullArgs);
        }

        public void Release()
        {
            argsBuffer?.Release();
            argsBuffer = null;
        }
    }

    private class ShadowRenderPart
    {
        public ComputeBuffer argsBuffer;
        private readonly uint[] args;

        public ShadowRenderPart(uint[] sourceArgs)
        {
            args = (uint[])sourceArgs.Clone();
            argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
            argsBuffer.SetData(args);
        }

        public void SetInstanceCount(CommandBuffer cmd, uint instanceCount)
        {
            if (cmd == null || argsBuffer == null) return;

            args[1] = instanceCount;
            cmd.SetBufferData(argsBuffer, args);
        }

        public void Release()
        {
            argsBuffer?.Release();
            argsBuffer = null;
        }
    }

    private class ShadowLODRenderData
    {
        public ComputeBuffer visibleIndexBuffer;
        public MaterialPropertyBlock propertyBlock;
        public readonly List<ShadowRenderPart> parts = new List<ShadowRenderPart>();

        public void Release()
        {
            visibleIndexBuffer?.Release();
            visibleIndexBuffer = null;

            for (int i = 0; i < parts.Count; i++) parts[i].Release();
            parts.Clear();
        }
    }

    private class CameraShadowState
    {
        public Camera camera;
        public LODCrossFadeData crossFade;
        public ShadowLODRenderData lod0;
        public ShadowLODRenderData lod1;
        public ShadowLODRenderData lod2;
        public ShadowLODRenderData lod3;
        public ComputeBuffer dummyLOD1Buffer;
        public ComputeBuffer dummyLOD2Buffer;
        public ComputeBuffer dummyLOD3Buffer;
        public Bounds drawBounds;
        public bool hasDrawBounds;
        public long diagnosticResetShadowInvocationID = -1;

        public void Release()
        {
            lod0?.Release();
            lod1?.Release();
            lod2?.Release();
            lod3?.Release();

            dummyLOD1Buffer?.Release();
            dummyLOD1Buffer = null;

            dummyLOD2Buffer?.Release();
            dummyLOD2Buffer = null;

            dummyLOD3Buffer?.Release();
            dummyLOD3Buffer = null;
        }
    }

    private class LODRenderData
    {
        public Mesh mesh;
        public VegetationLODAsset asset;
        public ComputeBuffer visibleIndexBuffer;
        public ComputeBuffer diagnosticCounterBuffer;
        public MaterialPropertyBlock propertyBlock;
        public readonly List<RenderPart> parts = new List<RenderPart>();

        public bool IsValid => mesh != null && asset != null && asset.IsValid && parts.Count > 0;

        public void Release()
        {
            visibleIndexBuffer?.Release();
            visibleIndexBuffer = null;

            diagnosticCounterBuffer?.Release();
            diagnosticCounterBuffer = null;

            for (int i = 0; i < parts.Count; i++) parts[i].Release();

            parts.Clear();
        }
    }

    private class RenderGroup
    {
        public LODCrossFadeData forwardCrossFade;
        public int speciesIndex;
        public VegetationSpecies species;
        public ComputeBuffer matrixBuffer;
        public ComputeBuffer boundsBuffer;
        public ComputeBuffer persistentIDBuffer;
        public ComputeBuffer dummyLOD1Buffer;
        public ComputeBuffer dummyLOD2Buffer;
        public ComputeBuffer dummyLOD3Buffer;
        public LODRenderData lod0;
        public LODRenderData lod1;
        public LODRenderData lod2;
        public LODRenderData lod3;
        public Bounds drawBounds;
        public int instanceCount;
        public int activeInstanceCount;
        public readonly Dictionary<int, int> slotByPersistentID = new Dictionary<int, int>();
        public readonly Dictionary<Vector2Int, ChunkRange> chunkRangeLookup = new Dictionary<Vector2Int, ChunkRange>();
        public readonly HashSet<ChunkRange> dirtyChunkRanges = new HashSet<ChunkRange>();
        public readonly List<ChunkRange> chunkRanges = new List<ChunkRange>();
        public readonly Dictionary<int, CameraShadowState> shadowStates = new Dictionary<int, CameraShadowState>();
        public readonly List<int> staleShadowStateIDs = new List<int>();
        public string diagnosticResourceKey;
        public long diagnosticForwardPreparePassID = -1;
        public long diagnosticForwardResetPassID = -1;
        public int diagnosticForwardResetFrame = -1;
        public int diagnosticForwardResetCameraID;

        public void Release()
        {
            matrixBuffer?.Release();
            matrixBuffer = null;

            boundsBuffer?.Release();
            boundsBuffer = null;

            persistentIDBuffer?.Release();
            persistentIDBuffer = null;

            dummyLOD1Buffer?.Release();
            dummyLOD1Buffer = null;

            dummyLOD2Buffer?.Release();
            dummyLOD2Buffer = null;

            dummyLOD3Buffer?.Release();
            dummyLOD3Buffer = null;

            lod0?.Release();
            lod1?.Release();
            lod2?.Release();
            lod3?.Release();

            foreach (CameraShadowState state in shadowStates.Values) state.Release();
            shadowStates.Clear();
            staleShadowStateIDs.Clear();

            slotByPersistentID.Clear();
            chunkRangeLookup.Clear();
            dirtyChunkRanges.Clear();
            chunkRanges.Clear();
        }
    }

    private void OnEnable()
    {
        if (!activeRenderers.Contains(this)) activeRenderers.Add(this);

        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;

        needsRebuild = true;
        cullOnlyKernel = -1;
        cullLODKernel = -1;
        ResetKernelThreadMetadata();
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        activeRenderers.Remove(this);

        ReleaseBuffers();

        uploadedDatabase = null;
        uploadedDataRevision = int.MinValue;
        preparedWithCulling = false;
    }

    private void OnValidate()
    {
        diagnosticsVerboseInterval = Mathf.Max(1, diagnosticsVerboseInterval);
        shadowInfluencePadding = Mathf.Max(0f, shadowInfluencePadding);
        diagnosticsGPUReadbackInterval = Mathf.Max(1, diagnosticsGPUReadbackInterval);
        diagnosticsSafeInstanceLimit = Mathf.Max(1, diagnosticsSafeInstanceLimit);
        incrementalReserveSlotsPerChunk = Mathf.Max(0, incrementalReserveSlotsPerChunk);
        incrementalReserveRatio = Mathf.Clamp01(incrementalReserveRatio);
        incrementalMaxChangesPerSpecies = Mathf.Max(1, incrementalMaxChangesPerSpecies);
        needsRebuild = true;
        cullOnlyKernel = -1;
        cullLODKernel = -1;
        ResetKernelThreadMetadata();
    }

    private void Update()
    {
        EnsureBuffersUpToDate();
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (!isActiveAndEnabled || camera == null) return;
        if (!ShouldRenderShadowsForCamera(camera)) return;

        BeginRuntimeStatsFrame();
        currentCamera = camera;

        EnsureBuffersUpToDate();
        if (renderGroups.Count == 0) return;

        long shadowInvocationID = VegetationDiagnostics.BeginShadow(camera, diagnosticsMode);
        CommandBuffer cmd = CommandBufferPool.Get("Vegetation Shadow Culling");
        try
        {
            using (ShadowTotalProfilerMarker.Auto())
            {
                using (ShadowCullProfilerMarker.Auto())
                {
                    // GPU sample must begin and end inside the same submitted command buffer.
                    // Do not let a sample span ExecuteCommandBuffer()/Clear().
                    cmd.BeginSample("Vegetation Shadow Cull");
                    using (ShadowPrepareProfilerMarker.Auto())
                    using (ShadowCullingCpuProfilerMarker.Auto())
                        PrepareShadowsForCamera(cmd, camera, shadowInvocationID);
                    cmd.EndSample("Vegetation Shadow Cull");
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                }

                using (ShadowDrawProfilerMarker.Auto())
                {
                    // SubmitShadowCasters uses Graphics.DrawMeshInstancedIndirect directly rather
                    // than this CommandBuffer, so keep Draw/Total as CPU ProfilerMarkers here.
                    using (ShadowDrawCpuProfilerMarker.Auto()) SubmitShadowCasters(camera, shadowInvocationID);
                }
            }
        }
        finally
        {
            CommandBufferPool.Release(cmd);
            VegetationDiagnostics.EndShadow(shadowInvocationID, camera);
        }
    }

    private void EnsureBuffersUpToDate()
    {
        if (database == null)
        {
            if (renderGroups.Count > 0) ReleaseBuffers();

            uploadedDatabase = null;
            uploadedDataRevision = int.MinValue;
            needsRebuild = true;
            return;
        }

        if (uploadedDatabase != database || needsRebuild)
        {
            Rebuild();
            return;
        }

        if (uploadedDataRevision == database.DataRevision) return;

        if (enableIncrementalUpdates && database.TryGetChangeSets(uploadedDataRevision, pendingChangeSets))
        {
            bool appliedAll = true;

            for (int i = 0; i < pendingChangeSets.Count; i++)
            {
                VegetationDatabaseChangeSet changeSet = pendingChangeSets[i];

                if (
                    changeSet == null ||
                    changeSet.requiresFullRebuild ||
                    !ApplyIncrementalChangeSet(changeSet)
                )
                {
                    appliedAll = false;
                    break;
                }

                uploadedDataRevision = changeSet.toRevision;
            }

            pendingChangeSets.Clear();

            if (appliedAll && uploadedDataRevision == database.DataRevision)
            {
                return;
            }
        }

        pendingChangeSets.Clear();
        Rebuild();
    }

    [ContextMenu("Rebuild GPU Buffers")]
    public void Rebuild()
    {
        RebuildProfilerMarker.Begin();
        try
        {
        ReleaseBuffers();

        uploadedInstanceCount = 0;
        drawCallCount = 0;
        chunkRangeCount = 0;
        visibleChunkRangeCount = 0;
        cullingDispatchCount = 0;

        uploadedDatabase = database;
        uploadedDataRevision = database != null ? database.DataRevision : int.MinValue;

        needsRebuild = false;
        preparedWithCulling = false;

        if (database == null || database.species == null || database.chunks == null) return;

        Dictionary<int, BuildData> buildDataBySpecies = new Dictionary<int, BuildData>();

        for (int chunkIndex = 0; chunkIndex < database.chunks.Count; chunkIndex++)
        {
            VegetationChunkData chunk = database.chunks[chunkIndex];

            if (chunk == null || chunk.instances == null || chunk.instances.Count == 0) continue;

            Dictionary<int, List<VegetationInstance>> chunkSpecies = new Dictionary<int, List<VegetationInstance>>();

            for (int instanceIndex = 0; instanceIndex < chunk.instances.Count; instanceIndex++)
            {
                VegetationInstance instance = chunk.instances[instanceIndex];

                if (instance.speciesIndex < 0 || instance.speciesIndex >= database.species.Count) continue;

                VegetationSpecies species = database.species[instance.speciesIndex];

                if (species == null || species.lod0 == null || !species.lod0.IsValid) continue;

                if (!chunkSpecies.TryGetValue(instance.speciesIndex, out List<VegetationInstance> list))
                {
                    list = new List<VegetationInstance>();
                    chunkSpecies.Add(instance.speciesIndex, list);
                }

                list.Add(instance);
            }

            foreach (KeyValuePair<int, List<VegetationInstance>> pair in chunkSpecies)
            {
                VegetationSpecies species = database.species[pair.Key];

                if (!buildDataBySpecies.TryGetValue(pair.Key, out BuildData build))
                {
                    build = new BuildData { speciesIndex = pair.Key, species = species };
                    buildDataBySpecies.Add(pair.Key, build);
                }

                int startIndex = build.instances.Count;

                build.instances.AddRange(pair.Value);

                build.chunkRanges.Add(new ChunkRange
                {
                    coordinate = chunk.coordinate,
                    startIndex = startIndex,
                    count = pair.Value.Count,
                    activeCount = pair.Value.Count,
                    bounds = CalculateBounds(species, pair.Value),
                    visibleLastFrame = true
                });
            }
        }

        foreach (KeyValuePair<int, BuildData> pair in buildDataBySpecies)
        {
            if (pair.Value.instances.Count == 0) continue;

            CreateRenderGroup(pair.Value);
        }
        }
        finally
        {
            RebuildProfilerMarker.End();
        }
    }

    private bool ApplyIncrementalChangeSet(VegetationDatabaseChangeSet changeSet)
    {
        if (changeSet == null || database == null) return false;

        using (IncrementalUpdateProfilerMarker.Auto())
        {
            HashSet<int> rebuiltSpecies = new HashSet<int>();
            Dictionary<int, List<VegetationDatabaseChange>> changesBySpecies = new Dictionary<int, List<VegetationDatabaseChange>>();

            for (int i = 0; i < changeSet.rebuildSpeciesIndices.Count; i++)
            {
                int speciesIndex = changeSet.rebuildSpeciesIndices[i];
                if (speciesIndex < 0 || speciesIndex >= database.species.Count) return false;
                if (!rebuiltSpecies.Add(speciesIndex)) continue;

                RebuildSpeciesGroup(speciesIndex);
                incrementalSpeciesRebuildCount++;
            }

            for (int i = 0; i < changeSet.changes.Count; i++)
            {
                VegetationDatabaseChange change = changeSet.changes[i];
                int speciesIndex = change.instance.speciesIndex;
                if (speciesIndex < 0 || speciesIndex >= database.species.Count) return false;
                if (rebuiltSpecies.Contains(speciesIndex)) continue;

                if (!changesBySpecies.TryGetValue(speciesIndex, out List<VegetationDatabaseChange> list))
                {
                    list = new List<VegetationDatabaseChange>();
                    changesBySpecies.Add(speciesIndex, list);
                }

                list.Add(change);
            }

            foreach (KeyValuePair<int, List<VegetationDatabaseChange>> pair in changesBySpecies)
            {
                int speciesIndex = pair.Key;
                List<VegetationDatabaseChange> changes = pair.Value;
                if (changes.Count == 0 || rebuiltSpecies.Contains(speciesIndex)) continue;

                bool onlyAdded = true;
                bool onlyRemoved = true;

                for (int i = 0; i < changes.Count; i++)
                {
                    VegetationDatabaseChangeType type = changes[i].type;
                    if (type != VegetationDatabaseChangeType.Added) onlyAdded = false;
                    if (type != VegetationDatabaseChangeType.Removed) onlyRemoved = false;
                }

                bool applied;

                if (onlyAdded) applied = TryApplyAddedInstancesBatch(changes);
                else if (onlyRemoved) applied = TryApplyRemovedInstancesBatch(changes);
                else if (changes.Count > incrementalMaxChangesPerSpecies) applied = false;
                else
                {
                    applied = true;

                    for (int i = 0; i < changes.Count; i++)
                    {
                        VegetationDatabaseChange change = changes[i];

                        switch (change.type)
                        {
                            case VegetationDatabaseChangeType.Added:
                                applied = TryApplyAddedInstance(change);
                                break;
                            case VegetationDatabaseChangeType.Removed:
                                applied = TryApplyRemovedInstance(change);
                                break;
                            case VegetationDatabaseChangeType.TransformUpdated:
                                applied = TryApplyTransformUpdate(change);
                                break;
                            default:
                                applied = false;
                                break;
                        }

                        if (!applied) break;
                    }
                }

                if (applied) continue;

                RebuildSpeciesGroup(speciesIndex);
                rebuiltSpecies.Add(speciesIndex);
                incrementalSpeciesRebuildCount++;
            }

            FlushIncrementalBoundsUpdates();
            uploadedInstanceCount = database.TotalInstanceCount;
            RefreshAggregateStats();
            preparedWithCulling = false;
            return true;
        }
    }

    private bool TryApplyAddedInstancesBatch(List<VegetationDatabaseChange> changes)
    {
        if (changes == null || changes.Count == 0) return true;

        int speciesIndex = changes[0].instance.speciesIndex;
        if (!renderGroupBySpeciesIndex.TryGetValue(speciesIndex, out RenderGroup group)) return false;

        Dictionary<ChunkRange, int> requiredSlots = new Dictionary<ChunkRange, int>();

        for (int i = 0; i < changes.Count; i++)
        {
            VegetationDatabaseChange change = changes[i];
            if (change.type != VegetationDatabaseChangeType.Added || change.instance.speciesIndex != speciesIndex) return false;
            if (!group.chunkRangeLookup.TryGetValue(change.newChunkCoordinate, out ChunkRange range)) return false;

            requiredSlots.TryGetValue(range, out int count);
            requiredSlots[range] = count + 1;
        }

        foreach (KeyValuePair<ChunkRange, int> pair in requiredSlots)
        {
            if (pair.Key.freeSlots.Count < pair.Value) return false;
        }

        List<PendingSlotWrite> writes = new List<PendingSlotWrite>(changes.Count);

        for (int i = 0; i < changes.Count; i++)
        {
            VegetationDatabaseChange change = changes[i];
            ChunkRange range = group.chunkRangeLookup[change.newChunkCoordinate];
            int slot = range.freeSlots.Pop();

            writes.Add(new PendingSlotWrite(slot, change.instance, range));
            group.slotByPersistentID[change.instance.persistentID] = slot;
            range.activeCount++;
            group.activeInstanceCount++;
            MarkIncrementalBoundsDirty(group, range);
        }

        UploadInstanceSlotsBatch(group, writes);
        incrementalSlotUpdateCount += changes.Count;
        return true;
    }

    private bool TryApplyRemovedInstancesBatch(List<VegetationDatabaseChange> changes)
    {
        if (changes == null || changes.Count == 0) return true;

        int speciesIndex = changes[0].instance.speciesIndex;
        if (!renderGroupBySpeciesIndex.TryGetValue(speciesIndex, out RenderGroup group)) return false;

        List<int> slots = new List<int>(changes.Count);
        List<ChunkRange> ranges = new List<ChunkRange>(changes.Count);
        HashSet<int> seenIDs = new HashSet<int>();

        for (int i = 0; i < changes.Count; i++)
        {
            VegetationDatabaseChange change = changes[i];
            int persistentID = change.instance.persistentID;

            if (change.type != VegetationDatabaseChangeType.Removed || change.instance.speciesIndex != speciesIndex) return false;
            if (!seenIDs.Add(persistentID)) return false;
            if (!group.slotByPersistentID.TryGetValue(persistentID, out int slot)) return false;
            if (!group.chunkRangeLookup.TryGetValue(change.oldChunkCoordinate, out ChunkRange range)) return false;
            if (slot < range.startIndex || slot >= range.startIndex + range.count) return false;

            slots.Add(slot);
            ranges.Add(range);
        }

        for (int i = 0; i < changes.Count; i++)
        {
            int persistentID = changes[i].instance.persistentID;
            int slot = slots[i];
            ChunkRange range = ranges[i];

            group.slotByPersistentID.Remove(persistentID);
            range.freeSlots.Push(slot);
            range.activeCount = Mathf.Max(0, range.activeCount - 1);
            group.activeInstanceCount = Mathf.Max(0, group.activeInstanceCount - 1);
            MarkIncrementalBoundsDirty(group, range);
        }

        ClearPersistentIDSlotsBatch(group, slots);
        incrementalSlotUpdateCount += changes.Count;
        return true;
    }

    private void UploadInstanceSlotsBatch(RenderGroup group, List<PendingSlotWrite> writes)
    {
        if (group == null || writes == null || writes.Count == 0) return;

        writes.Sort((a, b) => a.slot.CompareTo(b.slot));
        Bounds localBounds = group.species.GetLocalMeshBounds();
        int runStart = 0;

        while (runStart < writes.Count)
        {
            int runEnd = runStart + 1;
            while (runEnd < writes.Count && writes[runEnd].slot == writes[runEnd - 1].slot + 1) runEnd++;

            int runLength = runEnd - runStart;
            EnsureBatchUploadCapacity(runLength);

            for (int i = 0; i < runLength; i++)
            {
                PendingSlotWrite write = writes[runStart + i];
                Matrix4x4 matrix = write.instance.LocalToWorldMatrix;
                Bounds worldBounds = CalculateWorldBounds(localBounds, matrix, group.species.horizontalBoundsPadding, group.species.verticalBoundsPadding);
                float radius = Mathf.Max(worldBounds.extents.magnitude, 0.01f);

                batchMatrixUpload[i] = matrix;
                batchBoundsUpload[i] = new Vector4(worldBounds.center.x, worldBounds.center.y, worldBounds.center.z, radius);
                batchPersistentIDUpload[i] = unchecked((uint)write.instance.persistentID);
            }

            int destinationStart = writes[runStart].slot;
            group.matrixBuffer.SetData(batchMatrixUpload, 0, destinationStart, runLength);
            group.boundsBuffer.SetData(batchBoundsUpload, 0, destinationStart, runLength);
            group.persistentIDBuffer.SetData(batchPersistentIDUpload, 0, destinationStart, runLength);
            runStart = runEnd;
        }
    }

    private void ClearPersistentIDSlotsBatch(RenderGroup group, List<int> slots)
    {
        if (group == null || slots == null || slots.Count == 0) return;

        slots.Sort();
        int runStart = 0;

        while (runStart < slots.Count)
        {
            int runEnd = runStart + 1;
            while (runEnd < slots.Count && slots[runEnd] == slots[runEnd - 1] + 1) runEnd++;

            int runLength = runEnd - runStart;
            EnsureBatchUploadCapacity(runLength);
            group.persistentIDBuffer.SetData(batchZeroPersistentIDUpload, 0, slots[runStart], runLength);
            runStart = runEnd;
        }
    }

    private void EnsureBatchUploadCapacity(int required)
    {
        if (required <= batchMatrixUpload.Length) return;

        int capacity = Mathf.NextPowerOfTwo(required);
        batchMatrixUpload = new Matrix4x4[capacity];
        batchBoundsUpload = new Vector4[capacity];
        batchPersistentIDUpload = new uint[capacity];
        batchZeroPersistentIDUpload = new uint[capacity];
    }

    private bool TryApplyAddedInstance(VegetationDatabaseChange change)
    {
        int speciesIndex = change.instance.speciesIndex;

        if (!renderGroupBySpeciesIndex.TryGetValue(speciesIndex, out RenderGroup group)) return false;
        if (!group.chunkRangeLookup.TryGetValue(change.newChunkCoordinate, out ChunkRange range)) return false;
        if (range.freeSlots.Count == 0) return false;

        int slot = range.freeSlots.Pop();
        WriteInstanceSlot(group, slot, change.instance);
        group.slotByPersistentID[change.instance.persistentID] = slot;
        range.activeCount++;
        group.activeInstanceCount++;

        MarkIncrementalBoundsDirty(group, range);
        incrementalSlotUpdateCount++;
        return true;
    }

    private bool TryApplyRemovedInstance(VegetationDatabaseChange change)
    {
        int speciesIndex = change.instance.speciesIndex;

        if (!renderGroupBySpeciesIndex.TryGetValue(speciesIndex, out RenderGroup group)) return false;
        if (!group.slotByPersistentID.TryGetValue(change.instance.persistentID, out int slot)) return false;
        if (!group.chunkRangeLookup.TryGetValue(change.oldChunkCoordinate, out ChunkRange range)) return false;
        if (slot < range.startIndex || slot >= range.startIndex + range.count) return false;

        group.slotByPersistentID.Remove(change.instance.persistentID);
        ClearInstanceSlot(group, slot);
        range.freeSlots.Push(slot);
        range.activeCount = Mathf.Max(0, range.activeCount - 1);
        group.activeInstanceCount = Mathf.Max(0, group.activeInstanceCount - 1);

        MarkIncrementalBoundsDirty(group, range);
        incrementalSlotUpdateCount++;
        return true;
    }

    private bool TryApplyTransformUpdate(VegetationDatabaseChange change)
    {
        int speciesIndex = change.instance.speciesIndex;

        if (!renderGroupBySpeciesIndex.TryGetValue(speciesIndex, out RenderGroup group)) return false;
        if (!group.slotByPersistentID.TryGetValue(change.instance.persistentID, out int sourceSlot)) return false;
        if (!group.chunkRangeLookup.TryGetValue(change.oldChunkCoordinate, out ChunkRange oldRange)) return false;
        if (sourceSlot < oldRange.startIndex || sourceSlot >= oldRange.startIndex + oldRange.count) return false;

        if (change.oldChunkCoordinate == change.newChunkCoordinate)
        {
            WriteInstanceSlot(group, sourceSlot, change.instance);
            MarkIncrementalBoundsDirty(group, oldRange);
            incrementalSlotUpdateCount++;
            return true;
        }

        if (!group.chunkRangeLookup.TryGetValue(change.newChunkCoordinate, out ChunkRange newRange)) return false;
        if (newRange.freeSlots.Count == 0) return false;

        int destinationSlot = newRange.freeSlots.Pop();

        ClearInstanceSlot(group, sourceSlot);
        oldRange.freeSlots.Push(sourceSlot);
        oldRange.activeCount = Mathf.Max(0, oldRange.activeCount - 1);

        WriteInstanceSlot(group, destinationSlot, change.instance);
        group.slotByPersistentID[change.instance.persistentID] = destinationSlot;
        newRange.activeCount++;

        MarkIncrementalBoundsDirty(group, oldRange);
        MarkIncrementalBoundsDirty(group, newRange);
        incrementalSlotUpdateCount += 2;
        return true;
    }

    private void WriteInstanceSlot(RenderGroup group, int slot, VegetationInstance instance)
    {
        if (group == null || slot < 0 || slot >= group.instanceCount) return;

        Matrix4x4 matrix = instance.LocalToWorldMatrix;
        Bounds worldBounds = CalculateWorldBounds(
            group.species.GetLocalMeshBounds(),
            matrix,
            group.species.horizontalBoundsPadding,
            group.species.verticalBoundsPadding
        );

        float radius = Mathf.Max(worldBounds.extents.magnitude, 0.01f);

        singleMatrixUpload[0] = matrix;
        singleBoundsUpload[0] = new Vector4(worldBounds.center.x, worldBounds.center.y, worldBounds.center.z, radius);
        singlePersistentIDUpload[0] = unchecked((uint)instance.persistentID);

        group.matrixBuffer.SetData(singleMatrixUpload, 0, slot, 1);
        group.boundsBuffer.SetData(singleBoundsUpload, 0, slot, 1);
        group.persistentIDBuffer.SetData(singlePersistentIDUpload, 0, slot, 1);
    }

    private void ClearInstanceSlot(RenderGroup group, int slot)
    {
        if (group == null || slot < 0 || slot >= group.instanceCount) return;

        singlePersistentIDUpload[0] = 0u;
        group.persistentIDBuffer.SetData(singlePersistentIDUpload, 0, slot, 1);
    }

    private void MarkIncrementalBoundsDirty(RenderGroup group, ChunkRange range)
    {
        if (group == null || range == null) return;

        group.dirtyChunkRanges.Add(range);
        incrementalDirtyGroups.Add(group);
    }

    private void FlushIncrementalBoundsUpdates()
    {
        if (incrementalDirtyGroups.Count == 0) return;

        foreach (RenderGroup group in incrementalDirtyGroups)
        {
            if (group == null) continue;

            foreach (ChunkRange range in group.dirtyChunkRanges)
            {
                RefreshChunkRangeBounds(group, range);
            }

            group.dirtyChunkRanges.Clear();
            RecalculateGroupDrawBounds(group);
        }

        incrementalDirtyGroups.Clear();
    }

    private void RefreshChunkRangeBounds(RenderGroup group, ChunkRange range)
    {
        if (group == null || range == null || database == null) return;

        VegetationChunkData chunk = database.GetChunk(range.coordinate);
        bool hasBounds = false;
        Bounds bounds = default;
        Bounds localBounds = group.species.GetLocalMeshBounds();

        if (chunk != null && chunk.instances != null)
        {
            for (int i = 0; i < chunk.instances.Count; i++)
            {
                VegetationInstance instance = chunk.instances[i];
                if (instance.speciesIndex != group.speciesIndex) continue;

                Bounds instanceBounds = CalculateWorldBounds(
                    localBounds,
                    instance.LocalToWorldMatrix,
                    group.species.horizontalBoundsPadding,
                    group.species.verticalBoundsPadding
                );

                if (!hasBounds)
                {
                    bounds = instanceBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(instanceBounds);
                }
            }
        }

        if (hasBounds)
        {
            range.bounds = bounds;
            return;
        }

        Vector3 center = new Vector3(
            (range.coordinate.x + 0.5f) * database.chunkSize,
            0f,
            (range.coordinate.y + 0.5f) * database.chunkSize
        );

        range.bounds = new Bounds(center, new Vector3(database.chunkSize, 1f, database.chunkSize));
    }

    private static void RecalculateGroupDrawBounds(RenderGroup group)
    {
        if (group == null) return;

        bool initialized = false;
        Bounds drawBounds = default;

        for (int i = 0; i < group.chunkRanges.Count; i++)
        {
            ChunkRange range = group.chunkRanges[i];
            if (range.activeCount <= 0) continue;

            if (!initialized)
            {
                drawBounds = range.bounds;
                initialized = true;
            }
            else
            {
                drawBounds.Encapsulate(range.bounds);
            }
        }

        group.drawBounds = initialized ? drawBounds : new Bounds(Vector3.zero, Vector3.one);
    }

    private void RebuildSpeciesGroup(int speciesIndex)
    {
        using (SpeciesRebuildProfilerMarker.Auto())
        {
            RemoveRenderGroup(speciesIndex);

            BuildData build = BuildSpeciesData(speciesIndex);
            if (build != null && build.instances.Count > 0) CreateRenderGroup(build);
        }
    }

    private BuildData BuildSpeciesData(int speciesIndex)
    {
        if (
            database == null ||
            database.species == null ||
            database.chunks == null ||
            speciesIndex < 0 ||
            speciesIndex >= database.species.Count
        )
        {
            return null;
        }

        VegetationSpecies species = database.species[speciesIndex];
        if (species == null || species.lod0 == null || !species.lod0.IsValid) return null;

        BuildData build = new BuildData
        {
            speciesIndex = speciesIndex,
            species = species
        };

        List<VegetationInstance> chunkInstances = new List<VegetationInstance>();

        for (int chunkIndex = 0; chunkIndex < database.chunks.Count; chunkIndex++)
        {
            VegetationChunkData chunk = database.chunks[chunkIndex];
            if (chunk == null || chunk.instances == null || chunk.instances.Count == 0) continue;

            chunkInstances.Clear();

            for (int instanceIndex = 0; instanceIndex < chunk.instances.Count; instanceIndex++)
            {
                VegetationInstance instance = chunk.instances[instanceIndex];
                if (instance.speciesIndex == speciesIndex) chunkInstances.Add(instance);
            }

            if (chunkInstances.Count == 0) continue;

            int startIndex = build.instances.Count;
            build.instances.AddRange(chunkInstances);
            build.chunkRanges.Add(new ChunkRange
            {
                coordinate = chunk.coordinate,
                startIndex = startIndex,
                count = chunkInstances.Count,
                activeCount = chunkInstances.Count,
                bounds = CalculateBounds(species, chunkInstances),
                visibleLastFrame = true
            });
        }

        return build;
    }

    private void RemoveRenderGroup(int speciesIndex)
    {
        if (!renderGroupBySpeciesIndex.TryGetValue(speciesIndex, out RenderGroup group)) return;

        renderGroupBySpeciesIndex.Remove(speciesIndex);
        renderGroups.Remove(group);
        incrementalDirtyGroups.Remove(group);
        group.Release();
    }

    private void RefreshAggregateStats()
    {
        uploadedInstanceCount = database != null ? database.TotalInstanceCount : 0;
        drawCallCount = 0;
        chunkRangeCount = 0;

        for (int i = 0; i < renderGroups.Count; i++)
        {
            RenderGroup group = renderGroups[i];
            chunkRangeCount += group.chunkRanges.Count;
            drawCallCount += group.lod0 != null ? group.lod0.parts.Count : 0;
            drawCallCount += group.lod1 != null ? group.lod1.parts.Count : 0;
            drawCallCount += group.lod2 != null ? group.lod2.parts.Count : 0;
            drawCallCount += group.lod3 != null ? group.lod3.parts.Count : 0;
        }
    }

    private int GetReserveSlotCount(int activeCount)
    {
        if (!enableIncrementalUpdates) return 0;

        int fixedReserve = Mathf.Max(0, incrementalReserveSlotsPerChunk);
        int ratioReserve = Mathf.CeilToInt(Mathf.Max(0, activeCount) * Mathf.Clamp01(incrementalReserveRatio));
        return Mathf.Max(fixedReserve, ratioReserve);
    }

    private void CreateRenderGroup(BuildData build)
    {
        VegetationSpecies species = build.species;
        List<VegetationInstance> instances = build.instances;
        Bounds localMeshBounds = species.GetLocalMeshBounds();

        int totalCapacity = 0;
        for (int i = 0; i < build.chunkRanges.Count; i++) totalCapacity += build.chunkRanges[i].count + GetReserveSlotCount(build.chunkRanges[i].count);
        totalCapacity = Mathf.Max(totalCapacity, instances.Count);

        Matrix4x4[] matrices = new Matrix4x4[totalCapacity];
        Vector4[] boundsData = new Vector4[totalCapacity];
        uint[] persistentIDs = new uint[totalCapacity];

        Matrix4x4 inactiveMatrix = Matrix4x4.TRS(
            new Vector3(0f, -1000000f, 0f),
            Quaternion.identity,
            Vector3.one * 0.0001f
        );

        for (int i = 0; i < totalCapacity; i++)
        {
            matrices[i] = inactiveMatrix;
            boundsData[i] = new Vector4(0f, -1000000f, 0f, -1f);
            persistentIDs[i] = 0u;
        }

        int destinationCursor = 0;

        for (int rangeIndex = 0; rangeIndex < build.chunkRanges.Count; rangeIndex++)
        {
            ChunkRange range = build.chunkRanges[rangeIndex];
            int sourceStart = range.startIndex;
            int activeCount = range.count;
            int capacity = activeCount + GetReserveSlotCount(activeCount);

            range.startIndex = destinationCursor;
            range.activeCount = activeCount;
            range.count = capacity;

            for (int localIndex = 0; localIndex < activeCount; localIndex++)
            {
                VegetationInstance instance = instances[sourceStart + localIndex];
                Matrix4x4 matrix = instance.LocalToWorldMatrix;
                int destinationIndex = destinationCursor + localIndex;

                matrices[destinationIndex] = matrix;
                persistentIDs[destinationIndex] = unchecked((uint)instance.persistentID);

                Bounds worldBounds = CalculateWorldBounds(
                    localMeshBounds,
                    matrix,
                    species.horizontalBoundsPadding,
                    species.verticalBoundsPadding
                );
                float radius = Mathf.Max(worldBounds.extents.magnitude, 0.01f);

                boundsData[destinationIndex] = new Vector4(
                    worldBounds.center.x,
                    worldBounds.center.y,
                    worldBounds.center.z,
                    radius
                );
            }

            destinationCursor += capacity;
        }

        ComputeBuffer matrixBuffer = new ComputeBuffer(totalCapacity, sizeof(float) * 16, ComputeBufferType.Structured);
        matrixBuffer.SetData(matrices);

        ComputeBuffer boundsBuffer = new ComputeBuffer(totalCapacity, sizeof(float) * 4, ComputeBufferType.Structured);
        boundsBuffer.SetData(boundsData);

        ComputeBuffer persistentIDBuffer = new ComputeBuffer(totalCapacity, sizeof(uint), ComputeBufferType.Structured);
        persistentIDBuffer.SetData(persistentIDs);

        Bounds drawBounds = build.chunkRanges[0].bounds;

        for (int i = 1; i < build.chunkRanges.Count; i++)
        {
            drawBounds.Encapsulate(build.chunkRanges[i].bounds);
        }

        RenderGroup group = new RenderGroup
        {
            speciesIndex = build.speciesIndex,
            species = species,
            matrixBuffer = matrixBuffer,
            boundsBuffer = boundsBuffer,
            persistentIDBuffer = persistentIDBuffer,
            drawBounds = drawBounds,
            instanceCount = totalCapacity,
            activeInstanceCount = instances.Count
        };

        group.chunkRanges.AddRange(build.chunkRanges);

        for (int rangeIndex = 0; rangeIndex < group.chunkRanges.Count; rangeIndex++)
        {
            ChunkRange range = group.chunkRanges[rangeIndex];
            group.chunkRangeLookup[range.coordinate] = range;

            int endIndex = range.startIndex + range.count;
            for (int slot = range.startIndex; slot < endIndex; slot++)
            {
                uint persistentID = persistentIDs[slot];

                if (persistentID != 0u)
                {
                    group.slotByPersistentID[unchecked((int)persistentID)] = slot;
                }
                else
                {
                    range.freeSlots.Push(slot);
                }
            }
        }

        group.lod0 = CreateLODRenderData(species.lod0, matrixBuffer, totalCapacity, true);
        group.lod1 = CreateLODRenderData(species.lod1, matrixBuffer, totalCapacity, false);
        group.lod2 = CreateLODRenderData(species.lod2, matrixBuffer, totalCapacity, false);
        group.lod3 = CreateLODRenderData(species.lod3, matrixBuffer, totalCapacity, false);

        group.dummyLOD1Buffer = new ComputeBuffer(totalCapacity, sizeof(uint), ComputeBufferType.Append);
        group.dummyLOD2Buffer = new ComputeBuffer(totalCapacity, sizeof(uint), ComputeBufferType.Append);
        group.dummyLOD3Buffer = new ComputeBuffer(totalCapacity, sizeof(uint), ComputeBufferType.Append);

        group.dummyLOD1Buffer.SetCounterValue(0);
        group.dummyLOD2Buffer.SetCounterValue(0);
        group.dummyLOD3Buffer.SetCounterValue(0);

        if (group.lod0 == null || !group.lod0.IsValid)
        {
            group.Release();
            return;
        }

        group.diagnosticResourceKey = $"Renderer={GetInstanceID()}/ForwardGroup={renderGroups.Count}";
        renderGroups.Add(group);
        renderGroupBySpeciesIndex[group.speciesIndex] = group;

        uploadedInstanceCount += instances.Count;
        chunkRangeCount += group.chunkRanges.Count;

        drawCallCount += group.lod0 != null ? group.lod0.parts.Count : 0;
        drawCallCount += group.lod1 != null ? group.lod1.parts.Count : 0;
        drawCallCount += group.lod2 != null ? group.lod2.parts.Count : 0;
        drawCallCount += group.lod3 != null ? group.lod3.parts.Count : 0;
    }

    private LODRenderData CreateLODRenderData(VegetationLODAsset asset, ComputeBuffer matrixBuffer, int instanceCount, bool isLOD0)
    {
        if (asset == null || !asset.IsValid) return null;

        Mesh mesh = asset.mesh;

        ComputeBuffer visibleIndexBuffer = new ComputeBuffer(instanceCount, sizeof(uint), ComputeBufferType.Append);
        visibleIndexBuffer.SetCounterValue(0);

        MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        propertyBlock.SetBuffer(VegetationMatricesID, matrixBuffer);
        propertyBlock.SetBuffer(VegetationVisibleIndicesID, visibleIndexBuffer);
        propertyBlock.SetInt(VegetationUseVisibleIndicesID, 1);

        LODRenderData lodData = new LODRenderData
        {
            mesh = mesh,
            asset = asset,
            visibleIndexBuffer = visibleIndexBuffer,
            propertyBlock = propertyBlock
        };

        int usableSubMeshCount = Mathf.Min(mesh.subMeshCount, asset.materials.Length);

        if (mesh.subMeshCount != asset.materials.Length)
        {
            Debug.LogWarning($"Mesh '{mesh.name}' 的SubMesh数量({mesh.subMeshCount})和材质数量({asset.materials.Length})不一致。", mesh);
        }

        for (int subMeshIndex = 0; subMeshIndex < usableSubMeshCount; subMeshIndex++)
        {
            Material material = asset.materials[subMeshIndex];

            if (material == null) continue;

            material.enableInstancing = true;

            uint[] args =
            {
                mesh.GetIndexCount(subMeshIndex),
                isLOD0 ? (uint)instanceCount : 0u,
                mesh.GetIndexStart(subMeshIndex),
                mesh.GetBaseVertex(subMeshIndex),
                0
            };

            ComputeBuffer argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
            argsBuffer.SetData(args);

            lodData.parts.Add(new RenderPart
            {
                subMeshIndex = subMeshIndex,
                material = material,
                argsBuffer = argsBuffer,
                fullArgs = args
            });
        }

        if (lodData.parts.Count == 0)
        {
            lodData.Release();
            return null;
        }

        return lodData;
    }

    private CameraShadowState GetOrCreateShadowState(RenderGroup group, Camera camera)
    {
        if (group == null || camera == null || group.species == null || !group.species.castShadows) return null;

        int cameraID = camera.GetInstanceID();

        if (group.shadowStates.TryGetValue(cameraID, out CameraShadowState existingState))
        {
            if (existingState.camera == camera) return existingState;

            existingState.Release();
            group.shadowStates.Remove(cameraID);
        }

        ReleaseStaleShadowStates(group);

        CameraShadowState state = new CameraShadowState
        {
            camera = camera,
            lod0 = CreateShadowLODRenderData(group, group.lod0),
            lod1 = CreateShadowLODRenderData(group, group.lod1),
            lod2 = CreateShadowLODRenderData(group, group.lod2),
            lod3 = CreateShadowLODRenderData(group, group.lod3)
        };

        if (state.lod1 == null) state.dummyLOD1Buffer = CreateAppendBuffer(group.instanceCount);
        if (state.lod2 == null) state.dummyLOD2Buffer = CreateAppendBuffer(group.instanceCount);
        if (state.lod3 == null) state.dummyLOD3Buffer = CreateAppendBuffer(group.instanceCount);

        group.shadowStates.Add(cameraID, state);
        return state;
    }

    private static void ReleaseStaleShadowStates(RenderGroup group)
    {
        group.staleShadowStateIDs.Clear();

        foreach (KeyValuePair<int, CameraShadowState> pair in group.shadowStates)
        {
            if (pair.Value != null && pair.Value.camera != null) continue;

            pair.Value?.Release();
            group.staleShadowStateIDs.Add(pair.Key);
        }

        for (int i = 0; i < group.staleShadowStateIDs.Count; i++)
        {
            group.shadowStates.Remove(group.staleShadowStateIDs[i]);
        }

        group.staleShadowStateIDs.Clear();
    }

    private static ShadowLODRenderData CreateShadowLODRenderData(RenderGroup group, LODRenderData sourceLOD)
    {
        if (group == null || sourceLOD == null || !sourceLOD.IsValid) return null;

        ShadowLODRenderData shadowLOD = new ShadowLODRenderData
        {
            visibleIndexBuffer = CreateAppendBuffer(group.instanceCount),
            propertyBlock = new MaterialPropertyBlock()
        };

        shadowLOD.propertyBlock.SetBuffer(VegetationMatricesID, group.matrixBuffer);
        shadowLOD.propertyBlock.SetBuffer(VegetationVisibleIndicesID, shadowLOD.visibleIndexBuffer);
        shadowLOD.propertyBlock.SetInt(VegetationUseVisibleIndicesID, 1);

        for (int partIndex = 0; partIndex < sourceLOD.parts.Count; partIndex++)
        {
            shadowLOD.parts.Add(new ShadowRenderPart(sourceLOD.parts[partIndex].fullArgs));
        }

        return shadowLOD;
    }

    private static ComputeBuffer CreateAppendBuffer(int instanceCount)
    {
        ComputeBuffer buffer = new ComputeBuffer(Mathf.Max(instanceCount, 1), sizeof(uint), ComputeBufferType.Append);
        buffer.SetCounterValue(0);
        return buffer;
    }

    private static CameraShadowState GetShadowState(RenderGroup group, Camera camera)
    {
        if (group == null || camera == null) return null;

        group.shadowStates.TryGetValue(camera.GetInstanceID(), out CameraShadowState state);
        return state != null && state.camera == camera ? state : null;
    }

    public bool ShouldRenderForCamera(Camera camera)
    {
        if (camera == null) return false;
        if ((camera.cullingMask & (1 << gameObject.layer)) == 0) return false;

#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView) return true;
#endif

        if (targetCamera != null) return camera == targetCamera;

        return camera.cameraType == CameraType.Game;
    }

    public bool ShouldRenderForwardForCamera(Camera camera)
    {
        if (!ShouldRenderForCamera(camera)) return false;
        if (disableForwardRendering) return false;
        if (disableSceneForward && camera.cameraType == CameraType.SceneView) return false;
        return !forceGameCameraOnly || camera.cameraType == CameraType.Game;
    }

    private bool ShouldRenderShadowsForCamera(Camera camera)
    {
        if (!ShouldRenderForCamera(camera)) return false;
        if (disableShadowRendering) return false;
        if (disableSceneShadow && camera.cameraType == CameraType.SceneView) return false;

#if UNITY_EDITOR
        if (camera.cameraType == CameraType.SceneView && !renderSceneViewShadows) return false;
        if (camera.cameraType == CameraType.SceneView && Application.isPlaying && !renderSceneViewShadowsDuringPlay) return false;
#endif

        return true;
    }

    public void PrepareForCamera(CommandBuffer cmd, Camera renderCamera, long passID, int additionalLightsCount)
    {
        ForwardPrepareProfilerMarker.Begin();
        try
        {
        preparedWithCulling = false;

        if (cmd == null || renderCamera == null) return;

        BeginRuntimeStatsFrame();
        currentCamera = renderCamera;
        forwardCameraIDs.Add(renderCamera.GetInstanceID());

        EnsureBuffersUpToDate();

        if (database == null || renderGroups.Count == 0) return;

        bool canUseCulling = !disableGPUCulling && enableGPUCulling && cullingCompute != null &&
                             (Application.isPlaying || enableCullingInEditMode);

        if (!canUseCulling || !PrepareComputeShader())
        {
            using (ForwardCullingProfilerMarker.Auto()) PrepareWithoutCulling(cmd, renderCamera, passID, additionalLightsCount);
            return;
        }

        preparedWithCulling = true;

        Camera cullingCamera = ResolveCullingCamera(renderCamera);
        if (cullingCamera == null) cullingCamera = renderCamera;

        Transform cameraTransform = cullingCamera.transform;
        Vector3 cameraPosition = cameraTransform.position;

        GeometryUtility.CalculateFrustumPlanes(cullingCamera, frustumPlanes);

        cmd.SetComputeVectorParam(cullingCompute, CameraPositionWSID, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 0f));
        cmd.SetComputeVectorParam(cullingCompute, CameraForwardWSID, new Vector4(cameraTransform.forward.x, cameraTransform.forward.y, cameraTransform.forward.z, 0f));
        cmd.SetComputeVectorParam(cullingCompute, CameraRightWSID, new Vector4(cameraTransform.right.x, cameraTransform.right.y, cameraTransform.right.z, 0f));
        cmd.SetComputeVectorParam(cullingCompute, CameraUpWSID, new Vector4(cameraTransform.up.x, cameraTransform.up.y, cameraTransform.up.z, 0f));

        cmd.SetComputeFloatParam(cullingCompute, AspectID, Mathf.Max(cullingCamera.aspect, 0.001f));
        cmd.SetComputeFloatParam(cullingCompute, NearClipID, cullingCamera.nearClipPlane);
        cmd.SetComputeFloatParam(cullingCompute, TanHalfFovID, Mathf.Tan(cullingCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
        cmd.SetComputeFloatParam(cullingCompute, OrthographicSizeID, cullingCamera.orthographicSize);

        cmd.SetComputeIntParam(cullingCompute, IsOrthographicID, cullingCamera.orthographic ? 1 : 0);
        cmd.SetComputeIntParam(cullingCompute, EnableFrustumCullingID, enableFrustumCulling ? 1 : 0);
        cmd.SetComputeIntParam(cullingCompute, EnableShadowInfluenceCullingID, 0);
        cmd.SetComputeIntParam(cullingCompute, LODOffsetID, 0);

        visibleChunkRangeCount = 0;
        cullingDispatchCount = 0;

        VegetationDiagnostics.KernelMetadata(renderCamera, diagnosticsMode, diagnosticsVerboseInterval,
            "CSCullOnly", cullOnlyThreadX, cullOnlyThreadY, cullOnlyThreadZ, true);
        VegetationDiagnostics.KernelMetadata(renderCamera, diagnosticsMode, diagnosticsVerboseInterval,
            "CSCullLOD", cullLODThreadX, cullLODThreadY, cullLODThreadZ, true);

        using (ForwardCullingProfilerMarker.Auto())
        for (int groupIndex = 0; groupIndex < renderGroups.Count; groupIndex++)
        {
            RenderGroup group = renderGroups[groupIndex];
            VegetationSpecies species = group.species;
            string speciesName = GetSpeciesName(species, groupIndex);

            VegetationDiagnostics.ForwardPrepareStarted(
                renderCamera,
                speciesName,
                group.instanceCount,
                passID,
                group.diagnosticForwardPreparePassID
            );
            group.diagnosticForwardPreparePassID = passID;

            string resourceKey = GetForwardResourceKey(group, groupIndex);
            cmd.BeginSample("Vegetation Forward Cull");
            ResetVisibleBuffers(cmd, group, renderCamera, passID, resourceKey, speciesName);

            float cullDistance = Mathf.Max(species.cullDistance, 0.01f);
            float lod0Distance = enableGPULOD ? Mathf.Clamp(species.lod0Distance, 0f, cullDistance) : cullDistance;
            float lod1Distance = enableGPULOD ? Mathf.Clamp(species.lod1Distance, lod0Distance, cullDistance) : cullDistance;
            float lod2Distance = enableGPULOD ? Mathf.Clamp(species.lod2Distance, lod1Distance, cullDistance) : cullDistance;

            bool useDensityLOD = enableGPULOD && species.vegetationType == VegetationType.Grass;
            bool hasLOD1 = enableGPULOD && group.lod1 != null && group.lod1.IsValid;
            bool hasLOD2 = enableGPULOD && group.lod2 != null && group.lod2.IsValid;
            bool hasLOD3 = enableGPULOD && group.lod3 != null && group.lod3.IsValid;

            group.forwardCrossFade = BuildLODCrossFadeData(species, cameraPosition, lod0Distance, lod1Distance,
                lod2Distance, cullDistance, 0, hasLOD1, hasLOD2, hasLOD3, useDensityLOD);
            FilterUnsupportedCrossFades(species, ref group.forwardCrossFade, hasLOD1, hasLOD2, hasLOD3);

            cmd.SetComputeFloatParam(cullingCompute, LOD0DistanceID, lod0Distance);
            cmd.SetComputeFloatParam(cullingCompute, LOD1DistanceID, lod1Distance);
            cmd.SetComputeFloatParam(cullingCompute, LOD2DistanceID, lod2Distance);
            cmd.SetComputeFloatParam(cullingCompute, CullDistanceID, cullDistance);
            cmd.SetComputeVectorParam(cullingCompute, LODCrossFadeWidthsID, group.forwardCrossFade.widths);

            cmd.SetComputeFloatParam(cullingCompute, MidDensityID, Mathf.Clamp01(species.grassMidDensity));
            cmd.SetComputeFloatParam(cullingCompute, FarDensityID, Mathf.Clamp01(species.grassFarDensity));

            cmd.SetComputeIntParam(cullingCompute, UseDensityLODID, useDensityLOD ? 1 : 0);
            cmd.SetComputeIntParam(cullingCompute, HasLOD1ID, hasLOD1 ? 1 : 0);
            cmd.SetComputeIntParam(cullingCompute, HasLOD2ID, hasLOD2 ? 1 : 0);
            cmd.SetComputeIntParam(cullingCompute, HasLOD3ID, hasLOD3 ? 1 : 0);

            int activeKernel = enableGPULOD ? cullLODKernel : cullOnlyKernel;
            GetKernelMetadata(activeKernel, out string kernelName, out uint threadX, out uint threadY, out uint threadZ);
            cmd.SetComputeBufferParam(cullingCompute, activeKernel, SourceBoundsID, group.boundsBuffer);
            cmd.SetComputeBufferParam(cullingCompute, activeKernel, VisibleLOD0ID, group.lod0.visibleIndexBuffer);
            cmd.SetComputeBufferParam(cullingCompute, activeKernel, PersistentIDsID, group.persistentIDBuffer);

            if (enableGPULOD)
            {
                cmd.SetComputeBufferParam(cullingCompute, activeKernel, SourceMatricesID, group.matrixBuffer);

                ComputeBuffer lod1Buffer = group.lod1 != null ? group.lod1.visibleIndexBuffer : group.dummyLOD1Buffer;
                ComputeBuffer lod2Buffer = group.lod2 != null ? group.lod2.visibleIndexBuffer : group.dummyLOD2Buffer;
                ComputeBuffer lod3Buffer = group.lod3 != null ? group.lod3.visibleIndexBuffer : group.dummyLOD3Buffer;

                cmd.SetComputeBufferParam(cullingCompute, activeKernel, VisibleLOD1ID, lod1Buffer);
                cmd.SetComputeBufferParam(cullingCompute, activeKernel, VisibleLOD2ID, lod2Buffer);
                cmd.SetComputeBufferParam(cullingCompute, activeKernel, VisibleLOD3ID, lod3Buffer);
            }

            for (int rangeIndex = 0; rangeIndex < group.chunkRanges.Count; rangeIndex++)
                group.chunkRanges[rangeIndex].visibleLastFrame = false;

            VegetationDispatchStats dispatchStats = default;

            if (enableChunkCulling)
            {
                CullAndDispatchChunkRanges(cmd, group, activeKernel, cameraPosition, cullDistance, renderCamera,
                    speciesName, resourceKey, passID, kernelName, threadX, threadY, threadZ, ref dispatchStats);
            }
            else
            {
                for (int rangeIndex = 0; rangeIndex < group.chunkRanges.Count; rangeIndex++)
                    group.chunkRanges[rangeIndex].visibleLastFrame = true;

                dispatchStats.selectedChunkCount = group.chunkRanges.Count;
                DispatchRange(cmd, group, activeKernel, 0, group.instanceCount, renderCamera, speciesName,
                    resourceKey, passID, kernelName, threadX, threadY, threadZ, ref dispatchStats);
                visibleChunkRangeCount += group.chunkRanges.Count;
            }

            VegetationDiagnostics.ValidateGroupDispatchTotals(
                passID,
                renderCamera,
                speciesName,
                group.instanceCount,
                enableChunkCulling,
                dispatchStats
            );
            VegetationDiagnostics.RecordForwardPrepare(passID, group.instanceCount, dispatchStats.dispatchCallCount);
            currentForwardDispatchCount += dispatchStats.dispatchCallCount;

            cmd.EndSample("Vegetation Forward Cull");
            cmd.BeginSample("Vegetation Forward CopyCounter");

            PrepareLODForCamera(cmd, group, group.lod0, renderCamera, additionalLightsCount);

            if (hasLOD1) PrepareLODForCamera(cmd, group, group.lod1, renderCamera, additionalLightsCount);
            if (hasLOD2) PrepareLODForCamera(cmd, group, group.lod2, renderCamera, additionalLightsCount);
            if (hasLOD3) PrepareLODForCamera(cmd, group, group.lod3, renderCamera, additionalLightsCount);

            VegetationDiagnostics.GpuCommand(passID, renderCamera, resourceKey, speciesName, "CopyCounter",
                diagnosticsMode, diagnosticsVerboseInterval);

            ScheduleForwardReadback(cmd, renderCamera, group, speciesName, dispatchStats, kernelName,
                threadX, threadY, threadZ, true, passID);
            cmd.EndSample("Vegetation Forward CopyCounter");
        }
        }
        finally
        {
            ForwardPrepareProfilerMarker.End();
        }
    }
    private Camera ResolveCullingCamera(Camera renderCamera)
    {
        if (renderCamera == null) return targetCamera;

#if UNITY_EDITOR
        if (renderCamera.cameraType == CameraType.SceneView && sceneViewUseTargetCameraForCulling && targetCamera != null) return targetCamera;
#endif

        return renderCamera;
    }

    public void RenderForward(CommandBuffer cmd, Camera camera, long passID)
    {
        using (ForwardDrawCpuProfilerMarker.Auto())
        {
        if (cmd == null || camera == null) return;

        for (int groupIndex = 0; groupIndex < renderGroups.Count; groupIndex++)
        {
            RenderGroup group = renderGroups[groupIndex];

            VegetationDiagnostics.GpuCommand(passID, camera, GetForwardResourceKey(group, groupIndex),
                GetSpeciesName(group.species, groupIndex), "Draw", diagnosticsMode, diagnosticsVerboseInterval);

            int submittedDraws = DrawLODForward(cmd, group, group.lod0, 0);

            if (preparedWithCulling)
            {
                if (enableGPULOD && group.lod1 != null && group.lod1.IsValid) submittedDraws += DrawLODForward(cmd, group, group.lod1, 1);
                if (enableGPULOD && group.lod2 != null && group.lod2.IsValid) submittedDraws += DrawLODForward(cmd, group, group.lod2, 2);
                if (enableGPULOD && group.lod3 != null && group.lod3.IsValid) submittedDraws += DrawLODForward(cmd, group, group.lod3, 3);
            }

            VegetationDiagnostics.RecordForwardDraw(passID, submittedDraws);
            currentForwardDrawCount += submittedDraws;
        }
        }
    }

    public int RenderDepthNormals(CommandBuffer cmd, Camera camera)
    {
        if (cmd == null || camera == null) return 0;
        int submittedDraws = 0;
        for (int groupIndex = 0; groupIndex < renderGroups.Count; groupIndex++)
        {
            RenderGroup group = renderGroups[groupIndex];
            submittedDraws += DrawLODForward(cmd, group, group.lod0, 0, true);
            if (!preparedWithCulling) continue;
            if (enableGPULOD && group.lod1 != null && group.lod1.IsValid) submittedDraws += DrawLODForward(cmd, group, group.lod1, 1, true);
            if (enableGPULOD && group.lod2 != null && group.lod2.IsValid) submittedDraws += DrawLODForward(cmd, group, group.lod2, 2, true);
            if (enableGPULOD && group.lod3 != null && group.lod3.IsValid) submittedDraws += DrawLODForward(cmd, group, group.lod3, 3, true);
        }
        return submittedDraws;
    }

    private void CullAndDispatchChunkRanges(
        CommandBuffer cmd,
        RenderGroup group,
        int kernel,
        Vector3 cameraPosition,
        float cullDistance,
        Camera camera,
        string speciesName,
        string resourceKey,
        long passID,
        string kernelName,
        uint threadX,
        uint threadY,
        uint threadZ,
        ref VegetationDispatchStats dispatchStats)
    {
        int mergedStartIndex = -1;
        int mergedCount = 0;
        float cullDistanceSqr = cullDistance * cullDistance;

        for (int rangeIndex = 0; rangeIndex < group.chunkRanges.Count; rangeIndex++)
        {
            ChunkRange range = group.chunkRanges[rangeIndex];

            if (range.activeCount <= 0)
            {
                range.visibleLastFrame = false;

                if (mergedCount > 0)
                {
                    DispatchRange(cmd, group, kernel, mergedStartIndex, mergedCount, camera, speciesName,
                        resourceKey, passID, kernelName, threadX, threadY, threadZ, ref dispatchStats);
                    mergedStartIndex = -1;
                    mergedCount = 0;
                }

                continue;
            }

            bool visible = range.bounds.SqrDistance(cameraPosition) <= cullDistanceSqr;

            if (visible && enableFrustumCulling)
            {
                visible = GeometryUtility.TestPlanesAABB(frustumPlanes, range.bounds);
            }

            range.visibleLastFrame = visible;

            if (!visible)
            {
                if (mergedCount > 0)
                {
                    DispatchRange(cmd, group, kernel, mergedStartIndex, mergedCount, camera, speciesName,
                        resourceKey, passID, kernelName, threadX, threadY, threadZ, ref dispatchStats);

                    mergedStartIndex = -1;
                    mergedCount = 0;
                }

                continue;
            }

            visibleChunkRangeCount++;
            dispatchStats.selectedChunkCount++;

            if (mergedCount == 0)
            {
                mergedStartIndex = range.startIndex;
                mergedCount = range.count;
                continue;
            }

            int mergedEndIndex = mergedStartIndex + mergedCount;

            if (range.startIndex == mergedEndIndex)
            {
                mergedCount += range.count;
            }
            else
            {
                DispatchRange(cmd, group, kernel, mergedStartIndex, mergedCount, camera, speciesName,
                    resourceKey, passID, kernelName, threadX, threadY, threadZ, ref dispatchStats);

                mergedStartIndex = range.startIndex;
                mergedCount = range.count;
            }
        }

        if (mergedCount > 0)
        {
            DispatchRange(cmd, group, kernel, mergedStartIndex, mergedCount, camera, speciesName,
                resourceKey, passID, kernelName, threadX, threadY, threadZ, ref dispatchStats);
        }
    }

    private void ResetVisibleBuffers(
        CommandBuffer cmd,
        RenderGroup group,
        Camera camera,
        long passID,
        string resourceKey,
        string speciesName)
    {
        if (group.lod0 != null) cmd.SetBufferCounterValue(group.lod0.visibleIndexBuffer, 0);
        if (group.lod1 != null) cmd.SetBufferCounterValue(group.lod1.visibleIndexBuffer, 0);
        if (group.lod2 != null) cmd.SetBufferCounterValue(group.lod2.visibleIndexBuffer, 0);
        if (group.lod3 != null) cmd.SetBufferCounterValue(group.lod3.visibleIndexBuffer, 0);

        if (group.dummyLOD1Buffer != null) cmd.SetBufferCounterValue(group.dummyLOD1Buffer, 0);
        if (group.dummyLOD2Buffer != null) cmd.SetBufferCounterValue(group.dummyLOD2Buffer, 0);
        if (group.dummyLOD3Buffer != null) cmd.SetBufferCounterValue(group.dummyLOD3Buffer, 0);

        group.diagnosticForwardResetFrame = Time.frameCount;
        group.diagnosticForwardResetCameraID = camera != null ? camera.GetInstanceID() : 0;
        group.diagnosticForwardResetPassID = passID;

        VegetationDiagnostics.GpuCommand(passID, camera, resourceKey, speciesName, "Reset",
            diagnosticsMode, diagnosticsVerboseInterval);
    }

    private void PrepareLODForCamera(CommandBuffer cmd, RenderGroup group, LODRenderData lod, Camera camera, int additionalLightsCount)
    {
        if (lod == null || !lod.IsValid) return;

        lod.propertyBlock.SetBuffer(VegetationMatricesID, group.matrixBuffer);
        lod.propertyBlock.SetBuffer(VegetationVisibleIndicesID, lod.visibleIndexBuffer);
        lod.propertyBlock.SetInt(VegetationUseVisibleIndicesID, 1);

        SetCameraVectors(lod.propertyBlock, camera);
        SetAmbientProbe(lod.propertyBlock);
        lod.propertyBlock.SetInt(VegetationAdditionalLightsCountID, Mathf.Max(0, additionalLightsCount));
        lod.propertyBlock.SetInt(VegetationReceiveShadowsID, group.species != null && group.species.receiveShadows ? 1 : 0);

        for (int partIndex = 0; partIndex < lod.parts.Count; partIndex++)
        {
            RenderPart part = lod.parts[partIndex];

            cmd.CopyCounterValue(lod.visibleIndexBuffer, part.argsBuffer, sizeof(uint));
        }
    }

    private void PrepareWithoutCulling(CommandBuffer cmd, Camera camera, long passID, int additionalLightsCount)
    {
        visibleChunkRangeCount = chunkRangeCount;
        cullingDispatchCount = 0;

        for (int groupIndex = 0; groupIndex < renderGroups.Count; groupIndex++)
        {
            RenderGroup group = renderGroups[groupIndex];
            group.forwardCrossFade = default;
            string speciesName = GetSpeciesName(group.species, groupIndex);

            VegetationDiagnostics.ForwardPrepareStarted(
                camera,
                speciesName,
                group.instanceCount,
                passID,
                group.diagnosticForwardPreparePassID
            );
            group.diagnosticForwardPreparePassID = passID;

            string resourceKey = GetForwardResourceKey(group, groupIndex);
            cmd.BeginSample("Vegetation Forward Cull");
            ResetVisibleBuffers(cmd, group, camera, passID, resourceKey, speciesName);
            cmd.EndSample("Vegetation Forward Cull");

            for (int rangeIndex = 0; rangeIndex < group.chunkRanges.Count; rangeIndex++)
            {
                group.chunkRanges[rangeIndex].visibleLastFrame = true;
            }

            if (group.lod0 == null || !group.lod0.IsValid) continue;

            cmd.BeginSample("Vegetation Forward CopyCounter");

            int matrixCapacity = group.matrixBuffer != null ? group.matrixBuffer.count : 0;
            int visibleCapacity = group.lod0.visibleIndexBuffer != null ? group.lod0.visibleIndexBuffer.count : 0;
            int knownSafeInstanceCount = Mathf.Min(group.instanceCount, matrixCapacity, visibleCapacity);

            VegetationDiagnostics.ValidateDisableCullingInput(
                passID,
                camera,
                speciesName,
                group.instanceCount,
                matrixCapacity,
                visibleCapacity,
                knownSafeInstanceCount
            );

            PrepareLODWithoutCulling(cmd, group, group.lod0, camera, knownSafeInstanceCount, additionalLightsCount);
            PrepareLODWithoutCulling(cmd, group, group.lod1, camera, 0, additionalLightsCount);
            PrepareLODWithoutCulling(cmd, group, group.lod2, camera, 0, additionalLightsCount);
            PrepareLODWithoutCulling(cmd, group, group.lod3, camera, 0, additionalLightsCount);

            VegetationDiagnostics.GpuCommand(passID, camera, resourceKey, speciesName, "CopyCounter/ArgsCPU",
                diagnosticsMode, diagnosticsVerboseInterval);

            VegetationDispatchStats dispatchStats = new VegetationDispatchStats
            {
                selectedChunkCount = group.chunkRanges.Count
            };
            VegetationDiagnostics.RecordForwardPrepare(passID, group.instanceCount, 0);
            ScheduleForwardReadback(cmd, camera, group, speciesName, dispatchStats, "Bypassed",
                0, 0, 0, false, passID);
            cmd.EndSample("Vegetation Forward CopyCounter");
        }
    }

    private static void PrepareLODWithoutCulling(
        CommandBuffer cmd,
        RenderGroup group,
        LODRenderData lod,
        Camera camera,
        int instanceCount,
        int additionalLightsCount)
    {
        if (cmd == null || group == null || lod == null || !lod.IsValid) return;

        lod.propertyBlock.SetBuffer(VegetationMatricesID, group.matrixBuffer);
        lod.propertyBlock.SetBuffer(VegetationVisibleIndicesID, lod.visibleIndexBuffer);
        lod.propertyBlock.SetInt(VegetationUseVisibleIndicesID, 0);
        SetCameraVectors(lod.propertyBlock, camera);
        SetAmbientProbe(lod.propertyBlock);
        lod.propertyBlock.SetInt(VegetationAdditionalLightsCountID, Mathf.Max(0, additionalLightsCount));
        lod.propertyBlock.SetInt(VegetationReceiveShadowsID, group.species != null && group.species.receiveShadows ? 1 : 0);

        uint safeCount = (uint)Mathf.Max(0, instanceCount);
        for (int partIndex = 0; partIndex < lod.parts.Count; partIndex++)
        {
            lod.parts[partIndex].SetInstanceCount(cmd, safeCount);
        }
    }

    private void DispatchRange(
        CommandBuffer cmd,
        RenderGroup group,
        int kernel,
        int startIndex,
        int count,
        Camera camera,
        string speciesName,
        string resourceKey,
        long passID,
        string kernelName,
        uint threadX,
        uint threadY,
        uint threadZ,
        ref VegetationDispatchStats dispatchStats)
    {
        if (count <= 0) return;

        VegetationDiagnostics.VerifyResetBeforeDispatch(
            camera,
            speciesName,
            passID,
            "VisibleLOD0/1/2/3 + DummyLOD1/2/3",
            group.diagnosticForwardResetPassID
        );

        cmd.SetComputeIntParam(cullingCompute, StartIndexID, startIndex);
        cmd.SetComputeIntParam(cullingCompute, InstanceCountID, count);

        int threadsPerGroupX = Mathf.Max(1, (int)threadX);
        int threadGroupCount = Mathf.CeilToInt(count / (float)threadsPerGroupX);

        VegetationDiagnostics.ValidateDispatch(
            camera,
            speciesName,
            "Forward",
            kernelName,
            count,
            threadGroupCount,
            1,
            1,
            threadX,
            threadY,
            threadZ,
            group.instanceCount,
            diagnosticsSafeInstanceLimit,
            passID
        );

        cmd.DispatchCompute(cullingCompute, kernel, threadGroupCount, 1, 1);

        VegetationDiagnostics.GpuCommand(passID, camera, resourceKey, speciesName, "Dispatch",
            diagnosticsMode, diagnosticsVerboseInterval);

        cullingDispatchCount++;
        dispatchStats.dispatchInstanceCount += count;
        dispatchStats.dispatchCallCount++;
        dispatchStats.totalGroupX += threadGroupCount;
        dispatchStats.maxGroupX = Mathf.Max(dispatchStats.maxGroupX, threadGroupCount);
        dispatchStats.theoreticalThreadCount += (long)threadGroupCount * threadX * threadY * threadZ;
    }

    private void ScheduleForwardReadback(
        CommandBuffer cmd,
        Camera camera,
        RenderGroup group,
        string speciesName,
        VegetationDispatchStats dispatchStats,
        string kernelName,
        uint threadX,
        uint threadY,
        uint threadZ,
        bool gpuCulling,
        long passID)
    {
        if (passID == 0) return;
        if (diagnosticsMode == VegetationDiagnosticsMode.Off) return;
        if (!enableDiagnosticsGPUReadback) return;
        if (!VegetationDiagnostics.ShouldReadback(diagnosticsGPUReadbackInterval, passID)) return;

        VegetationDiagnostics.GroupReadback readback = VegetationDiagnostics.BeginGroupReadback(
            camera,
            diagnosticsMode,
            diagnosticsVerboseInterval,
            speciesName,
            group.instanceCount,
            group.chunkRanges.Count,
            gpuCulling ? group.diagnosticForwardResetFrame : -1,
            dispatchStats,
            kernelName,
            threadX,
            threadY,
            threadZ,
            diagnosticsSafeInstanceLimit,
            gpuCulling,
            gpuCulling && enableGPULOD,
            gpuCulling && (group.forwardCrossFade.widths.x > 0f || group.forwardCrossFade.widths.y > 0f || group.forwardCrossFade.widths.z > 0f),
            passID,
            GetLODCapacity(group.lod0),
            GetLODCapacity(group.lod1),
            GetLODCapacity(group.lod2),
            GetLODCapacity(group.lod3)
        );

        ScheduleLODReadback(cmd, readback, group.lod0, 0);
        if (gpuCulling && enableGPULOD)
        {
            ScheduleLODReadback(cmd, readback, group.lod1, 1);
            ScheduleLODReadback(cmd, readback, group.lod2, 2);
            ScheduleLODReadback(cmd, readback, group.lod3, 3);
        }
        else
        {
            readback.SetCounterValue(1, 0);
            readback.SetCounterValue(2, 0);
            readback.SetCounterValue(3, 0);
        }

        readback.Seal();
    }

    private static void ScheduleLODReadback(
        CommandBuffer cmd,
        VegetationDiagnostics.GroupReadback readback,
        LODRenderData lod,
        int lodIndex)
    {
        if (lod == null || !lod.IsValid)
        {
            readback.SetCounterValue(lodIndex, 0);
            return;
        }

        if (lod.diagnosticCounterBuffer == null)
        {
            lod.diagnosticCounterBuffer = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
        }

        cmd.CopyCounterValue(lod.visibleIndexBuffer, lod.diagnosticCounterBuffer, 0);
        readback.RequestCounter(cmd, lod.diagnosticCounterBuffer, lodIndex);
        ScheduleLODArgsReadback(cmd, readback, lod, lodIndex);
    }

    private static void ScheduleLODArgsReadback(
        CommandBuffer cmd,
        VegetationDiagnostics.GroupReadback readback,
        LODRenderData lod,
        int lodIndex)
    {
        if (lod == null || !lod.IsValid) return;

        string meshName = lod.mesh != null ? lod.mesh.name : "<null>";

        for (int partIndex = 0; partIndex < lod.parts.Count; partIndex++)
        {
            RenderPart part = lod.parts[partIndex];
            if (part.argsBuffer == null || part.fullArgs == null || part.fullArgs.Length < 5) continue;

            readback.RequestArgs(
                cmd,
                part.argsBuffer,
                lodIndex,
                meshName,
                part.subMeshIndex,
                part.fullArgs[0],
                part.fullArgs[2],
                part.fullArgs[3],
                part.fullArgs[4]
            );
        }
    }

    private static string GetSpeciesName(VegetationSpecies species, int groupIndex)
    {
        if (species == null) return $"<null:{groupIndex}>";
        return string.IsNullOrEmpty(species.speciesName) ? $"Species#{groupIndex}" : species.speciesName;
    }

    private string GetForwardResourceKey(RenderGroup group, int groupIndex)
    {
        if (group == null) return $"Renderer={GetInstanceID()}/ForwardGroup={groupIndex}";
        if (string.IsNullOrEmpty(group.diagnosticResourceKey))
            group.diagnosticResourceKey = $"Renderer={GetInstanceID()}/ForwardGroup={groupIndex}";
        return group.diagnosticResourceKey;
    }

    private static int GetLODCapacity(LODRenderData lod)
    {
        return lod != null && lod.visibleIndexBuffer != null ? lod.visibleIndexBuffer.count : 0;
    }

    private int DrawLODForward(CommandBuffer cmd, RenderGroup group, LODRenderData lod, int lodIndex, bool depthNormals = false)
    {
        if (lod == null || !lod.IsValid) return 0;

        SetLODCrossFadeProperties(lod.propertyBlock, lodIndex, group.forwardCrossFade);

        VegetationSpecies species = group != null ? group.species : null;
        bool useDistanceLOD = enableForwardWindLOD && species != null && species.vegetationType == VegetationType.Grass;
        lod.propertyBlock.SetInt(VegetationForwardWindLODEnabledID, enableForwardWindLOD ? 1 : 0);
        lod.propertyBlock.SetInt(VegetationForwardWindQualityID, GetForwardWindQuality(lodIndex));
        lod.propertyBlock.SetInt(VegetationForwardWindUseDistanceLODID, useDistanceLOD ? 1 : 0);
        lod.propertyBlock.SetVector(VegetationForwardWindLODDistanceSqID, GetForwardWindLODDistanceSq(species));

        int submittedDraws = 0;

        for (int partIndex = 0; partIndex < lod.parts.Count; partIndex++)
        {
            RenderPart part = lod.parts[partIndex];

            if (!IsMaterialReady(part.material)) continue;

            int passIndex = depthNormals ? part.material.FindPass("DepthNormalsOnly") : FindForwardPass(part.material);

            if (passIndex < 0) continue;

            cmd.DrawMeshInstancedIndirect(
                lod.mesh,
                part.subMeshIndex,
                part.material,
                passIndex,
                part.argsBuffer,
                0,
                lod.propertyBlock
            );
            submittedDraws++;
        }

        return submittedDraws;
    }

    private static int FindForwardPass(Material material)
    {
        if (!IsMaterialReady(material)) return -1;

        int passIndex = material.FindPass("Forward");

        if (passIndex < 0)
        {
            passIndex = material.FindPass("ForwardLit");
        }

        if (passIndex < 0 || passIndex >= material.passCount)
        {
            return -1;
        }

        return passIndex;
    }

    private static bool IsMaterialReady(Material material)
    {
        if (material == null || material.shader == null) return false;

        string shaderName = material.shader.name;

        if (shaderName == "Hidden/Internal-Loading") return false;
        if (shaderName == "Hidden/InternalErrorShader") return false;

        return true;
    }

    private void PrepareShadowsForCamera(CommandBuffer cmd, Camera camera, long shadowInvocationID)
    {
        if (cmd == null || camera == null) return;

        bool canUseCulling = !disableGPUCulling && enableGPUCulling && cullingCompute != null && (Application.isPlaying || enableCullingInEditMode);
        if (!canUseCulling || !PrepareComputeShader())
        {
            PrepareShadowsWithoutCulling(cmd, camera, shadowInvocationID);
            return;
        }

        Camera cullingCamera = ResolveCullingCamera(camera);
        if (cullingCamera == null) cullingCamera = camera;

        Transform cameraTransform = cullingCamera.transform;
        Vector3 cameraPosition = cameraTransform.position;

        cmd.SetComputeVectorParam(cullingCompute, CameraPositionWSID, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 0f));
        cmd.SetComputeVectorParam(cullingCompute, CameraForwardWSID, new Vector4(cameraTransform.forward.x, cameraTransform.forward.y, cameraTransform.forward.z, 0f));
        cmd.SetComputeVectorParam(cullingCompute, CameraRightWSID, new Vector4(cameraTransform.right.x, cameraTransform.right.y, cameraTransform.right.z, 0f));
        cmd.SetComputeVectorParam(cullingCompute, CameraUpWSID, new Vector4(cameraTransform.up.x, cameraTransform.up.y, cameraTransform.up.z, 0f));
        cmd.SetComputeFloatParam(cullingCompute, AspectID, Mathf.Max(cullingCamera.aspect, 0.001f));
        cmd.SetComputeFloatParam(cullingCompute, NearClipID, cullingCamera.nearClipPlane);
        cmd.SetComputeFloatParam(cullingCompute, TanHalfFovID, Mathf.Tan(cullingCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
        cmd.SetComputeFloatParam(cullingCompute, OrthographicSizeID, cullingCamera.orthographicSize);
        cmd.SetComputeIntParam(cullingCompute, IsOrthographicID, cullingCamera.orthographic ? 1 : 0);

        // Shadow caster不能直接按Camera视锥剔除，否则屏幕外物体可能无法把阴影投进画面。
        // Phase 2使用Camera Frustum沿Directional Light来光方向拉伸出的Influence Bounds做保守剔除。
        cmd.SetComputeIntParam(cullingCompute, EnableFrustumCullingID, 0);
        cmd.SetComputeIntParam(cullingCompute, EnableShadowInfluenceCullingID, 0);
        cmd.SetComputeIntParam(cullingCompute, LODOffsetID, Mathf.Clamp(shadowLODOffset, 0, 3));

        for (int groupIndex = 0; groupIndex < renderGroups.Count; groupIndex++)
        {
            RenderGroup group = renderGroups[groupIndex];
            VegetationSpecies species = group.species;
            if (species == null || !species.castShadows) continue;
            if (group.lod0 == null || !group.lod0.IsValid) continue;

            CameraShadowState shadowState = GetOrCreateShadowState(group, camera);
            if (shadowState == null || shadowState.lod0 == null) continue;

            ResetShadowVisibleBuffers(cmd, shadowState, camera, shadowInvocationID);
            shadowState.hasDrawBounds = false;

            float cullDistance = Mathf.Max(species.GetShadowCullDistance(), 0.01f);
            bool useShadowInfluence = TryBuildShadowInfluenceBounds(cullingCamera, cullDistance, out Bounds shadowInfluenceBounds);
            cmd.SetComputeIntParam(cullingCompute, EnableShadowInfluenceCullingID, useShadowInfluence ? 1 : 0);
            if (useShadowInfluence)
            {
                Vector3 influenceMin = shadowInfluenceBounds.min;
                Vector3 influenceMax = shadowInfluenceBounds.max;
                cmd.SetComputeVectorParam(cullingCompute, ShadowInfluenceMinID, new Vector4(influenceMin.x, influenceMin.y, influenceMin.z, 0f));
                cmd.SetComputeVectorParam(cullingCompute, ShadowInfluenceMaxID, new Vector4(influenceMax.x, influenceMax.y, influenceMax.z, 0f));
            }

            float lodScale = Mathf.Clamp(species.shadowLODScale, 0.1f, 2f);
            float lod0Distance = enableGPULOD ? Mathf.Clamp(species.lod0Distance * lodScale, 0f, cullDistance) : cullDistance;
            float lod1Distance = enableGPULOD ? Mathf.Clamp(species.lod1Distance * lodScale, lod0Distance, cullDistance) : cullDistance;
            float lod2Distance = enableGPULOD ? Mathf.Clamp(species.lod2Distance * lodScale, lod1Distance, cullDistance) : cullDistance;

            bool useDensityLOD = enableGPULOD && species.vegetationType == VegetationType.Grass;
            bool hasLOD1 = enableGPULOD && group.lod1 != null && group.lod1.IsValid;
            bool hasLOD2 = enableGPULOD && group.lod2 != null && group.lod2.IsValid;
            bool hasLOD3 = enableGPULOD && group.lod3 != null && group.lod3.IsValid;

            shadowState.crossFade = BuildLODCrossFadeData(species, cameraPosition, lod0Distance, lod1Distance,
                lod2Distance, cullDistance, Mathf.Clamp(shadowLODOffset, 0, 3), hasLOD1, hasLOD2, hasLOD3, useDensityLOD);
            FilterUnsupportedCrossFades(species, ref shadowState.crossFade, hasLOD1, hasLOD2, hasLOD3);

            cmd.SetComputeFloatParam(cullingCompute, LOD0DistanceID, lod0Distance);
            cmd.SetComputeFloatParam(cullingCompute, LOD1DistanceID, lod1Distance);
            cmd.SetComputeFloatParam(cullingCompute, LOD2DistanceID, lod2Distance);
            cmd.SetComputeFloatParam(cullingCompute, CullDistanceID, cullDistance);
            cmd.SetComputeVectorParam(cullingCompute, LODCrossFadeWidthsID, shadowState.crossFade.widths);
            cmd.SetComputeFloatParam(cullingCompute, MidDensityID, Mathf.Clamp01(species.grassMidDensity));
            cmd.SetComputeFloatParam(cullingCompute, FarDensityID, Mathf.Clamp01(species.grassFarDensity));
            cmd.SetComputeIntParam(cullingCompute, UseDensityLODID, useDensityLOD ? 1 : 0);
            cmd.SetComputeIntParam(cullingCompute, HasLOD1ID, hasLOD1 ? 1 : 0);
            cmd.SetComputeIntParam(cullingCompute, HasLOD2ID, hasLOD2 ? 1 : 0);
            cmd.SetComputeIntParam(cullingCompute, HasLOD3ID, hasLOD3 ? 1 : 0);

            int activeKernel = enableGPULOD ? cullLODKernel : cullOnlyKernel;
            GetKernelMetadata(activeKernel, out string kernelName, out uint threadX, out uint threadY, out uint threadZ);
            cmd.SetComputeBufferParam(cullingCompute, activeKernel, SourceBoundsID, group.boundsBuffer);
            cmd.SetComputeBufferParam(cullingCompute, activeKernel, VisibleLOD0ID, shadowState.lod0.visibleIndexBuffer);
            cmd.SetComputeBufferParam(cullingCompute, activeKernel, PersistentIDsID, group.persistentIDBuffer);

            if (enableGPULOD)
            {
                cmd.SetComputeBufferParam(cullingCompute, activeKernel, SourceMatricesID, group.matrixBuffer);
                cmd.SetComputeBufferParam(cullingCompute, activeKernel, VisibleLOD1ID, shadowState.lod1 != null ? shadowState.lod1.visibleIndexBuffer : shadowState.dummyLOD1Buffer);
                cmd.SetComputeBufferParam(cullingCompute, activeKernel, VisibleLOD2ID, shadowState.lod2 != null ? shadowState.lod2.visibleIndexBuffer : shadowState.dummyLOD2Buffer);
                cmd.SetComputeBufferParam(cullingCompute, activeKernel, VisibleLOD3ID, shadowState.lod3 != null ? shadowState.lod3.visibleIndexBuffer : shadowState.dummyLOD3Buffer);
            }

            string speciesName = GetSpeciesName(species, groupIndex);
            VegetationDispatchStats dispatchStats = default;
            if (group.instanceCount > 0)
            {
                if (enableShadowChunkCulling && group.chunkRanges.Count > 0) CullAndDispatchShadowChunkRanges(cmd, group, shadowState, activeKernel, cameraPosition, cullDistance, useShadowInfluence, shadowInfluenceBounds, camera, speciesName, shadowInvocationID, kernelName, threadX, threadY, threadZ, ref dispatchStats);
                else
                {
                    shadowState.drawBounds = group.drawBounds;
                    shadowState.hasDrawBounds = true;
                    DispatchShadowRange(cmd, group, shadowState, activeKernel, 0, group.instanceCount, camera, speciesName, shadowInvocationID, kernelName, threadX, threadY, threadZ, ref dispatchStats);
                }
            }

            VegetationDiagnostics.RecordShadowPrepare(shadowInvocationID, dispatchStats.dispatchInstanceCount, dispatchStats.dispatchCallCount);
            PrepareShadowLODForCamera(cmd, group.lod0, shadowState.lod0, camera);
            PrepareShadowLODForCamera(cmd, group.lod1, shadowState.lod1, camera);
            PrepareShadowLODForCamera(cmd, group.lod2, shadowState.lod2, camera);
            PrepareShadowLODForCamera(cmd, group.lod3, shadowState.lod3, camera);
        }
    }

    private bool TryBuildShadowInfluenceBounds(Camera camera, float cullDistance, out Bounds bounds)
    {
        bounds = default;
        if (!enableShadowInfluenceCulling || camera == null || cullDistance <= 0f) return false;

        Light directionalLight = ResolveShadowDirectionalLight();
        if (directionalLight == null) return false;

        float nearDistance = Mathf.Max(camera.nearClipPlane, 0.01f);
        float farDistance = Mathf.Max(nearDistance + 0.01f, Mathf.Min(camera.farClipPlane, cullDistance));
        camera.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), nearDistance, Camera.MonoOrStereoscopicEye.Mono, shadowNearCorners);
        camera.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), farDistance, Camera.MonoOrStereoscopicEye.Mono, shadowFarCorners);

        Transform cameraTransform = camera.transform;
        Vector3 casterOffset = -directionalLight.transform.forward.normalized * cullDistance;
        bool initialized = false;
        for (int i = 0; i < 4; i++)
        {
            Vector3 nearWS = cameraTransform.TransformPoint(shadowNearCorners[i]);
            Vector3 farWS = cameraTransform.TransformPoint(shadowFarCorners[i]);
            if (!initialized)
            {
                bounds = new Bounds(nearWS, Vector3.zero);
                initialized = true;
            }
            bounds.Encapsulate(nearWS);
            bounds.Encapsulate(farWS);
            bounds.Encapsulate(nearWS + casterOffset);
            bounds.Encapsulate(farWS + casterOffset);
        }

        if (!initialized) return false;
        float padding = Mathf.Max(0f, shadowInfluencePadding);
        if (padding > 0f) bounds.Expand(padding * 2f);
        return true;
    }

    private Light ResolveShadowDirectionalLight()
    {
        Light directionalLight = shadowDirectionalLight != null ? shadowDirectionalLight : RenderSettings.sun;
        if (directionalLight == null || directionalLight.type != LightType.Directional) return null;
        if (!directionalLight.enabled || !directionalLight.gameObject.activeInHierarchy) return null;
        return directionalLight;
    }

    private void CullAndDispatchShadowChunkRanges(CommandBuffer cmd, RenderGroup group, CameraShadowState shadowState, int kernel, Vector3 cameraPosition, float cullDistance, bool useShadowInfluence, Bounds shadowInfluenceBounds, Camera camera, string speciesName, long shadowInvocationID, string kernelName, uint threadX, uint threadY, uint threadZ, ref VegetationDispatchStats dispatchStats)
    {
        int mergedStartIndex = -1;
        int mergedCount = 0;
        float cullDistanceSqr = cullDistance * cullDistance;

        for (int rangeIndex = 0; rangeIndex < group.chunkRanges.Count; rangeIndex++)
        {
            ChunkRange range = group.chunkRanges[rangeIndex];
            if (range.activeCount <= 0)
            {
                if (mergedCount > 0)
                {
                    DispatchShadowRange(cmd, group, shadowState, kernel, mergedStartIndex, mergedCount, camera, speciesName, shadowInvocationID, kernelName, threadX, threadY, threadZ, ref dispatchStats);
                    mergedStartIndex = -1;
                    mergedCount = 0;
                }
                continue;
            }

            bool visible = range.bounds.SqrDistance(cameraPosition) <= cullDistanceSqr && (!useShadowInfluence || range.bounds.Intersects(shadowInfluenceBounds));
            if (!visible)
            {
                if (mergedCount > 0)
                {
                    DispatchShadowRange(cmd, group, shadowState, kernel, mergedStartIndex, mergedCount, camera, speciesName, shadowInvocationID, kernelName, threadX, threadY, threadZ, ref dispatchStats);
                    mergedStartIndex = -1;
                    mergedCount = 0;
                }
                continue;
            }

            EncapsulateShadowDrawBounds(shadowState, range.bounds);
            dispatchStats.selectedChunkCount++;
            if (mergedCount == 0)
            {
                mergedStartIndex = range.startIndex;
                mergedCount = range.count;
                continue;
            }

            int mergedEndIndex = mergedStartIndex + mergedCount;
            if (range.startIndex == mergedEndIndex) mergedCount += range.count;
            else
            {
                DispatchShadowRange(cmd, group, shadowState, kernel, mergedStartIndex, mergedCount, camera, speciesName, shadowInvocationID, kernelName, threadX, threadY, threadZ, ref dispatchStats);
                mergedStartIndex = range.startIndex;
                mergedCount = range.count;
            }
        }

        if (mergedCount > 0) DispatchShadowRange(cmd, group, shadowState, kernel, mergedStartIndex, mergedCount, camera, speciesName, shadowInvocationID, kernelName, threadX, threadY, threadZ, ref dispatchStats);
    }

    private void DispatchShadowRange(CommandBuffer cmd, RenderGroup group, CameraShadowState shadowState, int kernel, int startIndex, int count, Camera camera, string speciesName, long shadowInvocationID, string kernelName, uint threadX, uint threadY, uint threadZ, ref VegetationDispatchStats dispatchStats)
    {
        if (count <= 0) return;

        VegetationDiagnostics.VerifyShadowResetBeforeDispatch(camera, speciesName, shadowInvocationID, "Shadow VisibleLOD0/1/2/3 + DummyLOD1/2/3", shadowState.diagnosticResetShadowInvocationID);
        cmd.SetComputeIntParam(cullingCompute, StartIndexID, startIndex);
        cmd.SetComputeIntParam(cullingCompute, InstanceCountID, count);

        int threadGroupCount = Mathf.CeilToInt(count / (float)Mathf.Max(1, (int)threadX));
        VegetationDiagnostics.ValidateDispatch(camera, speciesName, "Shadow", kernelName, count, threadGroupCount, 1, 1, threadX, threadY, threadZ, group.instanceCount, diagnosticsSafeInstanceLimit, shadowInvocationID);
        cmd.DispatchCompute(cullingCompute, kernel, threadGroupCount, 1, 1);

        currentShadowDispatchCount++;
        dispatchStats.dispatchInstanceCount += count;
        dispatchStats.dispatchCallCount++;
        dispatchStats.totalGroupX += threadGroupCount;
        dispatchStats.maxGroupX = Mathf.Max(dispatchStats.maxGroupX, threadGroupCount);
        dispatchStats.theoreticalThreadCount += (long)threadGroupCount * threadX * threadY * threadZ;
    }

    private static void EncapsulateShadowDrawBounds(CameraShadowState state, Bounds bounds)
    {
        if (!state.hasDrawBounds)
        {
            state.drawBounds = bounds;
            state.hasDrawBounds = true;
            return;
        }
        state.drawBounds.Encapsulate(bounds);
    }

    private static void ResetShadowVisibleBuffers(
        CommandBuffer cmd,
        CameraShadowState state,
        Camera camera,
        long shadowInvocationID)
    {
        if (state.lod0 != null) cmd.SetBufferCounterValue(state.lod0.visibleIndexBuffer, 0);
        if (state.lod1 != null) cmd.SetBufferCounterValue(state.lod1.visibleIndexBuffer, 0);
        if (state.lod2 != null) cmd.SetBufferCounterValue(state.lod2.visibleIndexBuffer, 0);
        if (state.lod3 != null) cmd.SetBufferCounterValue(state.lod3.visibleIndexBuffer, 0);
        if (state.dummyLOD1Buffer != null) cmd.SetBufferCounterValue(state.dummyLOD1Buffer, 0);
        if (state.dummyLOD2Buffer != null) cmd.SetBufferCounterValue(state.dummyLOD2Buffer, 0);
        if (state.dummyLOD3Buffer != null) cmd.SetBufferCounterValue(state.dummyLOD3Buffer, 0);

        state.diagnosticResetShadowInvocationID = shadowInvocationID;
    }

    private static void PrepareShadowLODForCamera(CommandBuffer cmd, LODRenderData sourceLOD, ShadowLODRenderData shadowLOD, Camera camera)
    {
        if (sourceLOD == null || !sourceLOD.IsValid || shadowLOD == null) return;

        shadowLOD.propertyBlock.SetInt(VegetationUseVisibleIndicesID, 1);
        SetCameraVectors(shadowLOD.propertyBlock, camera);

        int partCount = Mathf.Min(sourceLOD.parts.Count, shadowLOD.parts.Count);
        for (int partIndex = 0; partIndex < partCount; partIndex++)
        {
            ShadowRenderPart part = shadowLOD.parts[partIndex];
            if (part.argsBuffer == null) continue;
            cmd.CopyCounterValue(shadowLOD.visibleIndexBuffer, part.argsBuffer, sizeof(uint));
        }
    }

    private void PrepareShadowsWithoutCulling(CommandBuffer cmd, Camera camera, long shadowInvocationID)
    {
        for (int groupIndex = 0; groupIndex < renderGroups.Count; groupIndex++)
        {
            RenderGroup group = renderGroups[groupIndex];
            if (group.species == null || !group.species.castShadows) continue;

            CameraShadowState shadowState = GetOrCreateShadowState(group, camera);
            if (shadowState == null) continue;

            shadowState.crossFade = default;

            ResetShadowVisibleBuffers(cmd, shadowState, camera, shadowInvocationID);
            shadowState.drawBounds = group.drawBounds;
            shadowState.hasDrawBounds = true;

            RestoreShadowLODWithoutCulling(cmd, shadowState.lod0, camera, group.instanceCount);
            RestoreShadowLODWithoutCulling(cmd, shadowState.lod1, camera, 0);
            RestoreShadowLODWithoutCulling(cmd, shadowState.lod2, camera, 0);
            RestoreShadowLODWithoutCulling(cmd, shadowState.lod3, camera, 0);
            VegetationDiagnostics.RecordShadowPrepare(shadowInvocationID, group.instanceCount, 0);
        }
    }

    private static void RestoreShadowLODWithoutCulling(
        CommandBuffer cmd,
        ShadowLODRenderData shadowLOD,
        Camera camera,
        int instanceCount)
    {
        if (cmd == null || shadowLOD == null) return;

        shadowLOD.propertyBlock.SetInt(VegetationUseVisibleIndicesID, 0);
        SetCameraVectors(shadowLOD.propertyBlock, camera);

        uint visibleInstanceCount = (uint)Mathf.Max(instanceCount, 0);
        for (int partIndex = 0; partIndex < shadowLOD.parts.Count; partIndex++)
        {
            shadowLOD.parts[partIndex].SetInstanceCount(cmd, visibleInstanceCount);
        }
    }

    private void SubmitShadowCasters(Camera camera, long shadowInvocationID)
    {
        if (camera == null) return;

        for (int groupIndex = 0; groupIndex < renderGroups.Count; groupIndex++)
        {
            RenderGroup group = renderGroups[groupIndex];
            if (group.species == null || !group.species.castShadows) continue;

            CameraShadowState shadowState = GetShadowState(group, camera);
            if (shadowState == null || !shadowState.hasDrawBounds) continue;

            int submittedDraws = 0;
            submittedDraws += DrawLODShadows(group, group.lod0, shadowState.lod0, shadowState.drawBounds, camera);
            submittedDraws += DrawLODShadows(group, group.lod1, shadowState.lod1, shadowState.drawBounds, camera);
            submittedDraws += DrawLODShadows(group, group.lod2, shadowState.lod2, shadowState.drawBounds, camera);
            submittedDraws += DrawLODShadows(group, group.lod3, shadowState.lod3, shadowState.drawBounds, camera);
            VegetationDiagnostics.RecordShadowDraw(shadowInvocationID, submittedDraws);
            currentShadowDrawCount += submittedDraws;
        }
    }

    private int DrawLODShadows(RenderGroup group, LODRenderData sourceLOD, ShadowLODRenderData shadowLOD, Bounds drawBounds, Camera camera)
    {
        if (sourceLOD == null || !sourceLOD.IsValid || shadowLOD == null) return 0;

        int lodIndex = sourceLOD == group.lod0 ? 0 : sourceLOD == group.lod1 ? 1 : sourceLOD == group.lod2 ? 2 : 3;
        CameraShadowState shadowState = GetShadowState(group, camera);
        SetLODCrossFadeProperties(shadowLOD.propertyBlock, lodIndex, shadowState != null ? shadowState.crossFade : default);

        shadowLOD.propertyBlock.SetInt(VegetationShadowWindLODEnabledID, enableShadowWindLOD ? 1 : 0);
        shadowLOD.propertyBlock.SetInt(VegetationShadowWindQualityID, Mathf.Clamp(shadowWindQuality, 0, 3));
        shadowLOD.propertyBlock.SetVector(VegetationShadowWindLODDistanceSqID, GetShadowWindLODDistanceSq(group.species));
        shadowLOD.propertyBlock.SetInt(VegetationShadowAlphaCutoffLODEnabledID, enableShadowAlphaCutoffLOD ? 1 : 0);
        shadowLOD.propertyBlock.SetVector(VegetationShadowAlphaCutoffBiasID, new Vector4(Mathf.Clamp(shadowAlphaCutoffSimplifiedBias, 0f, 0.25f), Mathf.Clamp(shadowAlphaCutoffMainBendBias, 0f, 0.25f), Mathf.Clamp(shadowAlphaCutoffStaticBias, 0f, 0.25f), 0f));

        int submittedDraws = 0;
        int partCount = Mathf.Min(sourceLOD.parts.Count, shadowLOD.parts.Count);
        for (int partIndex = 0; partIndex < partCount; partIndex++)
        {
            RenderPart part = sourceLOD.parts[partIndex];
            ShadowRenderPart shadowPart = shadowLOD.parts[partIndex];
            if (shadowPart.argsBuffer == null) continue;
            if (!IsMaterialReady(part.material)) continue;

            Graphics.DrawMeshInstancedIndirect(
                sourceLOD.mesh,
                part.subMeshIndex,
                part.material,
                drawBounds,
                shadowPart.argsBuffer,
                0,
                shadowLOD.propertyBlock,
                ShadowCastingMode.ShadowsOnly,
                false,
                gameObject.layer,
                camera
            );
            submittedDraws++;
        }

        return submittedDraws;
    }

    private int GetForwardWindQuality(int lodIndex)
    {
        if (!enableForwardWindLOD) return 3;
        switch (Mathf.Clamp(lodIndex, 0, 3))
        {
            case 0: return Mathf.Clamp(forwardWindLOD0Quality, 0, 3);
            case 1: return Mathf.Clamp(forwardWindLOD1Quality, 0, 3);
            case 2: return Mathf.Clamp(forwardWindLOD2Quality, 0, 3);
            default: return Mathf.Clamp(forwardWindLOD3Quality, 0, 3);
        }
    }

    private static Vector4 GetForwardWindLODDistanceSq(VegetationSpecies species)
    {
        if (species == null) return Vector4.zero;
        float cullDistance = Mathf.Max(species.cullDistance, 0.01f);
        float fullDistance = Mathf.Clamp(species.lod0Distance, 0f, cullDistance);
        float simplifiedDistance = Mathf.Clamp(species.lod1Distance, fullDistance, cullDistance);
        return new Vector4(fullDistance * fullDistance, simplifiedDistance * simplifiedDistance, cullDistance * cullDistance, 0f);
    }

    private Vector4 GetShadowWindLODDistanceSq(VegetationSpecies species)
    {
        if (species == null) return Vector4.zero;
        float cullDistance = Mathf.Max(species.GetShadowCullDistance(), 0.01f);
        float fullDistance;
        float simplifiedDistance;
        float mainBendDistance;
        if (enableGPULOD)
        {
            float lodScale = Mathf.Clamp(species.shadowLODScale, 0.1f, 2f);
            fullDistance = Mathf.Clamp(species.lod0Distance * lodScale, 0f, cullDistance);
            simplifiedDistance = Mathf.Clamp(species.lod1Distance * lodScale, fullDistance, cullDistance);
            mainBendDistance = Mathf.Clamp(species.lod2Distance * lodScale, simplifiedDistance, cullDistance);
        }
        else
        {
            fullDistance = cullDistance * 0.25f;
            simplifiedDistance = cullDistance * 0.55f;
            mainBendDistance = cullDistance * 0.8f;
        }
        return new Vector4(fullDistance * fullDistance, simplifiedDistance * simplifiedDistance, mainBendDistance * mainBendDistance, cullDistance * cullDistance);
    }

    private static void SetLODCrossFadeProperties(MaterialPropertyBlock block, int lodIndex, LODCrossFadeData data)
    {
        block.SetInt(VegetationLODIndexID, lodIndex);
        block.SetVector(VegetationLODDistancesID, data.distances);
        block.SetVector(VegetationLODCrossFadeParamsID, data.widths);
        block.SetVector(VegetationLODReferencePositionID, data.referencePosition);
        block.SetInt(VegetationLODOffsetID, data.offset);
    }

    private static int ResolveMeshLOD(int baseLOD, int offset, bool hasLOD1, bool hasLOD2, bool hasLOD3)
    {
        int target = Mathf.Min(baseLOD + Mathf.Max(offset, 0), 3);
        if (target <= 0) return 0;
        if (target == 1)
        {
            if (hasLOD1) return 1;
            if (hasLOD2) return 2;
            if (hasLOD3) return 3;
            return 0;
        }
        if (target == 2)
        {
            if (hasLOD2) return 2;
            if (hasLOD3) return 3;
            if (hasLOD1) return 1;
            return 0;
        }
        if (hasLOD3) return 3;
        if (hasLOD2) return 2;
        if (hasLOD1) return 1;
        return 0;
    }

    private static LODCrossFadeData BuildLODCrossFadeData(VegetationSpecies species, Vector3 cameraPosition,
        float lod0Distance, float lod1Distance, float lod2Distance, float cullDistance,
        int offset, bool hasLOD1, bool hasLOD2, bool hasLOD3, bool useDensityLOD)
    {
        LODCrossFadeData data = new LODCrossFadeData
        {
            distances = new Vector4(lod0Distance, lod1Distance, lod2Distance, cullDistance),
            referencePosition = new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, 0f),
            offset = offset
        };
        if (species == null || !species.enableLODCrossFade || species.lodCrossFadeWidth <= 0f || useDensityLOD) return data;

        Vector4 thresholds = data.distances;
        Vector4 widths = Vector4.zero;
        for (int boundary = 0; boundary < 3; boundary++)
        {
            int nearLOD = ResolveMeshLOD(boundary, offset, hasLOD1, hasLOD2, hasLOD3);
            int farLOD = ResolveMeshLOD(boundary + 1, offset, hasLOD1, hasLOD2, hasLOD3);
            if (farLOD != nearLOD + 1) continue;

            float distance = thresholds[boundary];
            widths[boundary] = Mathf.Min(species.lodCrossFadeWidth, 2f * distance, 2f * (cullDistance - distance));
            widths[boundary] = Mathf.Max(0f, widths[boundary]);
        }

        for (int first = 0; first < 3; first++)
        for (int second = first + 1; second < 3; second++)
        {
            if (widths[first] <= 0f || widths[second] <= 0f) continue;
            float separation = Mathf.Max(0f, thresholds[second] - thresholds[first]);
            widths[first] = Mathf.Min(widths[first], separation);
            widths[second] = Mathf.Min(widths[second], separation);
        }

        data.widths = new Vector4(widths[0], widths[1], widths[2], 0f);
        return data;
    }

    private static VegetationLODAsset GetLODAsset(VegetationSpecies species, int lodIndex)
    {
        if (species == null) return null;
        switch (lodIndex)
        {
            case 0: return species.lod0;
            case 1: return species.lod1;
            case 2: return species.lod2;
            default: return species.lod3;
        }
    }

    private static bool SupportsLODCrossFade(VegetationLODAsset asset)
    {
        if (asset == null || !asset.IsValid) return false;
        for (int i = 0; i < asset.materials.Length; i++)
        {
            Material material = asset.materials[i];
            if (material == null || material.shader == null) return false;
            string shaderName = material.shader.name;
            if (shaderName != "Vegetation/Grass" && shaderName != "Vegetation/Bush" &&
                shaderName != "Vegetation/Foliage" && shaderName != "Custom/VegetationTrunk" &&
                shaderName != "Vegetation/BushBillboard" &&
                shaderName != "ANGRYMESH/Stylized Pack/Grass VegetationIndirect" &&
                shaderName != "ANGRYMESH/Stylized Pack/Props VegetationIndirect" &&
                shaderName != "ANGRYMESH/Stylized Pack/Tree Bark VegetationIndirect" &&
                shaderName != "ANGRYMESH/Stylized Pack/Tree Leaf VegetationIndirect") return false;
        }
        return true;
    }

    private static void FilterUnsupportedCrossFades(VegetationSpecies species, ref LODCrossFadeData data,
        bool hasLOD1, bool hasLOD2, bool hasLOD3)
    {
        for (int boundary = 0; boundary < 3; boundary++)
        {
            if (data.widths[boundary] <= 0f) continue;
            int nearLOD = ResolveMeshLOD(boundary, data.offset, hasLOD1, hasLOD2, hasLOD3);
            int farLOD = ResolveMeshLOD(boundary + 1, data.offset, hasLOD1, hasLOD2, hasLOD3);
            if (!SupportsLODCrossFade(GetLODAsset(species, nearLOD)) ||
                !SupportsLODCrossFade(GetLODAsset(species, farLOD))) data.widths[boundary] = 0f;
        }
    }

    private static void SetCameraVectors(MaterialPropertyBlock propertyBlock, Camera camera)
    {
        Transform cameraTransform = camera.transform;

        propertyBlock.SetVector(CameraPositionWSID, cameraTransform.position);
        propertyBlock.SetVector(CameraForwardWSID, cameraTransform.forward);
        propertyBlock.SetVector(CameraRightWSID, cameraTransform.right);
        propertyBlock.SetVector(CameraUpWSID, cameraTransform.up);
    }

    private static void SetAmbientProbe(MaterialPropertyBlock propertyBlock)
    {
        SHCoefficients coefficients = new SHCoefficients(RenderSettings.ambientProbe);

        propertyBlock.SetVector(UnitySHArID, coefficients.SHAr);
        propertyBlock.SetVector(UnitySHAgID, coefficients.SHAg);
        propertyBlock.SetVector(UnitySHAbID, coefficients.SHAb);
        propertyBlock.SetVector(UnitySHBrID, coefficients.SHBr);
        propertyBlock.SetVector(UnitySHBgID, coefficients.SHBg);
        propertyBlock.SetVector(UnitySHBbID, coefficients.SHBb);
        propertyBlock.SetVector(UnitySHCID, coefficients.SHC);
        propertyBlock.SetVector(UnityProbesOcclusionID, coefficients.ProbesOcclusion);
    }

    private bool PrepareComputeShader()
    {
        if (cullingCompute == null) return false;

        if (cullOnlyKernel < 0)
        {
            if (!cullingCompute.HasKernel("CSCullOnly")) return false;

            cullOnlyKernel = cullingCompute.FindKernel("CSCullOnly");
            cullingCompute.GetKernelThreadGroupSizes(cullOnlyKernel, out cullOnlyThreadX, out cullOnlyThreadY, out cullOnlyThreadZ);
        }

        if (cullLODKernel < 0)
        {
            if (!cullingCompute.HasKernel("CSCullLOD")) return false;

            cullLODKernel = cullingCompute.FindKernel("CSCullLOD");
            cullingCompute.GetKernelThreadGroupSizes(cullLODKernel, out cullLODThreadX, out cullLODThreadY, out cullLODThreadZ);
        }

        return cullOnlyKernel >= 0 && cullLODKernel >= 0;
    }

    private void GetKernelMetadata(int kernel, out string kernelName, out uint threadX, out uint threadY, out uint threadZ)
    {
        if (kernel == cullLODKernel)
        {
            kernelName = "CSCullLOD";
            threadX = cullLODThreadX;
            threadY = cullLODThreadY;
            threadZ = cullLODThreadZ;
            return;
        }

        kernelName = "CSCullOnly";
        threadX = cullOnlyThreadX;
        threadY = cullOnlyThreadY;
        threadZ = cullOnlyThreadZ;
    }

    private void ResetKernelThreadMetadata()
    {
        cullOnlyThreadX = 64;
        cullOnlyThreadY = 1;
        cullOnlyThreadZ = 1;
        cullLODThreadX = 64;
        cullLODThreadY = 1;
        cullLODThreadZ = 1;
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null) return targetCamera;

        if (!Application.isPlaying)
        {
#if UNITY_EDITOR
            if (SceneView.lastActiveSceneView != null && SceneView.lastActiveSceneView.camera != null)
            {
                return SceneView.lastActiveSceneView.camera;
            }
#endif
        }

        return Camera.main;
    }

    private VegetationSpecies ResolveLODDebugSpecies()
    {
        if (lodDebugSpecies != null) return lodDebugSpecies;

        for (int i = 0; i < renderGroups.Count; i++)
        {
            if (renderGroups[i].species != null)
            {
                return renderGroups[i].species;
            }
        }

        return null;
    }

    private static Bounds CalculateBounds(VegetationSpecies species, List<VegetationInstance> instances)
    {
        Bounds localBounds = species.GetLocalMeshBounds();

        Bounds bounds = CalculateWorldBounds(
            localBounds,
            instances[0].LocalToWorldMatrix,
            species.horizontalBoundsPadding,
            species.verticalBoundsPadding
        );

        for (int i = 1; i < instances.Count; i++)
        {
            bounds.Encapsulate(
                CalculateWorldBounds(
                    localBounds,
                    instances[i].LocalToWorldMatrix,
                    species.horizontalBoundsPadding,
                    species.verticalBoundsPadding
                )
            );
        }

        return bounds;
    }

    private static Bounds CalculateWorldBounds(Bounds localBounds, Matrix4x4 localToWorld, float horizontalPadding, float verticalPadding)
    {
        Vector3 center = localToWorld.MultiplyPoint3x4(localBounds.center);
        Vector3 extents = localBounds.extents;

        Vector3 axisX = localToWorld.MultiplyVector(new Vector3(extents.x, 0f, 0f));
        Vector3 axisY = localToWorld.MultiplyVector(new Vector3(0f, extents.y, 0f));
        Vector3 axisZ = localToWorld.MultiplyVector(new Vector3(0f, 0f, extents.z));

        Vector3 worldExtents = new Vector3(
            Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
            Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
            Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z)
        );

        worldExtents += new Vector3(
            Mathf.Max(0f, horizontalPadding),
            Mathf.Max(0f, verticalPadding),
            Mathf.Max(0f, horizontalPadding)
        );

        return new Bounds(center, worldExtents * 2f);
    }

    private void OnDrawGizmos()
    {
        if (showChunkBounds) DrawChunkDebug();
        if (showLODDistance) DrawLODDebug();
    }

    private void DrawChunkDebug()
    {
        if (renderGroups == null) return;

        for (int groupIndex = 0; groupIndex < renderGroups.Count; groupIndex++)
        {
            RenderGroup group = renderGroups[groupIndex];

            for (int rangeIndex = 0; rangeIndex < group.chunkRanges.Count; rangeIndex++)
            {
                ChunkRange range = group.chunkRanges[rangeIndex];

                if (showOnlyVisibleChunks && !range.visibleLastFrame) continue;

                Gizmos.color = range.visibleLastFrame
                    ? new Color(0.1f, 1f, 0.2f, 1f)
                    : new Color(1f, 0.15f, 0.1f, 1f);

                Gizmos.DrawWireCube(range.bounds.center, range.bounds.size);

#if UNITY_EDITOR
                if (!showChunkLabels) continue;

                GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
                style.alignment = TextAnchor.MiddleCenter;

                style.normal.textColor = range.visibleLastFrame
                    ? new Color(0.1f, 1f, 0.2f)
                    : new Color(1f, 0.2f, 0.15f);

                Vector3 labelPosition = range.bounds.center + Vector3.up * (range.bounds.extents.y + 0.5f);

                Handles.Label(
                    labelPosition,
                    $"Chunk {range.coordinate}\n{group.species.speciesName}\nInstances: {range.activeCount}/{range.count}",
                    style
                );
#endif
            }
        }
    }

    private void DrawLODDebug()
    {
        Camera camera = ResolveCamera();
        VegetationSpecies species = ResolveLODDebugSpecies();

        if (camera == null || species == null) return;

        Vector3 center = camera.transform.position;

        float lod0Distance = Mathf.Max(0f, species.lod0Distance);
        float lod1Distance = Mathf.Max(lod0Distance, species.lod1Distance);
        float lod2Distance = Mathf.Max(lod1Distance, species.lod2Distance);
        float cullDistance = species.vegetationType == VegetationType.Grass
            ? Mathf.Max(lod1Distance, species.cullDistance)
            : Mathf.Max(lod2Distance, species.cullDistance);

        Gizmos.color = new Color(0.1f, 1f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(center, lod0Distance);

        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(center, lod1Distance);

        if (species.vegetationType != VegetationType.Grass)
        {
            Gizmos.color = new Color(1f, 0.45f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(center, lod2Distance);
        }

        Gizmos.color = new Color(1f, 0.15f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(center, cullDistance);

#if UNITY_EDITOR
        Handles.color = new Color(0.1f, 1f, 0.2f, 0.9f);
        Handles.DrawWireDisc(center, Vector3.up, lod0Distance);

        Handles.color = new Color(1f, 0.85f, 0.1f, 0.9f);
        Handles.DrawWireDisc(center, Vector3.up, lod1Distance);

        if (species.vegetationType != VegetationType.Grass)
        {
            Handles.color = new Color(1f, 0.45f, 0.1f, 0.9f);
            Handles.DrawWireDisc(center, Vector3.up, lod2Distance);
        }

        Handles.color = new Color(1f, 0.15f, 0.1f, 0.9f);
        Handles.DrawWireDisc(center, Vector3.up, cullDistance);

        if (!showLODLabels) return;

        GUIStyle infoStyle = new GUIStyle(EditorStyles.boldLabel);
        infoStyle.normal.textColor = Color.white;

        LODCrossFadeData debugFade = BuildLODCrossFadeData(species, camera.transform.position,
            lod0Distance, lod1Distance, lod2Distance, cullDistance, 0,
            enableGPULOD && species.lod1 != null && species.lod1.IsValid,
            enableGPULOD && species.lod2 != null && species.lod2.IsValid,
            enableGPULOD && species.lod3 != null && species.lod3.IsValid,
            !enableGPULOD || species.vegetationType == VegetationType.Grass);
        FilterUnsupportedCrossFades(species, ref debugFade,
            enableGPULOD && species.lod1 != null && species.lod1.IsValid,
            enableGPULOD && species.lod2 != null && species.lod2.IsValid,
            enableGPULOD && species.lod3 != null && species.lod3.IsValid);
        bool fadeActive = debugFade.widths.x > 0f || debugFade.widths.y > 0f || debugFade.widths.z > 0f;

        Handles.Label(
            center + Vector3.up * 2f,
            $"LOD Camera: {camera.name}\nSpecies: {species.speciesName}\nGPU LOD: {(enableGPULOD ? "ON" : "OFF")}" +
            $"\nMesh Crossfade: {(fadeActive ? "ON" : "OFF")} (requested {species.lodCrossFadeWidth:F1}m)" +
            $"\nEffective Widths: {debugFade.widths.x:F1} / {debugFade.widths.y:F1} / {debugFade.widths.z:F1}m" +
            $"\nThresholds: {lod0Distance:F1} / {lod1Distance:F1} / {lod2Distance:F1}m",
            infoStyle
        );

        GUIStyle lod0Style = new GUIStyle(EditorStyles.boldLabel);
        lod0Style.normal.textColor = new Color(0.1f, 1f, 0.2f);

        GUIStyle lod1Style = new GUIStyle(EditorStyles.boldLabel);
        lod1Style.normal.textColor = new Color(1f, 0.85f, 0.1f);

        GUIStyle lod2Style = new GUIStyle(EditorStyles.boldLabel);
        lod2Style.normal.textColor = new Color(1f, 0.45f, 0.1f);

        GUIStyle cullStyle = new GUIStyle(EditorStyles.boldLabel);
        cullStyle.normal.textColor = new Color(1f, 0.15f, 0.1f);

        Vector3 direction = camera.transform.right;

        if (species.vegetationType == VegetationType.Grass)
        {
            Handles.Label(
                center + direction * lod0Distance,
                $"100% Density\n0 - {lod0Distance:F1}m",
                lod0Style
            );

            Handles.Label(
                center + direction * lod1Distance,
                $"Mid Density {species.grassMidDensity * 100f:F0}%\n{lod0Distance:F1} - {lod1Distance:F1}m",
                lod1Style
            );

            Handles.Label(
                center + direction * cullDistance,
                $"Far Density {species.grassFarDensity * 100f:F0}%\n{lod1Distance:F1} - {cullDistance:F1}m\nOutside = Cull",
                cullStyle
            );
        }
        else
        {
            string lod1Text = species.lod1 != null && species.lod1.IsValid
                ? "LOD1"
                : "LOD0 fallback";

            string lod2Text = species.lod2 != null && species.lod2.IsValid
                ? "LOD2"
                : species.lod1 != null && species.lod1.IsValid
                    ? "LOD1 fallback"
                    : "LOD0 fallback";

            string lod3Text = species.lod3 != null && species.lod3.IsValid
                ? "LOD3"
                : species.lod2 != null && species.lod2.IsValid
                    ? "LOD2 fallback"
                    : species.lod1 != null && species.lod1.IsValid
                        ? "LOD1 fallback"
                        : "LOD0 fallback";

            Handles.Label(
                center + direction * lod0Distance,
                $"LOD0 End\n{lod0Distance:F1}m",
                lod0Style
            );

            Handles.Label(
                center + direction * lod1Distance,
                $"{lod1Text} End\n{lod1Distance:F1}m",
                lod1Style
            );

            Handles.Label(
                center + direction * lod2Distance,
                $"{lod2Text} End\n{lod2Distance:F1}m",
                lod2Style
            );

            Handles.Label(
                center + direction * cullDistance,
                $"{lod3Text}\nCull at {cullDistance:F1}m",
                cullStyle
            );
        }
#endif
    }

    private void BeginRuntimeStatsFrame()
    {
        int frame = Time.frameCount;
        if (runtimeStatsFrame == frame) return;

        if (runtimeStatsFrame >= 0)
        {
            completedStatsFrame = runtimeStatsFrame;
            completedForwardCameraCount = forwardCameraIDs.Count;
            completedForwardDispatchCount = currentForwardDispatchCount;
            completedForwardDrawCount = currentForwardDrawCount;
            completedShadowDispatchCount = currentShadowDispatchCount;
            completedShadowDrawCount = currentShadowDrawCount;
        }

        runtimeStatsFrame = frame;
        forwardCameraIDs.Clear();
        currentForwardDispatchCount = 0;
        currentForwardDrawCount = 0;
        currentShadowDispatchCount = 0;
        currentShadowDrawCount = 0;
    }

    private void ReleaseBuffers()
    {
        for (int i = 0; i < renderGroups.Count; i++)
        {
            renderGroups[i].Release();
        }

        renderGroups.Clear();
        renderGroupBySpeciesIndex.Clear();
        incrementalDirtyGroups.Clear();
        pendingChangeSets.Clear();

        preparedWithCulling = false;

        uploadedInstanceCount = 0;
        drawCallCount = 0;
        chunkRangeCount = 0;
        visibleChunkRangeCount = 0;
        cullingDispatchCount = 0;
        forwardCameraIDs.Clear();
        runtimeStatsFrame = -1;
        completedStatsFrame = -1;
        currentForwardDispatchCount = 0;
        currentForwardDrawCount = 0;
        currentShadowDispatchCount = 0;
        currentShadowDrawCount = 0;
        completedForwardCameraCount = 0;
        completedForwardDispatchCount = 0;
        completedForwardDrawCount = 0;
        completedShadowDispatchCount = 0;
        completedShadowDrawCount = 0;
        currentCamera = null;
    }
}
