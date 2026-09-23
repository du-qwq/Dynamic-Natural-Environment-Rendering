using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class VegetationBiomeMaskSettings
{
    [Tooltip("启用整个Biome共享的大尺度空间Mask")]
    public bool enabled = true;

    [Header("Large Scale")]
    [Min(0.0001f)]
    [Tooltip("大尺度区域频率。越小，森林/空地块越大")]
    public float baseScale = 0.0065f;

    [Header("Detail")]
    [Min(0.0001f)]
    [Tooltip("用于打破大尺度轮廓的细节频率")]
    public float detailScale = 0.028f;

    [Range(0f, 1f)]
    [Tooltip("细节对大尺度Mask的影响")]
    public float detailStrength = 0.22f;

    [Header("Domain Warp")]
    [Min(0.0001f)]
    [Tooltip("形状扭曲频率")]
    public float warpScale = 0.0035f;

    [Min(0f)]
    [Tooltip("形状扭曲强度，单位为世界米")]
    public float warpStrength = 24f;

    [Header("Seed")]
    public int seedOffset = 7301;

    [Tooltip("反转整张Biome Mask")]
    public bool invert = false;

    public void Sanitize()
    {
        baseScale = Mathf.Max(0.0001f, baseScale);
        detailScale = Mathf.Max(0.0001f, detailScale);
        detailStrength = Mathf.Clamp01(detailStrength);
        warpScale = Mathf.Max(0.0001f, warpScale);
        warpStrength = Mathf.Max(0f, warpStrength);
    }
}

[CreateAssetMenu(fileName = "VBP_NewBiome", menuName = "Vegetation/Biome Profile")]
public class VegetationBiomeProfile : ScriptableObject
{
    [Header("Biome")]
    public string biomeName = "New Biome";

    [Header("Biome Mask")]
    public VegetationBiomeMaskSettings biomeMask = new VegetationBiomeMaskSettings();

    [Header("Scatter Layers")]
    [Tooltip("生成顺序很重要。建议 Trees -> Rocks -> Bushes -> Flowers -> Grass")]
    public List<VegetationScatterLayer> layers = new List<VegetationScatterLayer>();

    private void OnValidate()
    {
        if (biomeMask == null)
        {
            biomeMask = new VegetationBiomeMaskSettings();
        }

        biomeMask.Sanitize();

        if (layers == null)
        {
            layers = new List<VegetationScatterLayer>();
            return;
        }

        for (int i = 0; i < layers.Count; i++)
        {
            VegetationScatterLayer layer = layers[i];

            if (layer == null)
            {
                continue;
            }

            layer.Sanitize();

            if (layer.seedOffset == 0)
            {
                layer.seedOffset = (i + 1) * 1009;
            }
        }
    }
}