using System;
using UnityEngine;

public static class ForestTerrainGenerator
{
    public static void GenerateAndApply(Terrain terrain, ForestTerrainProfile profile, Action<float, string> progress = null)
    {
        if (terrain == null)
        {
            Debug.LogError("ForestTerrainGenerator: Terrain is null.");
            return;
        }

        if (terrain.terrainData == null)
        {
            Debug.LogError("ForestTerrainGenerator: TerrainData is null.", terrain);
            return;
        }

        if (profile == null)
        {
            Debug.LogError("ForestTerrainGenerator: Profile is null.", terrain);
            return;
        }

        profile.Sanitize();

        float[,] heights = GenerateHeights(terrain, profile, progress);

        progress?.Invoke(0.96f, "Applying Heightmap");

        terrain.terrainData.SetHeights(0, 0, heights);
        terrain.Flush();

        progress?.Invoke(1f, "Finished");
    }

    public static float[,] GenerateHeights(Terrain terrain, ForestTerrainProfile profile, Action<float, string> progress = null)
    {
        TerrainData terrainData = terrain.terrainData;
        int resolution = terrainData.heightmapResolution;
        Vector3 terrainSize = terrainData.size;
        Vector3 terrainPosition = terrain.transform.position;

        float[,] heights = new float[resolution, resolution];

        Vector2 macroOffset = GetSeedOffset(profile.seed, 0x1031);
        Vector2 hillOffset = GetSeedOffset(profile.seed, 0x2081);
        Vector2 mountainOffset = GetSeedOffset(profile.seed, 0x3091);
        Vector2 valleyOffset = GetSeedOffset(profile.seed, 0x40A1);
        Vector2 warpOffsetA = GetSeedOffset(profile.seed, 0x50B1);
        Vector2 warpOffsetB = GetSeedOffset(profile.seed, 0x60C1);

        float invResolution = 1f / Mathf.Max(1, resolution - 1);

        for (int z = 0; z < resolution; z++)
        {
            float normalizedZ = z * invResolution;
            float worldZ = terrainPosition.z + normalizedZ * terrainSize.z;

            for (int x = 0; x < resolution; x++)
            {
                float normalizedX = x * invResolution;
                float worldX = terrainPosition.x + normalizedX * terrainSize.x;

                float warpX = FractalNoise(
                    worldX * profile.warpScale + warpOffsetA.x,
                    worldZ * profile.warpScale + warpOffsetA.y,
                    3,
                    profile.lacunarity,
                    profile.persistence
                ) * 2f - 1f;

                float warpZ = FractalNoise(
                    worldX * profile.warpScale + warpOffsetB.x,
                    worldZ * profile.warpScale + warpOffsetB.y,
                    3,
                    profile.lacunarity,
                    profile.persistence
                ) * 2f - 1f;

                float warpedWorldX = worldX + warpX * profile.warpStrength;
                float warpedWorldZ = worldZ + warpZ * profile.warpStrength;

                float macroNoise = FractalNoise(
                    warpedWorldX * profile.macroScale + macroOffset.x,
                    warpedWorldZ * profile.macroScale + macroOffset.y,
                    profile.octaves,
                    profile.lacunarity,
                    profile.persistence
                );

                float hillNoise = FractalNoise(
                    warpedWorldX * profile.hillScale + hillOffset.x,
                    warpedWorldZ * profile.hillScale + hillOffset.y,
                    profile.octaves,
                    profile.lacunarity,
                    profile.persistence
                );

                float mountainNoise = FractalNoise(
                    warpedWorldX * profile.mountainScale + mountainOffset.x,
                    warpedWorldZ * profile.mountainScale + mountainOffset.y,
                    profile.octaves,
                    profile.lacunarity,
                    profile.persistence
                );

                float valleyNoise = FractalNoise(
                    warpedWorldX * profile.valleyScale + valleyOffset.x,
                    warpedWorldZ * profile.valleyScale + valleyOffset.y,
                    profile.octaves,
                    profile.lacunarity,
                    profile.persistence
                );

                float macroContribution = (macroNoise * 2f - 1f) * profile.macroStrength;
                float hillContribution = (hillNoise * 2f - 1f) * profile.hillStrength;

                float ridge = 1f - Mathf.Abs(mountainNoise * 2f - 1f);
                ridge = Mathf.Pow(Mathf.Clamp01(ridge), profile.mountainPower);
                float mountainContribution = ridge * profile.mountainStrength;

                float valley = 1f - Mathf.Abs(valleyNoise * 2f - 1f);
                valley = Mathf.Pow(Mathf.Clamp01(valley), profile.valleyPower);
                float valleyContribution = valley * profile.valleyStrength;

                float height = profile.baseHeight;
                height += macroContribution;
                height += hillContribution;
                height += mountainContribution;
                height -= valleyContribution;

                height = Mathf.Clamp01(height);
                height = Mathf.Clamp01(profile.heightCurve.Evaluate(height));

                height = ApplyBorder(
                    height,
                    normalizedX,
                    normalizedZ,
                    terrainSize,
                    profile
                );

                heights[z, x] = Mathf.Clamp01(height);
            }

            float progressValue = resolution > 1 ? z / (float)(resolution - 1) : 1f;
            progress?.Invoke(progressValue * 0.82f, $"Generating Heightmap {z + 1}/{resolution}");
        }

        if (profile.smoothingPasses > 0 && profile.smoothingStrength > 0f)
        {
            SmoothHeights(
                heights,
                profile.smoothingPasses,
                profile.smoothingStrength,
                progress
            );
        }

        return heights;
    }

    private static float ApplyBorder(
        float height,
        float normalizedX,
        float normalizedZ,
        Vector3 terrainSize,
        ForestTerrainProfile profile)
    {
        if (profile.borderMode == ForestTerrainBorderMode.None || profile.borderWidth <= 0f)
        {
            return height;
        }

        float worldDistanceLeft = normalizedX * terrainSize.x;
        float worldDistanceRight = (1f - normalizedX) * terrainSize.x;
        float worldDistanceBottom = normalizedZ * terrainSize.z;
        float worldDistanceTop = (1f - normalizedZ) * terrainSize.z;

        float nearestBorderDistance = Mathf.Min(
            Mathf.Min(worldDistanceLeft, worldDistanceRight),
            Mathf.Min(worldDistanceBottom, worldDistanceTop)
        );

        if (nearestBorderDistance >= profile.borderWidth)
        {
            return height;
        }

        float t = Mathf.Clamp01(nearestBorderDistance / Mathf.Max(0.001f, profile.borderWidth));
        t = Smooth01(t);

        switch (profile.borderMode)
        {
            case ForestTerrainBorderMode.FlattenToHeight:
                return Mathf.Lerp(profile.borderTargetHeight, height, t);

            case ForestTerrainBorderMode.Lower:
                return Mathf.Clamp01(height - (1f - t) * profile.borderLowerAmount);

            default:
                return height;
        }
    }

    private static void SmoothHeights(
        float[,] heights,
        int passes,
        float strength,
        Action<float, string> progress)
    {
        int height = heights.GetLength(0);
        int width = heights.GetLength(1);

        float[,] source = heights;
        float[,] scratch = new float[height, width];

        for (int pass = 0; pass < passes; pass++)
        {
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float sum = 0f;
                    int count = 0;

                    int minZ = Mathf.Max(0, z - 1);
                    int maxZ = Mathf.Min(height - 1, z + 1);
                    int minX = Mathf.Max(0, x - 1);
                    int maxX = Mathf.Min(width - 1, x + 1);

                    for (int sampleZ = minZ; sampleZ <= maxZ; sampleZ++)
                    {
                        for (int sampleX = minX; sampleX <= maxX; sampleX++)
                        {
                            sum += source[sampleZ, sampleX];
                            count++;
                        }
                    }

                    float average = count > 0 ? sum / count : source[z, x];
                    scratch[z, x] = Mathf.Lerp(source[z, x], average, strength);
                }
            }

            if (pass < passes - 1)
            {
                float[,] temp = source;
                source = scratch;
                scratch = temp;
            }

            float passProgress = (pass + 1f) / Mathf.Max(1, passes);
            progress?.Invoke(
                Mathf.Lerp(0.82f, 0.95f, passProgress),
                $"Smoothing {pass + 1}/{passes}"
            );
        }

        if (!ReferenceEquals(source, heights))
        {
            CopyArray(source, heights);
        }
        else if (passes % 2 == 1)
        {
            CopyArray(scratch, heights);
        }
    }

    private static void CopyArray(float[,] source, float[,] destination)
    {
        int height = source.GetLength(0);
        int width = source.GetLength(1);

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                destination[z, x] = source[z, x];
            }
        }
    }

    private static float FractalNoise(
        float x,
        float y,
        int octaves,
        float lacunarity,
        float persistence)
    {
        float value = 0f;
        float amplitude = 1f;
        float frequency = 1f;
        float amplitudeSum = 0f;

        for (int octave = 0; octave < octaves; octave++)
        {
            value += Mathf.PerlinNoise(x * frequency, y * frequency) * amplitude;
            amplitudeSum += amplitude;

            frequency *= lacunarity;
            amplitude *= persistence;
        }

        return amplitudeSum > 0f ? value / amplitudeSum : 0f;
    }

    private static Vector2 GetSeedOffset(int seed, int salt)
    {
        unchecked
        {
            uint hashA = HashUInt((uint)(seed ^ salt));
            uint hashB = HashUInt(hashA ^ 0x9E3779B9u);

            float x = (hashA & 0x00FFFFFFu) / 16777215f;
            float y = (hashB & 0x00FFFFFFu) / 16777215f;

            return new Vector2(x * 10000f, y * 10000f);
        }
    }

    private static uint HashUInt(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;
        return value;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }
}