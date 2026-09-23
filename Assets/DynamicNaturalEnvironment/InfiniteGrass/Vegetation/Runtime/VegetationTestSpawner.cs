using UnityEngine;

public class VegetationTestSpawner : MonoBehaviour
{
    [Header("数据")]
    public VegetationDatabase database;
    public VegetationSpecies species;
    public VegetationRenderer renderer;

    [Header("生成")]
    [Min(1)]
    public int instanceCount = 1000;

    public Vector2 areaSize = new Vector2(50f, 50f);

    [Tooltip("固定随机种子")]
    public int seed = 12345;

    [Header("地形")]
    [Tooltip("有Terrain就直接指定，没有则使用Raycast")]
    public Terrain terrain;

    public LayerMask groundMask = ~0;

    [Min(1f)]
    public float rayHeight = 500f;

    [ContextMenu("Generate Test Instances")]
    public void GenerateTestInstances()
    {
        if (database == null || species == null)
        {
            Debug.LogError("VegetationTestSpawner：Database或Species没有设置。");
            return;
        }

        database.ClearAllInstances();

        System.Random random = new System.Random(seed);

        for (int i = 0; i < instanceCount; i++)
        {
            float x = Mathf.Lerp(-areaSize.x * 0.5f, areaSize.x * 0.5f, (float)random.NextDouble());
            float z = Mathf.Lerp(-areaSize.y * 0.5f, areaSize.y * 0.5f, (float)random.NextDouble());

            Vector3 position = transform.position + new Vector3(x, 0f, z);
            Vector3 normal = Vector3.up;

            if (!TryGetGround(ref position, ref normal)) continue;

            float yaw = species.randomYRotation ? (float)random.NextDouble() * 360f : 0f;

            Quaternion rotation = Quaternion.identity;

            if (species.alignToTerrainNormal)
            {
                rotation = Quaternion.FromToRotation(Vector3.up, normal);
                rotation = Quaternion.AngleAxis(yaw, normal) * rotation;
            }
            else
            {
                rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            float scaleValue = Mathf.Lerp(species.scaleRange.x, species.scaleRange.y, (float)random.NextDouble());
            Vector3 scale = Vector3.one * scaleValue;

            database.AddInstance(species, position, rotation, scale);
        }

        database.RecalculateAllBounds();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(database);
        UnityEditor.AssetDatabase.SaveAssets();
#endif

        if (renderer != null) renderer.Rebuild();

        Debug.Log($"Vegetation测试数据生成完成，共生成 {database.GetTotalInstanceCount()} 个Instance。");
    }

    [ContextMenu("Clear Test Instances")]
    public void ClearTestInstances()
    {
        if (database == null) return;

        database.ClearAllInstances();

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(database);
        UnityEditor.AssetDatabase.SaveAssets();
#endif

        if (renderer != null) renderer.Rebuild();
    }

    private bool TryGetGround(ref Vector3 position, ref Vector3 normal)
    {
        if (terrain != null)
        {
            TerrainData terrainData = terrain.terrainData;

            float normalizedX = Mathf.InverseLerp(terrain.transform.position.x, terrain.transform.position.x + terrainData.size.x, position.x);
            float normalizedZ = Mathf.InverseLerp(terrain.transform.position.z, terrain.transform.position.z + terrainData.size.z, position.z);

            if (normalizedX < 0f || normalizedX > 1f || normalizedZ < 0f || normalizedZ > 1f) return false;

            position.y = terrain.SampleHeight(position) + terrain.transform.position.y;
            normal = terrainData.GetInterpolatedNormal(normalizedX, normalizedZ);

            return true;
        }

        Vector3 rayOrigin = position + Vector3.up * rayHeight;

        if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayHeight * 2f, groundMask)) return false;

        position = hit.point;
        normal = hit.normal;

        return true;
    }
}