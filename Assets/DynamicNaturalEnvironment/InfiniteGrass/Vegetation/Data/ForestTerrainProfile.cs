using UnityEngine;

public enum ForestTerrainBorderMode
{
    None,
    FlattenToHeight,
    Lower
}

[CreateAssetMenu(fileName = "FTP_ForestTerrain", menuName = "Vegetation/Forest Terrain Profile")]
public class ForestTerrainProfile : ScriptableObject
{
    [Header("Seed")]
    public int seed = 12345;

    [Header("Base")]
    [Range(0f, 1f)]
    [Tooltip("整个Terrain的基础归一化高度。实际世界高度由TerrainData Size Y决定")]
    public float baseHeight = 0.16f;

    [Header("Macro Shape")]
    [Min(0.00001f)]
    [Tooltip("最大尺度的地形起伏频率。越小，地形块越大")]
    public float macroScale = 0.0018f;

    [Range(0f, 1f)]
    public float macroStrength = 0.18f;

    [Header("Rolling Hills")]
    [Min(0.00001f)]
    public float hillScale = 0.0065f;

    [Range(0f, 1f)]
    public float hillStrength = 0.10f;

    [Header("Mountains / Ridges")]
    [Min(0.00001f)]
    public float mountainScale = 0.0012f;

    [Range(0f, 1f)]
    public float mountainStrength = 0.14f;

    [Range(0.25f, 8f)]
    [Tooltip("越高，山脊区域越集中")]
    public float mountainPower = 2.4f;

    [Header("Valleys")]
    [Min(0.00001f)]
    public float valleyScale = 0.0028f;

    [Range(0f, 1f)]
    public float valleyStrength = 0.08f;

    [Range(0.25f, 8f)]
    [Tooltip("越高，谷地越集中成带状")]
    public float valleyPower = 2.0f;

    [Header("Domain Warp")]
    [Min(0.00001f)]
    [Tooltip("控制地形形状扭曲尺度")]
    public float warpScale = 0.0018f;

    [Min(0f)]
    [Tooltip("地形采样坐标的世界空间扭曲强度，单位为米")]
    public float warpStrength = 60f;

    [Header("Height Remap")]
    [Tooltip("对最终0~1高度进行重新映射。可以用它把谷底压平、让高地更陡")]
    public AnimationCurve heightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Border")]
    public ForestTerrainBorderMode borderMode = ForestTerrainBorderMode.None;

    [Min(0f)]
    [Tooltip("距离Terrain边缘多少米开始执行边缘处理")]
    public float borderWidth = 80f;

    [Range(0f, 1f)]
    [Tooltip("FlattenToHeight模式下的目标归一化高度")]
    public float borderTargetHeight = 0.08f;

    [Range(0f, 1f)]
    [Tooltip("Lower模式下Terrain最边缘最多降低多少归一化高度")]
    public float borderLowerAmount = 0.12f;

    [Header("Smoothing")]
    [Range(0, 8)]
    public int smoothingPasses = 2;

    [Range(0f, 1f)]
    [Tooltip("每次平滑时向周围平均值靠近的程度")]
    public float smoothingStrength = 0.30f;

    [Header("Advanced Noise")]
    [Range(1, 8)]
    public int octaves = 4;

    [Range(1.5f, 3f)]
    public float lacunarity = 2.03f;

    [Range(0.1f, 0.9f)]
    public float persistence = 0.5f;

    public void Sanitize()
    {
        baseHeight = Mathf.Clamp01(baseHeight);

        macroScale = Mathf.Max(0.00001f, macroScale);
        macroStrength = Mathf.Clamp01(macroStrength);

        hillScale = Mathf.Max(0.00001f, hillScale);
        hillStrength = Mathf.Clamp01(hillStrength);

        mountainScale = Mathf.Max(0.00001f, mountainScale);
        mountainStrength = Mathf.Clamp01(mountainStrength);
        mountainPower = Mathf.Clamp(mountainPower, 0.25f, 8f);

        valleyScale = Mathf.Max(0.00001f, valleyScale);
        valleyStrength = Mathf.Clamp01(valleyStrength);
        valleyPower = Mathf.Clamp(valleyPower, 0.25f, 8f);

        warpScale = Mathf.Max(0.00001f, warpScale);
        warpStrength = Mathf.Max(0f, warpStrength);

        borderWidth = Mathf.Max(0f, borderWidth);
        borderTargetHeight = Mathf.Clamp01(borderTargetHeight);
        borderLowerAmount = Mathf.Clamp01(borderLowerAmount);

        smoothingPasses = Mathf.Clamp(smoothingPasses, 0, 8);
        smoothingStrength = Mathf.Clamp01(smoothingStrength);

        octaves = Mathf.Clamp(octaves, 1, 8);
        lacunarity = Mathf.Clamp(lacunarity, 1.5f, 3f);
        persistence = Mathf.Clamp(persistence, 0.1f, 0.9f);

        if (heightCurve == null || heightCurve.length == 0)
        {
            heightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }

    private void OnValidate()
    {
        Sanitize();
    }
}