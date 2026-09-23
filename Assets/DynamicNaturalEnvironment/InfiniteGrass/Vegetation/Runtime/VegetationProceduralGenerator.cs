using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class VegetationProceduralGenerator : MonoBehaviour
{
    [Header("Target")]
    public VegetationDatabase database;
    public VegetationBiomeProfile biome;

    [Header("Area")]
    [Min(0.1f)]
    public Vector2 areaSize =
        new Vector2(
            100f,
            100f
        );

    public bool useTransformYRotation = true;

    [Header("Ground")]
    public LayerMask groundMask = ~0;

    [Min(1f)]
    public float rayHeight = 1000f;

    [Min(1f)]
    public float rayDistance = 2000f;

    [Header("Influence Volumes")]
    public bool useInfluenceVolumes = true;

    [Header("Generation")]
    public int seed = 12345;

    [SerializeField, HideInInspector]
    private List<Vector2Int> generatedIDRanges =
        new List<Vector2Int>();

    [SerializeField, HideInInspector]
    private int lastGeneratedCount;

    [SerializeField, HideInInspector]
    private int lastRemovedCount;

    [SerializeField, HideInInspector]
    private float lastGenerationSeconds;

    [SerializeField, HideInInspector]
    private int lastBiomeMaskRejectCount;

    [SerializeField, HideInInspector]
    private int lastTerrainLayerRejectCount;

    [SerializeField, HideInInspector]
    private int lastTerrainDensityRejectCount;

    [SerializeField, HideInInspector]
    private int lastParentInfluenceRejectCount;

    [SerializeField, HideInInspector]
    private int lastExclusionRejectCount;

    [SerializeField, HideInInspector]
    private int lastDensityRejectCount;

    private readonly List<
        VegetationExclusionVolume
    > exclusionVolumes =
        new List<
            VegetationExclusionVolume
        >();

    private readonly List<
        VegetationDensityVolume
    > densityVolumes =
        new List<
            VegetationDensityVolume
        >();

    private readonly Dictionary<
        Terrain,
        TerrainLayerCache
    > terrainLayerCaches =
        new Dictionary<
            Terrain,
            TerrainLayerCache
        >();

    private readonly HashSet<
        TerrainLayer
    > usedTerrainLayers =
        new HashSet<
            TerrainLayer
        >();

    private readonly Dictionary<
        VegetationSpeciesGroup,
        ParentSpatialHash
    > generatedGroupIndices =
        new Dictionary<
            VegetationSpeciesGroup,
            ParentSpatialHash
        >();

    private Bounds generationWorldBounds;

    private bool runtimeBiomeMaskEnabled;

    private Vector2 biomeBaseOffset;
    private Vector2 biomeDetailOffset;
    private Vector2 biomeWarpOffsetA;
    private Vector2 biomeWarpOffsetB;

    private const int HardCandidateLimitPerLayer =
        2000000;

    private const float ParentSpatialCellSize =
        4f;

    public int LastGeneratedCount =>
        lastGeneratedCount;

    public int LastRemovedCount =>
        lastRemovedCount;

    public float LastGenerationSeconds =>
        lastGenerationSeconds;

    public int GeneratedRangeCount =>
        generatedIDRanges != null
            ? generatedIDRanges.Count
            : 0;

    public int LastBiomeMaskRejectCount =>
        lastBiomeMaskRejectCount;

    public int LastTerrainLayerRejectCount =>
        lastTerrainLayerRejectCount;

    public int LastTerrainDensityRejectCount =>
        lastTerrainDensityRejectCount;

    public int LastParentInfluenceRejectCount =>
        lastParentInfluenceRejectCount;

    public int LastExclusionRejectCount =>
        lastExclusionRejectCount;

    public int LastDensityRejectCount =>
        lastDensityRejectCount;

    public int CachedExclusionVolumeCount =>
        exclusionVolumes.Count;

    public int CachedDensityVolumeCount =>
        densityVolumes.Count;

    public int Generate(
        Action<float, string> progress = null)
    {
        return GenerateInternal(
            false,
            progress
        );
    }

    public int Regenerate(
        Action<float, string> progress = null)
    {
        return GenerateInternal(
            true,
            progress
        );
    }

    public int ClearGenerated()
    {
        if (
            database == null ||
            generatedIDRanges == null ||
            generatedIDRanges.Count == 0
        )
        {
            lastRemovedCount = 0;
            return 0;
        }

        database.BeginBatchMutation();

        int removed;

        try
        {
            removed =
                database
                    .RemoveInstancesByPersistentIDRanges(
                        generatedIDRanges
                    );
        }
        finally
        {
            database.EndBatchMutation();
        }

        generatedIDRanges.Clear();

        lastRemovedCount =
            removed;

        return removed;
    }

    private int GenerateInternal(
        bool clearExisting,
        Action<float, string> progress)
    {
        if (
            !CanGenerate(
                out string error
            )
        )
        {
            Debug.LogWarning(
                error,
                this
            );

            return 0;
        }

        if (biome.biomeMask == null)
        {
            biome.biomeMask =
                new VegetationBiomeMaskSettings();
        }

        biome.biomeMask.Sanitize();

        for (
            int i = 0;
            i < biome.layers.Count;
            i++
        )
        {
            biome.layers[i]
                ?.Sanitize();
        }

        Physics.SyncTransforms();

        generationWorldBounds =
            CalculateGenerationWorldBounds();

        CacheInfluenceVolumes();

        CollectUsedTerrainLayers();

        PrepareBiomeMaskRuntime();

        terrainLayerCaches.Clear();

        generatedGroupIndices.Clear();

        System.Diagnostics.Stopwatch stopwatch =
            System.Diagnostics
                .Stopwatch
                .StartNew();

        int generatedCount = 0;
        int removedCount = 0;

        int firstGeneratedID = -1;
        int lastGeneratedID = -1;

        lastBiomeMaskRejectCount = 0;
        lastTerrainLayerRejectCount = 0;
        lastTerrainDensityRejectCount = 0;
        lastParentInfluenceRejectCount = 0;
        lastExclusionRejectCount = 0;
        lastDensityRejectCount = 0;

        database.BeginBatchMutation();

        try
        {
            if (
                clearExisting &&
                generatedIDRanges != null &&
                generatedIDRanges.Count > 0
            )
            {
                removedCount =
                    database
                        .RemoveInstancesByPersistentIDRanges(
                            generatedIDRanges
                        );

                generatedIDRanges.Clear();
            }

            int layerCount =
                Mathf.Max(
                    1,
                    biome.layers.Count
                );

            for (
                int layerIndex = 0;
                layerIndex <
                biome.layers.Count;
                layerIndex++
            )
            {
                VegetationScatterLayer layer =
                    biome.layers[
                        layerIndex
                    ];

                if (layer == null)
                {
                    Debug.LogWarning(
                        $"VegetationProceduralGenerator: Biome '{biome.name}' Layer {layerIndex} is null.",
                        this
                    );

                    continue;
                }

                if (!layer.enabled)
                {
                    continue;
                }

                if (
                    layer.speciesGroup ==
                    null
                )
                {
                    Debug.LogWarning(
                        $"VegetationProceduralGenerator: Layer '{layer.layerName}' skipped because Species Group is null.",
                        this
                    );

                    continue;
                }

                if (
                    !layer
                        .speciesGroup
                        .HasValidSpecies
                )
                {
                    Debug.LogWarning(
                        $"VegetationProceduralGenerator: Layer '{layer.layerName}' skipped because Species Group '{layer.speciesGroup.name}' contains no valid VegetationSpecies.",
                        layer.speciesGroup
                    );

                    continue;
                }

                int layerGenerated =
                    GenerateLayer(
                        layer,
                        layerIndex,
                        layerCount,
                        progress,
                        ref firstGeneratedID,
                        ref lastGeneratedID
                    );

                generatedCount +=
                    layerGenerated;

                Debug.Log(
                    $"VegetationProceduralGenerator: Layer '{layer.layerName}' generated {layerGenerated:N0} instances.",
                    this
                );
            }
        }
        finally
        {
            database.EndBatchMutation();

            progress?.Invoke(
                1f,
                "Finished"
            );
        }

        if (
            firstGeneratedID >= 0 &&
            lastGeneratedID >=
            firstGeneratedID
        )
        {
            generatedIDRanges.Add(
                new Vector2Int(
                    firstGeneratedID,
                    lastGeneratedID
                )
            );
        }

        stopwatch.Stop();

        lastGeneratedCount =
            generatedCount;

        lastRemovedCount =
            removedCount;

        lastGenerationSeconds =
            (float)
            stopwatch
                .Elapsed
                .TotalSeconds;

        Debug.Log(
            $"VegetationProceduralGenerator finished. " +
            $"Generated={generatedCount:N0}, " +
            $"BiomeMaskRejected={lastBiomeMaskRejectCount:N0}, " +
            $"TerrainRejected={lastTerrainLayerRejectCount:N0}, " +
            $"TerrainDensityRejected={lastTerrainDensityRejectCount:N0}, " +
            $"ParentRejected={lastParentInfluenceRejectCount:N0}, " +
            $"Excluded={lastExclusionRejectCount:N0}, " +
            $"VolumeDensityRejected={lastDensityRejectCount:N0}.",
            this
        );

        return generatedCount;
    }

    private int GenerateLayer(
        VegetationScatterLayer layer,
        int layerIndex,
        int layerCount,
        Action<float, string> progress,
        ref int firstGeneratedID,
        ref int lastGeneratedID)
    {
        float area =
            Mathf.Max(
                0.01f,
                areaSize.x *
                areaSize.y
            );

        int candidateCount =
            Mathf.CeilToInt(
                area *
                layer.densityPerSquareMeter
            );

        if (candidateCount <= 0)
        {
            return 0;
        }

        if (
            candidateCount >
            HardCandidateLimitPerLayer
        )
        {
            Debug.LogWarning(
                $"VegetationProceduralGenerator: Layer '{layer.layerName}' requested {candidateCount:N0} candidates. Clamped to {HardCandidateLimitPerLayer:N0}.",
                this
            );

            candidateCount =
                HardCandidateLimitPerLayer;
        }

        int layerSeed =
            CombineSeed(
                seed,
                layer.seedOffset,
                0x41A7
            );

        System.Random random =
            new System.Random(
                layerSeed
            );

        Vector2 noiseOffsetA =
            GetSeedOffset(
                layerSeed,
                0x1171
            );

        Vector2 noiseOffsetB =
            GetSeedOffset(
                layerSeed,
                0x28F3
            );

        float spatialCellSize =
            Mathf.Max(
                0.05f,
                layer.minSpacing
            );

        LayerSpatialHash spatialHash =
            new LayerSpatialHash(
                spatialCellSize
            );

        int generated = 0;

        int progressInterval =
            Mathf.Max(
                1,
                candidateCount / 200
            );

        for (
            int candidateIndex = 0;
            candidateIndex <
            candidateCount;
            candidateIndex++
        )
        {
            if (
                candidateIndex %
                progressInterval == 0
            )
            {
                float localProgress =
                    candidateIndex /
                    (float)
                    candidateCount;

                float totalProgress =
                    (
                        layerIndex +
                        localProgress
                    ) /
                    Mathf.Max(
                        1f,
                        layerCount
                    );

                progress?.Invoke(
                    totalProgress,
                    $"{layer.layerName} {candidateIndex:N0}/{candidateCount:N0}"
                );
            }

            if (
                layer.maxInstances > 0 &&
                generated >=
                layer.maxInstances
            )
            {
                break;
            }

            Vector3 candidatePosition =
                GetRandomAreaPosition(
                    random
                );

            if (
                !PassDistribution(
                    layer,
                    candidatePosition,
                    random,
                    noiseOffsetA,
                    noiseOffsetB
                )
            )
            {
                continue;
            }

            float biomeMaskMultiplier =
                EvaluateBiomeMaskDensity(
                    layer,
                    candidatePosition
                );

            if (
                biomeMaskMultiplier <=
                0f
            )
            {
                lastBiomeMaskRejectCount++;
                continue;
            }

            if (
                biomeMaskMultiplier <
                    0.9999f &&
                (float)
                    random.NextDouble() >
                    biomeMaskMultiplier
            )
            {
                lastBiomeMaskRejectCount++;
                continue;
            }

            if (
                !TryFindGround(
                    candidatePosition,
                    out RaycastHit hit
                )
            )
            {
                continue;
            }

            float height =
                hit.point.y;

            if (
                height <
                    layer.heightRange.x ||
                height >
                    layer.heightRange.y
            )
            {
                continue;
            }

            float slope =
                Vector3.Angle(
                    hit.normal,
                    Vector3.up
                );

            if (
                slope <
                    layer.slopeRange.x ||
                slope >
                    layer.slopeRange.y
            )
            {
                continue;
            }

            if (
                !EvaluateTerrainLayerFilter(
                    layer,
                    hit,
                    out float
                        terrainDensityMultiplier
                )
            )
            {
                lastTerrainLayerRejectCount++;
                continue;
            }

            if (
                terrainDensityMultiplier <=
                0f
            )
            {
                lastTerrainDensityRejectCount++;
                continue;
            }

            if (
                terrainDensityMultiplier <
                    0.9999f &&
                (float)
                    random.NextDouble() >
                    terrainDensityMultiplier
            )
            {
                lastTerrainDensityRejectCount++;
                continue;
            }

            float parentMultiplier =
                EvaluateParentInfluence(
                    layer,
                    hit.point
                );

            if (
                parentMultiplier <= 0f
            )
            {
                lastParentInfluenceRejectCount++;
                continue;
            }

            if (
                parentMultiplier <
                    0.9999f &&
                (float)
                    random.NextDouble() >
                    parentMultiplier
            )
            {
                lastParentInfluenceRejectCount++;
                continue;
            }

            if (
                IsExcludedByVolume(
                    layer,
                    hit.point
                )
            )
            {
                lastExclusionRejectCount++;
                continue;
            }

            float volumeDensityMultiplier =
                EvaluateDensityVolumes(
                    layer,
                    hit.point
                );

            if (
                volumeDensityMultiplier <=
                0f
            )
            {
                lastDensityRejectCount++;
                continue;
            }

            if (
                volumeDensityMultiplier <
                    0.9999f &&
                (float)
                    random.NextDouble() >
                    volumeDensityMultiplier
            )
            {
                lastDensityRejectCount++;
                continue;
            }

            VegetationSpecies selectedSpecies =
                layer
                    .speciesGroup
                    .GetRandomSpecies(
                        random
                    );

            if (
                selectedSpecies == null
            )
            {
                continue;
            }

            float spacing =
                Mathf.Max(
                    layer.minSpacing,
                    selectedSpecies
                        .minSpacing
                );

            if (
                spacing > 0f &&
                spatialHash.HasPointNear(
                    hit.point,
                    spacing
                )
            )
            {
                continue;
            }

            if (
                spacing > 0f &&
                layer
                    .avoidExistingVegetation &&
                database
                    .TryFindNearestInstance(
                        hit.point,
                        spacing,
                        -1,
                        out _
                    )
            )
            {
                continue;
            }

            Vector3 position =
                hit.point;

            float surfaceOffset =
                RandomRange(
                    random,
                    layer
                        .surfaceOffsetRange
                        .x,
                    layer
                        .surfaceOffsetRange
                        .y
                );

            position +=
                hit.normal *
                surfaceOffset;

            Quaternion rotation =
                CreateRotation(
                    selectedSpecies,
                    hit.normal,
                    random
                );

            float speciesScaleMin =
                Mathf.Min(
                    selectedSpecies
                        .scaleRange.x,
                    selectedSpecies
                        .scaleRange.y
                );

            float speciesScaleMax =
                Mathf.Max(
                    selectedSpecies
                        .scaleRange.x,
                    selectedSpecies
                        .scaleRange.y
                );

            float speciesScale =
                RandomRange(
                    random,
                    speciesScaleMin,
                    speciesScaleMax
                );

            float layerScale =
                RandomRange(
                    random,
                    layer
                        .scaleMultiplierRange
                        .x,
                    layer
                        .scaleMultiplierRange
                        .y
                );

            float finalScale =
                Mathf.Max(
                    0.001f,
                    speciesScale *
                    layerScale
                );

            int persistentID =
                database.AddInstance(
                    selectedSpecies,
                    position,
                    rotation,
                    Vector3.one *
                    finalScale
                );

            if (
                persistentID < 0
            )
            {
                continue;
            }

            if (
                firstGeneratedID < 0
            )
            {
                firstGeneratedID =
                    persistentID;
            }

            lastGeneratedID =
                persistentID;

            spatialHash.Add(
                position
            );

            RegisterGeneratedGroupPosition(
                layer.speciesGroup,
                position
            );

            generated++;
        }

        return generated;
    }

    private void PrepareBiomeMaskRuntime()
    {
        runtimeBiomeMaskEnabled =
            biome != null &&
            biome.biomeMask != null &&
            biome.biomeMask.enabled;

        if (!runtimeBiomeMaskEnabled)
        {
            return;
        }

        int maskSeed =
            CombineSeed(
                seed,
                biome
                    .biomeMask
                    .seedOffset,
                0x6B31
            );

        biomeBaseOffset =
            GetSeedOffset(
                maskSeed,
                0x1337
            );

        biomeDetailOffset =
            GetSeedOffset(
                maskSeed,
                0x2749
            );

        biomeWarpOffsetA =
            GetSeedOffset(
                maskSeed,
                0x3811
            );

        biomeWarpOffsetB =
            GetSeedOffset(
                maskSeed,
                0x4927
            );
    }

    private float SampleBiomeMask(
        Vector3 worldPosition)
    {
        if (
            !runtimeBiomeMaskEnabled ||
            biome == null ||
            biome.biomeMask == null
        )
        {
            return 1f;
        }

        VegetationBiomeMaskSettings settings =
            biome.biomeMask;

        Vector2 worldXZ =
            new Vector2(
                worldPosition.x,
                worldPosition.z
            );

        float warpX =
            FractalNoise(
                worldXZ.x *
                    settings.warpScale +
                    biomeWarpOffsetA.x,

                worldXZ.y *
                    settings.warpScale +
                    biomeWarpOffsetA.y
            ) -
            0.5f;

        float warpY =
            FractalNoise(
                worldXZ.x *
                    settings.warpScale +
                    biomeWarpOffsetB.x,

                worldXZ.y *
                    settings.warpScale +
                    biomeWarpOffsetB.y
            ) -
            0.5f;

        Vector2 warpedPosition =
            worldXZ +
            new Vector2(
                warpX,
                warpY
            ) *
            settings.warpStrength;

        float largeScale =
            FractalNoise(
                warpedPosition.x *
                    settings.baseScale +
                    biomeBaseOffset.x,

                warpedPosition.y *
                    settings.baseScale +
                    biomeBaseOffset.y
            );

        float detail =
            FractalNoise(
                warpedPosition.x *
                    settings.detailScale +
                    biomeDetailOffset.x,

                warpedPosition.y *
                    settings.detailScale +
                    biomeDetailOffset.y
            );

        float value =
            largeScale +
            (
                detail -
                0.5f
            ) *
            settings.detailStrength;

        value =
            Mathf.Clamp01(
                value
            );

        value =
            Smooth01(
                value
            );

        if (settings.invert)
        {
            value =
                1f - value;
        }

        return value;
    }

    private float EvaluateBiomeMaskDensity(
        VegetationScatterLayer layer,
        Vector3 worldPosition)
    {
        if (
            !runtimeBiomeMaskEnabled ||
            layer == null ||
            layer.biomeMaskMode ==
                VegetationBiomeMaskMode.Ignore
        )
        {
            return 1f;
        }

        float mask =
            SampleBiomeMask(
                worldPosition
            );

        float gate = 1f;

        float threshold =
            layer.biomeMaskThreshold;

        float softness =
            Mathf.Max(
                0.001f,
                layer.biomeMaskSoftness
            );

        switch (
            layer.biomeMaskMode
        )
        {
            case VegetationBiomeMaskMode.High:
            {
                float start =
                    threshold -
                    softness;

                float end =
                    threshold +
                    softness;

                gate =
                    Smooth01(
                        Mathf.InverseLerp(
                            start,
                            end,
                            mask
                        )
                    );

                break;
            }

            case VegetationBiomeMaskMode.Low:
            {
                float start =
                    threshold -
                    softness;

                float end =
                    threshold +
                    softness;

                float highGate =
                    Smooth01(
                        Mathf.InverseLerp(
                            start,
                            end,
                            mask
                        )
                    );

                gate =
                    1f -
                    highGate;

                break;
            }

            case VegetationBiomeMaskMode.EdgeBand:
            {
                float distance =
                    Mathf.Abs(
                        mask -
                        threshold
                    );

                float width =
                    Mathf.Max(
                        0.001f,
                        layer
                            .biomeMaskEdgeWidth
                    );

                float edgeEnd =
                    width +
                    softness;

                gate =
                    1f -
                    Smooth01(
                        Mathf.InverseLerp(
                            width,
                            edgeEnd,
                            distance
                        )
                    );

                break;
            }
        }

        return
            Mathf.Lerp(
                layer
                    .biomeMaskDensityOutside,

                layer
                    .biomeMaskDensityInside,

                Mathf.Clamp01(
                    gate
                )
            );
    }

    private float EvaluateParentInfluence(
        VegetationScatterLayer layer,
        Vector3 worldPosition)
    {
        if (
            !layer.enableParentInfluence ||
            layer.parentSpeciesGroup ==
                null
        )
        {
            return 1f;
        }

        float minDistance =
            Mathf.Max(
                0f,
                layer.parentMinDistance
            );

        float maxDistance =
            Mathf.Max(
                minDistance + 0.01f,
                layer.parentMaxDistance
            );

        float falloff =
            Mathf.Max(
                0f,
                layer.parentEdgeFalloff
            );

        float searchDistance =
            maxDistance +
            falloff;

        if (
            !generatedGroupIndices
                .TryGetValue(
                    layer
                        .parentSpeciesGroup,
                    out ParentSpatialHash index
                )
        )
        {
            return
                layer
                    .parentDensityOutside;
        }

        if (
            !index
                .TryFindNearestDistance(
                    worldPosition,
                    searchDistance,
                    out float distance
                )
        )
        {
            return
                layer
                    .parentDensityOutside;
        }

        float innerGate;

        if (
            minDistance <=
            0.0001f
        )
        {
            innerGate = 1f;
        }
        else if (
            falloff <=
            0.0001f
        )
        {
            innerGate =
                distance >=
                minDistance
                    ? 1f
                    : 0f;
        }
        else
        {
            float innerStart =
                Mathf.Max(
                    0f,
                    minDistance -
                    falloff
                );

            float innerEnd =
                minDistance +
                falloff;

            innerGate =
                Smooth01(
                    Mathf.InverseLerp(
                        innerStart,
                        innerEnd,
                        distance
                    )
                );
        }

        float outerGate;

        if (
            falloff <=
            0.0001f
        )
        {
            outerGate =
                distance <=
                maxDistance
                    ? 1f
                    : 0f;
        }
        else
        {
            float outerStart =
                Mathf.Max(
                    minDistance,
                    maxDistance -
                    falloff
                );

            float outerEnd =
                maxDistance +
                falloff;

            outerGate =
                1f -
                Smooth01(
                    Mathf.InverseLerp(
                        outerStart,
                        outerEnd,
                        distance
                    )
                );
        }

        float bandWeight =
            Mathf.Clamp01(
                innerGate *
                outerGate
            );

        return
            Mathf.Lerp(
                layer.parentDensityOutside,
                layer.parentDensityInside,
                bandWeight
            );
    }

    private void RegisterGeneratedGroupPosition(
        VegetationSpeciesGroup group,
        Vector3 position)
    {
        if (group == null)
        {
            return;
        }

        if (
            !generatedGroupIndices
                .TryGetValue(
                    group,
                    out ParentSpatialHash index
                )
        )
        {
            index =
                new ParentSpatialHash(
                    ParentSpatialCellSize
                );

            generatedGroupIndices.Add(
                group,
                index
            );
        }

        index.Add(
            position
        );
    }

    private bool EvaluateTerrainLayerFilter(
        VegetationScatterLayer layer,
        RaycastHit hit,
        out float densityMultiplier)
    {
        densityMultiplier = 1f;

        if (
            !layer.enableTerrainLayerFilter ||
            layer.terrainLayerRules == null ||
            layer.terrainLayerRules.Count == 0
        )
        {
            return true;
        }

        Terrain terrain = null;

        if (hit.collider != null)
        {
            terrain =
                hit.collider
                    .GetComponent<Terrain>();
        }

        if (terrain == null)
        {
            return
                !layer
                    .requireTerrainForLayerFilter;
        }

        TerrainLayerCache cache =
            GetTerrainLayerCache(
                terrain
            );

        if (cache == null)
        {
            return
                !layer
                    .requireTerrainForLayerFilter;
        }

        bool hasIncludeRule = false;
        bool includeMatched = false;

        for (
            int i = 0;
            i <
            layer
                .terrainLayerRules
                .Count;
            i++
        )
        {
            VegetationTerrainLayerRule rule =
                layer
                    .terrainLayerRules[i];

            if (
                rule == null ||
                !rule.enabled ||
                rule.terrainLayer == null
            )
            {
                continue;
            }

            float weight =
                cache.Sample(
                    rule.terrainLayer,
                    hit.point
                );

            switch (rule.mode)
            {
                case VegetationTerrainLayerRuleMode.Include:

                    hasIncludeRule = true;

                    if (
                        weight >=
                        rule.threshold
                    )
                    {
                        includeMatched = true;
                    }

                    break;

                case VegetationTerrainLayerRuleMode.Exclude:

                    if (
                        weight >=
                        rule.threshold
                    )
                    {
                        return false;
                    }

                    break;

                case VegetationTerrainLayerRuleMode.Density:

                    float range =
                        Mathf.Max(
                            rule.densityBlendEnd -
                            rule.densityBlendStart,
                            0.001f
                        );

                    float t =
                        Mathf.Clamp01(
                            (
                                weight -
                                rule.densityBlendStart
                            ) /
                            range
                        );

                    t =
                        Smooth01(
                            t
                        );

                    float ruleMultiplier =
                        Mathf.Lerp(
                            1f,
                            rule
                                .densityMultiplierAtFullWeight,
                            t
                        );

                    densityMultiplier *=
                        ruleMultiplier;

                    break;
            }
        }

        if (
            hasIncludeRule &&
            !includeMatched
        )
        {
            return false;
        }

        densityMultiplier =
            Mathf.Clamp01(
                densityMultiplier
            );

        return true;
    }

    private TerrainLayerCache GetTerrainLayerCache(
        Terrain terrain)
    {
        if (terrain == null)
        {
            return null;
        }

        if (
            terrainLayerCaches
                .TryGetValue(
                    terrain,
                    out TerrainLayerCache cache
                )
        )
        {
            return cache;
        }

        cache =
            new TerrainLayerCache(
                terrain,
                generationWorldBounds,
                usedTerrainLayers
            );

        terrainLayerCaches.Add(
            terrain,
            cache
        );

        return cache;
    }

    private void CollectUsedTerrainLayers()
    {
        usedTerrainLayers.Clear();

        if (
            biome == null ||
            biome.layers == null
        )
        {
            return;
        }

        for (
            int layerIndex = 0;
            layerIndex <
            biome.layers.Count;
            layerIndex++
        )
        {
            VegetationScatterLayer layer =
                biome.layers[
                    layerIndex
                ];

            if (
                layer == null ||
                !layer.enabled ||
                !layer
                    .enableTerrainLayerFilter ||
                layer
                    .terrainLayerRules ==
                    null
            )
            {
                continue;
            }

            for (
                int ruleIndex = 0;
                ruleIndex <
                layer
                    .terrainLayerRules
                    .Count;
                ruleIndex++
            )
            {
                VegetationTerrainLayerRule rule =
                    layer
                        .terrainLayerRules[
                            ruleIndex
                        ];

                if (
                    rule == null ||
                    !rule.enabled ||
                    rule.terrainLayer ==
                        null
                )
                {
                    continue;
                }

                usedTerrainLayers.Add(
                    rule.terrainLayer
                );
            }
        }
    }

    private Bounds CalculateGenerationWorldBounds()
    {
        float halfX =
            areaSize.x *
            0.5f;

        float halfZ =
            areaSize.y *
            0.5f;

        Quaternion rotation =
            useTransformYRotation
                ? Quaternion.Euler(
                    0f,
                    transform.eulerAngles.y,
                    0f
                )
                : Quaternion.identity;

        Vector3 p0 =
            transform.position +
            rotation *
            new Vector3(
                -halfX,
                0f,
                -halfZ
            );

        Vector3 p1 =
            transform.position +
            rotation *
            new Vector3(
                -halfX,
                0f,
                halfZ
            );

        Vector3 p2 =
            transform.position +
            rotation *
            new Vector3(
                halfX,
                0f,
                -halfZ
            );

        Vector3 p3 =
            transform.position +
            rotation *
            new Vector3(
                halfX,
                0f,
                halfZ
            );

        Bounds bounds =
            new Bounds(
                p0,
                Vector3.zero
            );

        bounds.Encapsulate(p1);
        bounds.Encapsulate(p2);
        bounds.Encapsulate(p3);

        bounds.Expand(
            new Vector3(
                2f,
                rayHeight +
                rayDistance,
                2f
            )
        );

        return bounds;
    }

    private void CacheInfluenceVolumes()
    {
        exclusionVolumes.Clear();
        densityVolumes.Clear();

        if (!useInfluenceVolumes)
        {
            return;
        }

        VegetationExclusionVolume[]
            allExclusionVolumes =
                FindObjectsOfType<
                    VegetationExclusionVolume
                >(true);

        for (
            int i = 0;
            i <
            allExclusionVolumes.Length;
            i++
        )
        {
            VegetationExclusionVolume volume =
                allExclusionVolumes[i];

            if (
                volume == null ||
                !volume.isActiveAndEnabled ||
                volume.gameObject.scene !=
                    gameObject.scene
            )
            {
                continue;
            }

            exclusionVolumes.Add(
                volume
            );
        }

        VegetationDensityVolume[]
            allDensityVolumes =
                FindObjectsOfType<
                    VegetationDensityVolume
                >(true);

        for (
            int i = 0;
            i <
            allDensityVolumes.Length;
            i++
        )
        {
            VegetationDensityVolume volume =
                allDensityVolumes[i];

            if (
                volume == null ||
                !volume.isActiveAndEnabled ||
                volume.gameObject.scene !=
                    gameObject.scene
            )
            {
                continue;
            }

            densityVolumes.Add(
                volume
            );
        }
    }

    private bool IsExcludedByVolume(
        VegetationScatterLayer layer,
        Vector3 worldPosition)
    {
        if (!useInfluenceVolumes)
        {
            return false;
        }

        for (
            int i = 0;
            i <
            exclusionVolumes.Count;
            i++
        )
        {
            VegetationExclusionVolume volume =
                exclusionVolumes[i];

            if (
                volume == null ||
                !volume.AffectsLayer(
                    layer
                )
            )
            {
                continue;
            }

            if (
                volume.Contains(
                    worldPosition
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private float EvaluateDensityVolumes(
        VegetationScatterLayer layer,
        Vector3 worldPosition)
    {
        if (!useInfluenceVolumes)
        {
            return 1f;
        }

        float multiplier = 1f;

        for (
            int i = 0;
            i <
            densityVolumes.Count;
            i++
        )
        {
            VegetationDensityVolume volume =
                densityVolumes[i];

            if (
                volume == null ||
                !volume.AffectsLayer(
                    layer
                )
            )
            {
                continue;
            }

            multiplier *=
                volume
                    .EvaluateMultiplier(
                        worldPosition
                    );

            if (
                multiplier <=
                0.0001f
            )
            {
                return 0f;
            }
        }

        return
            Mathf.Clamp01(
                multiplier
            );
    }

    private bool PassDistribution(
        VegetationScatterLayer layer,
        Vector3 position,
        System.Random random,
        Vector2 noiseOffsetA,
        Vector2 noiseOffsetB)
    {
        if (
            layer.distributionMode ==
            VegetationScatterDistributionMode.Uniform
        )
        {
            return true;
        }

        if (
            layer.distributionMode ==
            VegetationScatterDistributionMode.Noise
        )
        {
            float noise =
                FractalNoise(
                    position.x *
                        layer.noiseScale +
                        noiseOffsetA.x,

                    position.z *
                        layer.noiseScale +
                        noiseOffsetA.y
                );

            float edgeEnd =
                Mathf.Min(
                    1f,
                    layer.noiseThreshold +
                    0.20f
                );

            float noiseMask =
                Mathf.InverseLerp(
                    layer.noiseThreshold,
                    Mathf.Max(
                        layer.noiseThreshold +
                        0.001f,
                        edgeEnd
                    ),
                    noise
                );

            noiseMask =
                Smooth01(
                    noiseMask
                );

            float acceptance =
                Mathf.Lerp(
                    1f,
                    noiseMask,
                    layer.noiseStrength
                );

            return
                (float)
                    random.NextDouble() <=
                acceptance;
        }

        float clusterFrequency =
            1f /
            Mathf.Max(
                0.1f,
                layer.clusterSize
            );

        float clusterNoise =
            FractalNoise(
                position.x *
                    clusterFrequency +
                    noiseOffsetA.x,

                position.z *
                    clusterFrequency +
                    noiseOffsetA.y
            );

        float halfSoftness =
            layer.clusterEdgeSoftness *
            0.5f;

        float clusterMask =
            Mathf.InverseLerp(
                layer.clusterThreshold -
                    halfSoftness,

                layer.clusterThreshold +
                    halfSoftness,

                clusterNoise
            );

        clusterMask =
            Smooth01(
                clusterMask
            );

        float detailNoise =
            FractalNoise(
                position.x *
                    layer.noiseScale +
                    noiseOffsetB.x,

                position.z *
                    layer.noiseScale +
                    noiseOffsetB.y
            );

        float detailMask =
            Mathf.Lerp(
                1f,
                detailNoise,
                layer.noiseStrength
            );

        float finalAcceptance =
            Mathf.Clamp01(
                clusterMask *
                detailMask
            );

        return
            (float)
                random.NextDouble() <=
            finalAcceptance;
    }

    private bool TryFindGround(
        Vector3 candidate,
        out RaycastHit hit)
    {
        Vector3 origin =
            new Vector3(
                candidate.x,
                transform.position.y +
                    rayHeight,
                candidate.z
            );

        return
            Physics.Raycast(
                origin,
                Vector3.down,
                out hit,
                rayDistance,
                groundMask,
                QueryTriggerInteraction.Ignore
            );
    }

    private Vector3 GetRandomAreaPosition(
        System.Random random)
    {
        float x =
            RandomRange(
                random,
                -areaSize.x * 0.5f,
                areaSize.x * 0.5f
            );

        float z =
            RandomRange(
                random,
                -areaSize.y * 0.5f,
                areaSize.y * 0.5f
            );

        Vector3 local =
            new Vector3(
                x,
                0f,
                z
            );

        Quaternion rotation =
            useTransformYRotation
                ? Quaternion.Euler(
                    0f,
                    transform.eulerAngles.y,
                    0f
                )
                : Quaternion.identity;

        return
            transform.position +
            rotation *
            local;
    }

    private static Quaternion CreateRotation(
        VegetationSpecies species,
        Vector3 surfaceNormal,
        System.Random random)
    {
        float yaw =
            species.randomYRotation
                ? (float)
                    random.NextDouble() *
                    360f
                : 0f;

        Quaternion yawRotation =
            Quaternion.Euler(
                0f,
                yaw,
                0f
            );

        if (
            !species
                .alignToTerrainNormal
        )
        {
            return yawRotation;
        }

        Quaternion normalRotation =
            Quaternion
                .FromToRotation(
                    Vector3.up,
                    surfaceNormal
                );

        return
            normalRotation *
            yawRotation;
    }

    private bool CanGenerate(
        out string error)
    {
        if (database == null)
        {
            error =
                "VegetationProceduralGenerator: Database is null.";

            return false;
        }

        if (biome == null)
        {
            error =
                "VegetationProceduralGenerator: Biome Profile is null.";

            return false;
        }

        if (
            biome.layers == null ||
            biome.layers.Count == 0
        )
        {
            error =
                "VegetationProceduralGenerator: Biome Profile has no Scatter Layers.";

            return false;
        }

        if (
            areaSize.x <= 0f ||
            areaSize.y <= 0f
        )
        {
            error =
                "VegetationProceduralGenerator: Area Size must be greater than zero.";

            return false;
        }

        if (groundMask.value == 0)
        {
            error =
                "VegetationProceduralGenerator: Ground Mask is empty.";

            return false;
        }

        error = null;

        return true;
    }

    private void OnValidate()
    {
        areaSize.x =
            Mathf.Max(
                0.1f,
                areaSize.x
            );

        areaSize.y =
            Mathf.Max(
                0.1f,
                areaSize.y
            );

        rayHeight =
            Mathf.Max(
                1f,
                rayHeight
            );

        rayDistance =
            Mathf.Max(
                1f,
                rayDistance
            );
    }

    private void OnDrawGizmosSelected()
    {
        Quaternion rotation =
            useTransformYRotation
                ? Quaternion.Euler(
                    0f,
                    transform.eulerAngles.y,
                    0f
                )
                : Quaternion.identity;

        Matrix4x4 oldMatrix =
            Gizmos.matrix;

        Color oldColor =
            Gizmos.color;

        Gizmos.matrix =
            Matrix4x4.TRS(
                transform.position,
                rotation,
                Vector3.one
            );

        Gizmos.color =
            new Color(
                0.2f,
                0.9f,
                0.35f,
                0.85f
            );

        Gizmos.DrawWireCube(
            Vector3.zero,
            new Vector3(
                areaSize.x,
                0.2f,
                areaSize.y
            )
        );

        Gizmos.matrix =
            oldMatrix;

        Gizmos.color =
            oldColor;
    }

    private static float FractalNoise(
        float x,
        float y)
    {
        float value = 0f;
        float amplitude = 0.5714286f;
        float frequency = 1f;
        float amplitudeSum = 0f;

        for (
            int octave = 0;
            octave < 3;
            octave++
        )
        {
            value +=
                Mathf.PerlinNoise(
                    x * frequency,
                    y * frequency
                ) *
                amplitude;

            amplitudeSum +=
                amplitude;

            frequency *=
                2.03f;

            amplitude *=
                0.5f;
        }

        if (
            amplitudeSum <= 0f
        )
        {
            return 0f;
        }

        return
            value /
            amplitudeSum;
    }

    private static float Smooth01(
        float value)
    {
        value =
            Mathf.Clamp01(
                value
            );

        return
            value *
            value *
            (3f - 2f * value);
    }

    private static float RandomRange(
        System.Random random,
        float min,
        float max)
    {
        return
            Mathf.Lerp(
                min,
                max,
                (float)
                    random.NextDouble()
            );
    }

    private static int CombineSeed(
        int a,
        int b,
        int c)
    {
        unchecked
        {
            int hash = 17;

            hash =
                hash * 31 + a;

            hash =
                hash * 31 + b;

            hash =
                hash * 31 + c;

            return hash;
        }
    }

    private static Vector2 GetSeedOffset(
        int seed,
        int salt)
    {
        unchecked
        {
            uint hashA =
                HashUInt(
                    (uint)
                    (
                        seed ^
                        salt
                    )
                );

            uint hashB =
                HashUInt(
                    hashA ^
                    0x9E3779B9u
                );

            float x =
                (
                    hashA &
                    0x00FFFFFFu
                ) /
                16777215f;

            float y =
                (
                    hashB &
                    0x00FFFFFFu
                ) /
                16777215f;

            return
                new Vector2(
                    x * 10000f,
                    y * 10000f
                );
        }
    }

    private static uint HashUInt(
        uint value)
    {
        value ^=
            value >> 16;

        value *=
            0x7FEB352Du;

        value ^=
            value >> 15;

        value *=
            0x846CA68Bu;

        value ^=
            value >> 16;

        return value;
    }

    private sealed class TerrainLayerCache
    {
        private readonly Terrain terrain;
        private readonly TerrainData terrainData;
        private readonly Vector3 terrainPosition;

        private readonly int alphamapWidth;
        private readonly int alphamapHeight;

        private readonly int startX;
        private readonly int startY;

        private readonly int cachedWidth;
        private readonly int cachedHeight;

        private readonly Dictionary<
            TerrainLayer,
            float[,]
        > channels =
            new Dictionary<
                TerrainLayer,
                float[,]
            >();

        public TerrainLayerCache(
            Terrain terrain,
            Bounds generationBounds,
            HashSet<TerrainLayer>
                requestedLayers)
        {
            this.terrain =
                terrain;

            if (
                terrain == null ||
                terrain.terrainData ==
                    null
            )
            {
                return;
            }

            terrainData =
                terrain.terrainData;

            terrainPosition =
                terrain
                    .transform
                    .position;

            alphamapWidth =
                terrainData
                    .alphamapWidth;

            alphamapHeight =
                terrainData
                    .alphamapHeight;

            if (
                alphamapWidth <= 0 ||
                alphamapHeight <= 0
            )
            {
                return;
            }

            Vector3 terrainSize =
                terrainData.size;

            float worldMinX =
                Mathf.Max(
                    generationBounds
                        .min.x,
                    terrainPosition.x
                );

            float worldMaxX =
                Mathf.Min(
                    generationBounds
                        .max.x,
                    terrainPosition.x +
                    terrainSize.x
                );

            float worldMinZ =
                Mathf.Max(
                    generationBounds
                        .min.z,
                    terrainPosition.z
                );

            float worldMaxZ =
                Mathf.Min(
                    generationBounds
                        .max.z,
                    terrainPosition.z +
                    terrainSize.z
                );

            if (
                worldMaxX <
                    worldMinX ||
                worldMaxZ <
                    worldMinZ
            )
            {
                return;
            }

            float minU =
                Mathf.Clamp01(
                    (
                        worldMinX -
                        terrainPosition.x
                    ) /
                    Mathf.Max(
                        terrainSize.x,
                        0.001f
                    )
                );

            float maxU =
                Mathf.Clamp01(
                    (
                        worldMaxX -
                        terrainPosition.x
                    ) /
                    Mathf.Max(
                        terrainSize.x,
                        0.001f
                    )
                );

            float minV =
                Mathf.Clamp01(
                    (
                        worldMinZ -
                        terrainPosition.z
                    ) /
                    Mathf.Max(
                        terrainSize.z,
                        0.001f
                    )
                );

            float maxV =
                Mathf.Clamp01(
                    (
                        worldMaxZ -
                        terrainPosition.z
                    ) /
                    Mathf.Max(
                        terrainSize.z,
                        0.001f
                    )
                );

            startX =
                Mathf.Clamp(
                    Mathf.FloorToInt(
                        minU *
                        (
                            alphamapWidth -
                            1
                        )
                    ) -
                    1,
                    0,
                    alphamapWidth -
                    1
                );

            startY =
                Mathf.Clamp(
                    Mathf.FloorToInt(
                        minV *
                        (
                            alphamapHeight -
                            1
                        )
                    ) -
                    1,
                    0,
                    alphamapHeight -
                    1
                );

            int endX =
                Mathf.Clamp(
                    Mathf.CeilToInt(
                        maxU *
                        (
                            alphamapWidth -
                            1
                        )
                    ) +
                    1,
                    0,
                    alphamapWidth -
                    1
                );

            int endY =
                Mathf.Clamp(
                    Mathf.CeilToInt(
                        maxV *
                        (
                            alphamapHeight -
                            1
                        )
                    ) +
                    1,
                    0,
                    alphamapHeight -
                    1
                );

            cachedWidth =
                Mathf.Max(
                    1,
                    endX -
                    startX +
                    1
                );

            cachedHeight =
                Mathf.Max(
                    1,
                    endY -
                    startY +
                    1
                );

            float[,,] source =
                terrainData
                    .GetAlphamaps(
                        startX,
                        startY,
                        cachedWidth,
                        cachedHeight
                    );

            TerrainLayer[]
                terrainLayers =
                    terrainData
                        .terrainLayers;

            if (
                terrainLayers == null ||
                requestedLayers == null
            )
            {
                return;
            }

            foreach (
                TerrainLayer requestedLayer
                in requestedLayers
            )
            {
                if (
                    requestedLayer ==
                    null
                )
                {
                    continue;
                }

                int channelIndex = -1;

                for (
                    int i = 0;
                    i <
                    terrainLayers.Length;
                    i++
                )
                {
                    if (
                        terrainLayers[i] ==
                        requestedLayer
                    )
                    {
                        channelIndex =
                            i;

                        break;
                    }
                }

                if (
                    channelIndex < 0
                )
                {
                    continue;
                }

                float[,] channel =
                    new float[
                        cachedHeight,
                        cachedWidth
                    ];

                for (
                    int y = 0;
                    y <
                    cachedHeight;
                    y++
                )
                {
                    for (
                        int x = 0;
                        x <
                        cachedWidth;
                        x++
                    )
                    {
                        channel[y, x] =
                            source[
                                y,
                                x,
                                channelIndex
                            ];
                    }
                }

                channels[
                    requestedLayer
                ] = channel;
            }
        }

        public float Sample(
            TerrainLayer terrainLayer,
            Vector3 worldPosition)
        {
            if (
                terrainLayer == null ||
                terrain == null ||
                terrainData == null
            )
            {
                return 0f;
            }

            if (
                !channels.TryGetValue(
                    terrainLayer,
                    out float[,]
                        channel
                )
            )
            {
                return 0f;
            }

            Vector3 terrainSize =
                terrainData.size;

            float u =
                (
                    worldPosition.x -
                    terrainPosition.x
                ) /
                Mathf.Max(
                    terrainSize.x,
                    0.001f
                );

            float v =
                (
                    worldPosition.z -
                    terrainPosition.z
                ) /
                Mathf.Max(
                    terrainSize.z,
                    0.001f
                );

            if (
                u < 0f ||
                u > 1f ||
                v < 0f ||
                v > 1f
            )
            {
                return 0f;
            }

            float mapX =
                u *
                (
                    alphamapWidth -
                    1
                ) -
                startX;

            float mapY =
                v *
                (
                    alphamapHeight -
                    1
                ) -
                startY;

            mapX =
                Mathf.Clamp(
                    mapX,
                    0f,
                    cachedWidth -
                    1
                );

            mapY =
                Mathf.Clamp(
                    mapY,
                    0f,
                    cachedHeight -
                    1
                );

            int x0 =
                Mathf.FloorToInt(
                    mapX
                );

            int y0 =
                Mathf.FloorToInt(
                    mapY
                );

            int x1 =
                Mathf.Min(
                    x0 + 1,
                    cachedWidth - 1
                );

            int y1 =
                Mathf.Min(
                    y0 + 1,
                    cachedHeight - 1
                );

            float tx =
                mapX -
                x0;

            float ty =
                mapY -
                y0;

            float a =
                Mathf.Lerp(
                    channel[y0, x0],
                    channel[y0, x1],
                    tx
                );

            float b =
                Mathf.Lerp(
                    channel[y1, x0],
                    channel[y1, x1],
                    tx
                );

            return
                Mathf.Lerp(
                    a,
                    b,
                    ty
                );
        }
    }

    private sealed class ParentSpatialHash
    {
        private readonly float cellSize;

        private readonly Dictionary<
            Vector2Int,
            List<Vector2>
        > cells =
            new Dictionary<
                Vector2Int,
                List<Vector2>
            >();

        public ParentSpatialHash(
            float cellSize)
        {
            this.cellSize =
                Mathf.Max(
                    0.1f,
                    cellSize
                );
        }

        public void Add(
            Vector3 position)
        {
            Vector2Int coordinate =
                GetCoordinate(
                    position
                );

            if (
                !cells.TryGetValue(
                    coordinate,
                    out List<Vector2> list
                )
            )
            {
                list =
                    new List<Vector2>();

                cells.Add(
                    coordinate,
                    list
                );
            }

            list.Add(
                new Vector2(
                    position.x,
                    position.z
                )
            );
        }

        public bool TryFindNearestDistance(
            Vector3 position,
            float maxDistance,
            out float nearestDistance)
        {
            nearestDistance =
                float.MaxValue;

            if (
                maxDistance <= 0f
            )
            {
                return false;
            }

            Vector2Int center =
                GetCoordinate(
                    position
                );

            int cellRadius =
                Mathf.CeilToInt(
                    maxDistance /
                    cellSize
                );

            float maxDistanceSqr =
                maxDistance *
                maxDistance;

            float nearestSqr =
                maxDistanceSqr;

            bool found = false;

            for (
                int x =
                    center.x -
                    cellRadius;
                x <=
                    center.x +
                    cellRadius;
                x++
            )
            {
                for (
                    int z =
                        center.y -
                        cellRadius;
                    z <=
                        center.y +
                        cellRadius;
                    z++
                )
                {
                    Vector2Int coordinate =
                        new Vector2Int(
                            x,
                            z
                        );

                    if (
                        !cells.TryGetValue(
                            coordinate,
                            out List<Vector2>
                                list
                        )
                    )
                    {
                        continue;
                    }

                    for (
                        int i = 0;
                        i <
                        list.Count;
                        i++
                    )
                    {
                        float dx =
                            list[i].x -
                            position.x;

                        float dz =
                            list[i].y -
                            position.z;

                        float sqrDistance =
                            dx * dx +
                            dz * dz;

                        if (
                            sqrDistance <=
                            nearestSqr
                        )
                        {
                            nearestSqr =
                                sqrDistance;

                            found = true;
                        }
                    }
                }
            }

            if (!found)
            {
                return false;
            }

            nearestDistance =
                Mathf.Sqrt(
                    nearestSqr
                );

            return true;
        }

        private Vector2Int GetCoordinate(
            Vector3 position)
        {
            return
                new Vector2Int(
                    Mathf.FloorToInt(
                        position.x /
                        cellSize
                    ),
                    Mathf.FloorToInt(
                        position.z /
                        cellSize
                    )
                );
        }
    }

    private sealed class LayerSpatialHash
    {
        private readonly float cellSize;

        private readonly Dictionary<
            Vector2Int,
            List<Vector2>
        > cells =
            new Dictionary<
                Vector2Int,
                List<Vector2>
            >();

        public LayerSpatialHash(
            float cellSize)
        {
            this.cellSize =
                Mathf.Max(
                    0.01f,
                    cellSize
                );
        }

        public void Add(
            Vector3 position)
        {
            Vector2Int coordinate =
                GetCoordinate(
                    position
                );

            if (
                !cells.TryGetValue(
                    coordinate,
                    out List<Vector2>
                        list
                )
            )
            {
                list =
                    new List<Vector2>();

                cells.Add(
                    coordinate,
                    list
                );
            }

            list.Add(
                new Vector2(
                    position.x,
                    position.z
                )
            );
        }

        public bool HasPointNear(
            Vector3 position,
            float radius)
        {
            if (radius <= 0f)
            {
                return false;
            }

            Vector2Int center =
                GetCoordinate(
                    position
                );

            int cellRadius =
                Mathf.CeilToInt(
                    radius /
                    cellSize
                );

            float radiusSqr =
                radius *
                radius;

            for (
                int x =
                    center.x -
                    cellRadius;
                x <=
                    center.x +
                    cellRadius;
                x++
            )
            {
                for (
                    int z =
                        center.y -
                        cellRadius;
                    z <=
                        center.y +
                        cellRadius;
                    z++
                )
                {
                    Vector2Int coordinate =
                        new Vector2Int(
                            x,
                            z
                        );

                    if (
                        !cells.TryGetValue(
                            coordinate,
                            out List<Vector2>
                                list
                        )
                    )
                    {
                        continue;
                    }

                    for (
                        int i = 0;
                        i <
                        list.Count;
                        i++
                    )
                    {
                        float dx =
                            list[i].x -
                            position.x;

                        float dz =
                            list[i].y -
                            position.z;

                        if (
                            dx * dx +
                            dz * dz <
                            radiusSqr
                        )
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private Vector2Int GetCoordinate(
            Vector3 position)
        {
            return
                new Vector2Int(
                    Mathf.FloorToInt(
                        position.x /
                        cellSize
                    ),
                    Mathf.FloorToInt(
                        position.z /
                        cellSize
                    )
                );
        }
    }
}