using UnityEngine;

[ExecuteAlways]
public class VegetationTerrainColorController : MonoBehaviour
{
    [Header("Terrain")]
    public Terrain terrain;

    [Header("Color Map")]
    public Texture2D terrainColorMap;

    [Tooltip("是否启用植被地形颜色融合")]
    public bool enableTerrainColor = true;

    private static readonly int TerrainColorEnabledID = Shader.PropertyToID("_VegetationTerrainColorEnabled");
    private static readonly int TerrainColorMapID = Shader.PropertyToID("_VegetationTerrainColorMap");
    private static readonly int TerrainOriginSizeID = Shader.PropertyToID("_VegetationTerrainOriginSize");

    private void OnEnable()
    {
        Apply();
    }

    private void Update()
    {
        Apply();
    }

    private void OnValidate()
    {
        Apply();
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(TerrainColorEnabledID, 0f);
    }

    private void Apply()
    {
        if (!enableTerrainColor || terrain == null || terrain.terrainData == null || terrainColorMap == null)
        {
            Shader.SetGlobalFloat(TerrainColorEnabledID, 0f);
            return;
        }

        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;

        Shader.SetGlobalFloat(TerrainColorEnabledID, 1f);
        Shader.SetGlobalTexture(TerrainColorMapID, terrainColorMap);
        Shader.SetGlobalVector(TerrainOriginSizeID, new Vector4(origin.x, origin.z, Mathf.Max(size.x, 0.001f), Mathf.Max(size.z, 0.001f)));
    }
}