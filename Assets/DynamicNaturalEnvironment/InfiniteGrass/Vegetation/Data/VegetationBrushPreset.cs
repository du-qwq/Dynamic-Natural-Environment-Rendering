using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class VegetationBrushSpecies
{
    public VegetationSpecies species;

    [Min(0f)]
    [Tooltip("该Species在混刷时的相对权重")]
    public float weight = 1f;
}

[CreateAssetMenu(fileName = "VBP_NewBrush", menuName = "Vegetation/Brush Preset")]
public class VegetationBrushPreset : ScriptableObject
{
    [Tooltip("该笔刷可以混合生成的Species")]
    public List<VegetationBrushSpecies> species = new List<VegetationBrushSpecies>();

    public VegetationSpecies GetRandomSpecies(float randomValue)
    {
        float totalWeight = 0f;

        for (int i = 0; i < species.Count; i++)
        {
            if (species[i].species == null || species[i].weight <= 0f) continue;
            totalWeight += species[i].weight;
        }

        if (totalWeight <= 0f) return null;

        float target = randomValue * totalWeight;
        float current = 0f;

        for (int i = 0; i < species.Count; i++)
        {
            VegetationBrushSpecies entry = species[i];

            if (entry.species == null || entry.weight <= 0f) continue;

            current += entry.weight;

            if (target <= current) return entry.species;
        }

        return null;
    }
}