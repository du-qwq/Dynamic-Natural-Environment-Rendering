using System;
using System.Collections.Generic;
using UnityEngine;

internal static class VegetationColliderAutoEstimator
{
    internal struct CapsuleEstimate
    {
        public Vector3 center;
        public float radius;
        public float height;
        public Mesh sourceMesh;
        public string method;
        public int sampleCount;
    }

    private static readonly string[] TrunkKeywords =
    {
        "trunk", "bark", "wood", "stem", "branch", "log", "树干", "树皮", "木"
    };

    public static bool TryEstimateTreeCapsule(VegetationSpecies species, out CapsuleEstimate estimate, out string error)
    {
        estimate = default;
        error = null;

        if (species == null)
        {
            error = "Species 为空。";
            return false;
        }

        VegetationLODAsset sourceLOD = GetSourceLOD(species);
        Mesh mesh = sourceLOD != null ? sourceLOD.mesh : null;
        if (mesh == null)
        {
            error = "没有可用于估算的 LOD Mesh。";
            return false;
        }

        Bounds bounds = mesh.bounds;
        if (bounds.size.y <= 0.0001f)
        {
            error = "Mesh Bounds 高度无效。";
            return false;
        }

        Vector3[] allVertices;
        try
        {
            allVertices = mesh.vertices;
        }
        catch (Exception)
        {
            return TryEstimateFromBounds(mesh, out estimate, out error);
        }

        if (allVertices == null || allVertices.Length < 4) return TryEstimateFromBounds(mesh, out estimate, out error);

        List<Vector3> trunkVertices = TryCollectTrunkSubMeshVertices(mesh, sourceLOD.materials, allVertices);
        if (trunkVertices.Count >= 12 && TryEstimateFromTrunkVertices(mesh, trunkVertices, out estimate)) return true;

        if (TryEstimateFromGeometry(mesh, allVertices, out estimate)) return true;

        return TryEstimateFromBounds(mesh, out estimate, out error);
    }

    public static bool TryEstimateFromBounds(VegetationSpecies species, out CapsuleEstimate estimate, out string error)
    {
        estimate = default;
        error = null;

        if (species == null)
        {
            error = "Species 为空。";
            return false;
        }

        VegetationLODAsset sourceLOD = GetSourceLOD(species);
        Mesh mesh = sourceLOD != null ? sourceLOD.mesh : null;
        if (mesh == null)
        {
            error = "没有可用于估算的 LOD Mesh。";
            return false;
        }

        return TryEstimateFromBounds(mesh, out estimate, out error);
    }

    private static VegetationLODAsset GetSourceLOD(VegetationSpecies species)
    {
        if (species.lod0 != null && species.lod0.mesh != null) return species.lod0;
        if (species.lod1 != null && species.lod1.mesh != null) return species.lod1;
        if (species.lod2 != null && species.lod2.mesh != null) return species.lod2;
        if (species.lod3 != null && species.lod3.mesh != null) return species.lod3;
        return null;
    }

    private static List<Vector3> TryCollectTrunkSubMeshVertices(Mesh mesh, Material[] materials, Vector3[] vertices)
    {
        List<Vector3> result = new List<Vector3>();
        if (mesh == null || materials == null || materials.Length == 0 || vertices == null || vertices.Length == 0) return result;

        int subMeshCount = Mathf.Min(mesh.subMeshCount, materials.Length);
        HashSet<int> usedIndices = new HashSet<int>();

        for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
        {
            Material material = materials[subMeshIndex];
            if (!LooksLikeTrunkMaterial(material)) continue;

            int[] indices;
            try
            {
                indices = mesh.GetIndices(subMeshIndex);
            }
            catch (Exception)
            {
                continue;
            }

            if (indices == null) continue;
            for (int i = 0; i < indices.Length; i++)
            {
                int vertexIndex = indices[i];
                if (vertexIndex < 0 || vertexIndex >= vertices.Length || !usedIndices.Add(vertexIndex)) continue;
                result.Add(vertices[vertexIndex]);
            }
        }

        return result;
    }

    private static bool LooksLikeTrunkMaterial(Material material)
    {
        if (material == null || string.IsNullOrEmpty(material.name)) return false;

        string name = material.name;
        for (int i = 0; i < TrunkKeywords.Length; i++)
        {
            if (name.IndexOf(TrunkKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }

        return false;
    }

    private static bool TryEstimateFromTrunkVertices(Mesh mesh, List<Vector3> vertices, out CapsuleEstimate estimate)
    {
        estimate = default;
        if (vertices == null || vertices.Count < 4) return false;

        List<float> ys = new List<float>(vertices.Count);
        for (int i = 0; i < vertices.Count; i++) ys.Add(vertices[i].y);
        ys.Sort();

        float bottomY = PercentileSorted(ys, 0.01f);
        float topY = PercentileSorted(ys, 0.92f);
        if (topY <= bottomY + 0.001f) return false;

        float axisSampleTop = Mathf.Lerp(bottomY, topY, 0.55f);
        List<float> xs = new List<float>();
        List<float> zs = new List<float>();

        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 vertex = vertices[i];
            if (vertex.y > axisSampleTop) continue;
            xs.Add(vertex.x);
            zs.Add(vertex.z);
        }

        if (xs.Count < 4) return false;

        xs.Sort();
        zs.Sort();
        float centerX = PercentileSorted(xs, 0.5f);
        float centerZ = PercentileSorted(zs, 0.5f);

        float radiusSampleTop = Mathf.Lerp(bottomY, topY, 0.58f);
        List<float> radialDistances = new List<float>();
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 vertex = vertices[i];
            if (vertex.y > radiusSampleTop) continue;
            float dx = vertex.x - centerX;
            float dz = vertex.z - centerZ;
            radialDistances.Add(Mathf.Sqrt(dx * dx + dz * dz));
        }

        if (radialDistances.Count < 4) return false;
        radialDistances.Sort();

        float radius = PercentileSorted(radialDistances, 0.86f) * 1.08f;
        float minRadius = Mathf.Max(0.01f, Mathf.Min(mesh.bounds.size.x, mesh.bounds.size.z) * 0.01f);
        float maxReasonableRadius = Mathf.Max(minRadius, Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.z) * 0.28f);
        radius = Mathf.Clamp(radius, minRadius, maxReasonableRadius);

        float height = Mathf.Max(radius * 2f, topY - bottomY);
        estimate = new CapsuleEstimate
        {
            center = new Vector3(centerX, (bottomY + topY) * 0.5f, centerZ),
            radius = radius,
            height = height,
            sourceMesh = mesh,
            method = "树干材质 SubMesh",
            sampleCount = vertices.Count
        };
        return true;
    }

    private static bool TryEstimateFromGeometry(Mesh mesh, Vector3[] vertices, out CapsuleEstimate estimate)
    {
        estimate = default;
        if (vertices == null || vertices.Length < 4) return false;

        Bounds bounds = mesh.bounds;
        float fullHeight = bounds.size.y;
        if (fullHeight <= 0.0001f) return false;

        float bottomY = bounds.min.y;
        float lowerTop = bottomY + fullHeight * 0.32f;
        List<Vector3> lowerVertices = new List<Vector3>();

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];
            if (vertex.y >= bottomY - fullHeight * 0.01f && vertex.y <= lowerTop) lowerVertices.Add(vertex);
        }

        if (lowerVertices.Count < 8) return false;

        List<float> xs = new List<float>(lowerVertices.Count);
        List<float> zs = new List<float>(lowerVertices.Count);
        for (int i = 0; i < lowerVertices.Count; i++)
        {
            xs.Add(lowerVertices[i].x);
            zs.Add(lowerVertices[i].z);
        }

        xs.Sort();
        zs.Sort();
        float centerX = PercentileSorted(xs, 0.5f);
        float centerZ = PercentileSorted(zs, 0.5f);

        List<float> distances = new List<float>(lowerVertices.Count);
        for (int i = 0; i < lowerVertices.Count; i++)
        {
            float dx = lowerVertices[i].x - centerX;
            float dz = lowerVertices[i].z - centerZ;
            distances.Add(Mathf.Sqrt(dx * dx + dz * dz));
        }

        distances.Sort();
        float radius = EstimateCoreRadius(distances) * 1.12f;
        float minRadius = Mathf.Max(0.01f, Mathf.Min(bounds.size.x, bounds.size.z) * 0.008f);
        float maxReasonableRadius = Mathf.Max(minRadius, Mathf.Max(bounds.size.x, bounds.size.z) * 0.22f);
        radius = Mathf.Clamp(radius, minRadius, maxReasonableRadius);

        float topY = FindLikelyTrunkTop(vertices, centerX, centerZ, bottomY, fullHeight, radius);
        float height = Mathf.Max(radius * 2f, topY - bottomY);

        estimate = new CapsuleEstimate
        {
            center = new Vector3(centerX, bottomY + height * 0.5f, centerZ),
            radius = radius,
            height = height,
            sourceMesh = mesh,
            method = "下半部几何估算",
            sampleCount = lowerVertices.Count
        };
        return true;
    }

    private static float EstimateCoreRadius(List<float> sortedDistances)
    {
        if (sortedDistances == null || sortedDistances.Count == 0) return 0.1f;
        if (sortedDistances.Count < 12) return PercentileSorted(sortedDistances, 0.55f);

        int start = Mathf.Clamp(Mathf.RoundToInt(sortedDistances.Count * 0.08f), 1, sortedDistances.Count - 2);
        int end = Mathf.Clamp(Mathf.RoundToInt(sortedDistances.Count * 0.72f), start + 1, sortedDistances.Count - 2);
        int bestIndex = -1;
        float bestScore = 0f;

        for (int i = start; i <= end; i++)
        {
            float current = Mathf.Max(0.0001f, sortedDistances[i]);
            float next = sortedDistances[i + 1];
            float ratio = next / current;
            float gap = next - current;
            float score = ratio * (1f + gap);

            if (ratio < 1.35f || score <= bestScore) continue;
            bestScore = score;
            bestIndex = i;
        }

        if (bestIndex >= 0) return sortedDistances[bestIndex];
        return PercentileSorted(sortedDistances, 0.38f);
    }

    private static float FindLikelyTrunkTop(Vector3[] vertices, float centerX, float centerZ, float bottomY, float fullHeight, float baseRadius)
    {
        const int sliceCount = 10;
        float defaultTop = bottomY + fullHeight * 0.62f;
        float startFraction = 0.18f;
        float endFraction = 0.78f;
        float bandHalfHeight = fullHeight * 0.035f;
        float previousRadius = baseRadius;

        for (int slice = 0; slice < sliceCount; slice++)
        {
            float t = sliceCount <= 1 ? 0f : (float)slice / (sliceCount - 1);
            float y = bottomY + fullHeight * Mathf.Lerp(startFraction, endFraction, t);
            List<float> distances = new List<float>();

            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 vertex = vertices[i];
                if (Mathf.Abs(vertex.y - y) > bandHalfHeight) continue;
                float dx = vertex.x - centerX;
                float dz = vertex.z - centerZ;
                distances.Add(Mathf.Sqrt(dx * dx + dz * dz));
            }

            if (distances.Count < 8) continue;
            distances.Sort();
            float sliceRadius = EstimateCoreRadius(distances);

            if (slice >= 2 && sliceRadius > Mathf.Max(baseRadius * 2.8f, previousRadius * 2.2f))
            {
                return Mathf.Clamp(y - bandHalfHeight, bottomY + baseRadius * 2f, bottomY + fullHeight * 0.82f);
            }

            previousRadius = Mathf.Max(baseRadius, sliceRadius);
        }

        return defaultTop;
    }

    private static bool TryEstimateFromBounds(Mesh mesh, out CapsuleEstimate estimate, out string error)
    {
        estimate = default;
        error = null;
        if (mesh == null)
        {
            error = "Mesh 为空。";
            return false;
        }

        Bounds bounds = mesh.bounds;
        if (bounds.size.y <= 0.0001f)
        {
            error = "Mesh Bounds 高度无效。";
            return false;
        }

        float horizontalSize = Mathf.Max(0.001f, Mathf.Min(bounds.size.x, bounds.size.z));
        float radius = Mathf.Max(0.01f, horizontalSize * 0.12f);
        float height = Mathf.Max(radius * 2f, bounds.size.y * 0.6f);
        float bottomY = bounds.min.y;

        estimate = new CapsuleEstimate
        {
            center = new Vector3(bounds.center.x, bottomY + height * 0.5f, bounds.center.z),
            radius = radius,
            height = height,
            sourceMesh = mesh,
            method = "Mesh Bounds 粗略估算",
            sampleCount = 0
        };
        return true;
    }

    private static float PercentileSorted(List<float> sortedValues, float percentile)
    {
        if (sortedValues == null || sortedValues.Count == 0) return 0f;
        if (sortedValues.Count == 1) return sortedValues[0];

        percentile = Mathf.Clamp01(percentile);
        float index = (sortedValues.Count - 1) * percentile;
        int lower = Mathf.FloorToInt(index);
        int upper = Mathf.CeilToInt(index);
        if (lower == upper) return sortedValues[lower];
        return Mathf.Lerp(sortedValues[lower], sortedValues[upper], index - lower);
    }
}
