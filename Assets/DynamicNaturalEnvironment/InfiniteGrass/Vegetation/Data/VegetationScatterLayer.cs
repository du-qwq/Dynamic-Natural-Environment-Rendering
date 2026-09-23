using System;
using System.Collections.Generic;
using UnityEngine;

public enum VegetationScatterDistributionMode
{
    Uniform,
    Noise,
    Clustered
}

public enum VegetationTerrainLayerRuleMode
{
    Include,
    Exclude,
    Density
}

public enum VegetationBiomeMaskMode
{
    Ignore,
    High,
    Low,
    EdgeBand
}

[Serializable]
public class VegetationTerrainLayerRule
{
    public bool enabled = true;

    public TerrainLayer terrainLayer;

    public VegetationTerrainLayerRuleMode mode =
        VegetationTerrainLayerRuleMode.Exclude;

    [Range(0f, 1f)]
    public float threshold = 0.25f;

    [Range(0f, 1f)]
    public float densityBlendStart = 0.05f;

    [Range(0f, 1f)]
    public float densityBlendEnd = 0.65f;

    [Range(0f, 1f)]
    public float densityMultiplierAtFullWeight = 0f;

    public void Sanitize()
    {
        threshold = Mathf.Clamp01(threshold);
        densityBlendStart = Mathf.Clamp01(densityBlendStart);
        densityBlendEnd = Mathf.Clamp01(densityBlendEnd);
        densityMultiplierAtFullWeight = Mathf.Clamp01(densityMultiplierAtFullWeight);

        if (densityBlendStart > densityBlendEnd)
        {
            float temp = densityBlendStart;
            densityBlendStart = densityBlendEnd;
            densityBlendEnd = temp;
        }

        if (Mathf.Abs(densityBlendEnd - densityBlendStart) < 0.001f)
        {
            densityBlendEnd =
                Mathf.Min(
                    1f,
                    densityBlendStart + 0.001f
                );
        }
    }
}

[Serializable]
public class VegetationScatterLayer
{
    [Header("Layer")]
    public string layerName = "New Layer";
    public bool enabled = true;
    public VegetationSpeciesGroup speciesGroup;

    [Header("Density")]
    [Min(0f)]
    public float densityPerSquareMeter = 0.05f;

    [Min(0f)]
    public float minSpacing = 1f;

    public bool avoidExistingVegetation = true;

    [Min(0)]
    public int maxInstances = 0;

    [Header("Placement Filter")]
    public Vector2 heightRange =
        new Vector2(
            -1000f,
            10000f
        );

    public Vector2 slopeRange =
        new Vector2(
            0f,
            45f
        );

    [Header("Biome Mask")]
    [Tooltip("决定这个Layer如何响应Biome共享的大尺度Mask")]
    public VegetationBiomeMaskMode biomeMaskMode =
        VegetationBiomeMaskMode.Ignore;

    [Range(0f, 1f)]
    [Tooltip("High/Low模式的森林/空地分界值；EdgeBand模式的林缘中心")]
    public float biomeMaskThreshold = 0.50f;

    [Range(0.001f, 0.5f)]
    [Tooltip("High/Low模式下阈值附近的柔和过渡")]
    public float biomeMaskSoftness = 0.10f;

    [Range(0.001f, 0.5f)]
    [Tooltip("EdgeBand模式下林缘带的半宽")]
    public float biomeMaskEdgeWidth = 0.08f;

    [Range(0f, 1f)]
    [Tooltip("处于偏好区域时的密度倍率")]
    public float biomeMaskDensityInside = 1f;

    [Range(0f, 1f)]
    [Tooltip("偏好区域之外的密度倍率")]
    public float biomeMaskDensityOutside = 0.05f;

    [Header("Terrain Layer Filter")]
    public bool enableTerrainLayerFilter = false;

    public bool requireTerrainForLayerFilter = false;

    public List<VegetationTerrainLayerRule> terrainLayerRules =
        new List<VegetationTerrainLayerRule>();

    [Header("Parent Influence")]
    public bool enableParentInfluence = false;

    public VegetationSpeciesGroup parentSpeciesGroup;

    [Min(0f)]
    public float parentMinDistance = 1.5f;

    [Min(0.01f)]
    public float parentMaxDistance = 8f;

    [Min(0f)]
    public float parentEdgeFalloff = 1.5f;

    [Range(0f, 1f)]
    public float parentDensityInside = 1f;

    [Range(0f, 1f)]
    public float parentDensityOutside = 0.05f;

    [Header("Transform")]
    public Vector2 scaleMultiplierRange =
        Vector2.one;

    public Vector2 surfaceOffsetRange =
        Vector2.zero;

    [Header("Distribution")]
    public VegetationScatterDistributionMode distributionMode =
        VegetationScatterDistributionMode.Uniform;

    [Min(0.0001f)]
    public float noiseScale = 0.02f;

    [Range(0f, 1f)]
    public float noiseThreshold = 0.35f;

    [Range(0f, 1f)]
    public float noiseStrength = 1f;

    [Min(0.1f)]
    public float clusterSize = 20f;

    [Range(0f, 1f)]
    public float clusterThreshold = 0.48f;

    [Range(0.01f, 0.5f)]
    public float clusterEdgeSoftness = 0.15f;

    [Header("Seed")]
    public int seedOffset = 1009;

    public void Sanitize()
    {
        densityPerSquareMeter =
            Mathf.Max(
                0f,
                densityPerSquareMeter
            );

        minSpacing =
            Mathf.Max(
                0f,
                minSpacing
            );

        maxInstances =
            Mathf.Max(
                0,
                maxInstances
            );

        if (heightRange.x > heightRange.y)
        {
            float temp = heightRange.x;
            heightRange.x = heightRange.y;
            heightRange.y = temp;
        }

        slopeRange.x =
            Mathf.Clamp(
                slopeRange.x,
                0f,
                90f
            );

        slopeRange.y =
            Mathf.Clamp(
                slopeRange.y,
                0f,
                90f
            );

        if (slopeRange.x > slopeRange.y)
        {
            float temp = slopeRange.x;
            slopeRange.x = slopeRange.y;
            slopeRange.y = temp;
        }

        biomeMaskThreshold =
            Mathf.Clamp01(
                biomeMaskThreshold
            );

        biomeMaskSoftness =
            Mathf.Clamp(
                biomeMaskSoftness,
                0.001f,
                0.5f
            );

        biomeMaskEdgeWidth =
            Mathf.Clamp(
                biomeMaskEdgeWidth,
                0.001f,
                0.5f
            );

        biomeMaskDensityInside =
            Mathf.Clamp01(
                biomeMaskDensityInside
            );

        biomeMaskDensityOutside =
            Mathf.Clamp01(
                biomeMaskDensityOutside
            );

        parentMinDistance =
            Mathf.Max(
                0f,
                parentMinDistance
            );

        parentMaxDistance =
            Mathf.Max(
                parentMinDistance + 0.01f,
                parentMaxDistance
            );

        parentEdgeFalloff =
            Mathf.Max(
                0f,
                parentEdgeFalloff
            );

        parentDensityInside =
            Mathf.Clamp01(
                parentDensityInside
            );

        parentDensityOutside =
            Mathf.Clamp01(
                parentDensityOutside
            );

        scaleMultiplierRange.x =
            Mathf.Max(
                0.001f,
                scaleMultiplierRange.x
            );

        scaleMultiplierRange.y =
            Mathf.Max(
                0.001f,
                scaleMultiplierRange.y
            );

        if (
            scaleMultiplierRange.x >
            scaleMultiplierRange.y
        )
        {
            float temp =
                scaleMultiplierRange.x;

            scaleMultiplierRange.x =
                scaleMultiplierRange.y;

            scaleMultiplierRange.y =
                temp;
        }

        if (
            surfaceOffsetRange.x >
            surfaceOffsetRange.y
        )
        {
            float temp =
                surfaceOffsetRange.x;

            surfaceOffsetRange.x =
                surfaceOffsetRange.y;

            surfaceOffsetRange.y =
                temp;
        }

        noiseScale =
            Mathf.Max(
                0.0001f,
                noiseScale
            );

        clusterSize =
            Mathf.Max(
                0.1f,
                clusterSize
            );

        clusterEdgeSoftness =
            Mathf.Clamp(
                clusterEdgeSoftness,
                0.01f,
                0.5f
            );

        if (terrainLayerRules == null)
        {
            terrainLayerRules =
                new List<
                    VegetationTerrainLayerRule
                >();
        }

        for (
            int i = 0;
            i < terrainLayerRules.Count;
            i++
        )
        {
            terrainLayerRules[i]
                ?.Sanitize();
        }
    }
}