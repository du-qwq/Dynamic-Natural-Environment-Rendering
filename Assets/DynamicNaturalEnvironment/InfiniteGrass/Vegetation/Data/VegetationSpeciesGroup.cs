using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class VegetationSpeciesGroupEntry
{
    public VegetationSpecies species;

    [Min(0f)]
    [Tooltip("该Species在Group中的相对生成权重")]
    public float weight = 1f;
}

[CreateAssetMenu(fileName = "VSG_NewGroup", menuName = "Vegetation/Species Group")]
public class VegetationSpeciesGroup : ScriptableObject
{
    [Tooltip("这个Group中允许程序化生成的所有Species")]
    public List<VegetationSpeciesGroupEntry> species = new List<VegetationSpeciesGroupEntry>();

    public bool HasValidSpecies
    {
        get
        {
            if (species == null) return false;

            for (int i = 0; i < species.Count; i++)
            {
                VegetationSpeciesGroupEntry entry = species[i];

                if (entry != null && entry.species != null && entry.weight > 0f)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public VegetationSpecies GetRandomSpecies(System.Random random)
    {
        if (random == null || species == null || species.Count == 0)
        {
            return null;
        }

        float totalWeight = 0f;

        for (int i = 0; i < species.Count; i++)
        {
            VegetationSpeciesGroupEntry entry = species[i];

            if (entry == null || entry.species == null || entry.weight <= 0f)
            {
                continue;
            }

            totalWeight += entry.weight;
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        float target = (float)random.NextDouble() * totalWeight;
        float current = 0f;

        for (int i = 0; i < species.Count; i++)
        {
            VegetationSpeciesGroupEntry entry = species[i];

            if (entry == null || entry.species == null || entry.weight <= 0f)
            {
                continue;
            }

            current += entry.weight;

            if (target <= current)
            {
                return entry.species;
            }
        }

        for (int i = species.Count - 1; i >= 0; i--)
        {
            VegetationSpeciesGroupEntry entry = species[i];

            if (entry != null && entry.species != null && entry.weight > 0f)
            {
                return entry.species;
            }
        }

        return null;
    }
}