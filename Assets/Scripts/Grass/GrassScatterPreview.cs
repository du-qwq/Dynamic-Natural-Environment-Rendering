using UnityEngine;

[ExecuteAlways]
public class GrassScatterPreview : MonoBehaviour
{
    [Header("目标地形")]
    public Terrain targetTerrain;

    [Header("草")]
    public GameObject grassPrefab;
    public Material grassMaterial;

    [Header("生成区域")]
    public Vector2 areaSize = new Vector2(20f, 20f);
    [Min(1)] public int count = 3200;

    [Header("随机缩放")]
    public Vector2 scaleRange = new Vector2(0.65f, 0.95f);

    [Header("坡度")]
    [Range(0f, 90f)] public float maxSlope = 35f;
    [Range(0f, 1f)] public float alignToNormal = 0.25f;

    [Header("随机")]
    public int seed = 12345;

    [ContextMenu("Generate Grass")]
    public void GenerateGrass()
    {
        ClearGrass();
        if (!targetTerrain || !grassPrefab) return;

        Random.InitState(seed);

        TerrainData data = targetTerrain.terrainData;
        Vector3 terrainPos = targetTerrain.transform.position;
        Vector3 terrainSize = data.size;

        int generated = 0;
        int attempts = 0;
        int maxAttempts = count * 5;

        while (generated < count && attempts < maxAttempts)
        {
            attempts++;

            float worldX = transform.position.x + Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f);
            float worldZ = transform.position.z + Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f);

            float normalizedX = (worldX - terrainPos.x) / terrainSize.x;
            float normalizedZ = (worldZ - terrainPos.z) / terrainSize.z;

            if (normalizedX < 0f || normalizedX > 1f || normalizedZ < 0f || normalizedZ > 1f) continue;

            float worldY = targetTerrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + terrainPos.y;
            Vector3 normal = data.GetInterpolatedNormal(normalizedX, normalizedZ);
            float slope = Vector3.Angle(normal, Vector3.up);

            if (slope > maxSlope) continue;

            GameObject grass = Instantiate(grassPrefab, transform);
            grass.name = $"Grass_{generated:0000}";
            grass.transform.position = new Vector3(worldX, worldY, worldZ);

            Quaternion randomY = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Quaternion terrainRotation = Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, normal, alignToNormal));
            grass.transform.rotation = terrainRotation * randomY;

            float scale = Random.Range(scaleRange.x, scaleRange.y);
            grass.transform.localScale = Vector3.one * scale;

            if (grassMaterial)
            {
                Renderer[] renderers = grass.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in renderers) r.sharedMaterial = grassMaterial;
            }

            generated++;
        }

        Debug.Log($"Grass generated: {generated}/{count}");
    }

    [ContextMenu("Clear Grass")]
    public void ClearGrass()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }
}