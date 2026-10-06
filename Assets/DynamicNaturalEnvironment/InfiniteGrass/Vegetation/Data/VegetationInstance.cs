using System;
using UnityEngine;

public enum VegetationInstanceSource
{
    Manual = 0,
    Procedural = 1,
    Imported = 2
}

[Serializable]
public struct VegetationInstance
{
    [Tooltip("该实例在整个VegetationDatabase中的唯一ID")]
    public int persistentID;

    [Tooltip("该实例使用的VegetationSpecies索引")]
    public int speciesIndex;

    [Tooltip("该实例使用的VegetationSpecies持久ID；speciesIndex仅为当前列表索引缓存")]
    public int speciesID;

    public VegetationInstanceSource sourceType;
    public int generationOwnerID;
    public int generationBatchID;

    [Tooltip("世界空间位置")]
    public Vector3 position;

    [Tooltip("世界空间旋转")]
    public Quaternion rotation;

    [Tooltip("实例缩放")]
    public Vector3 scale;

    public VegetationInstance(int persistentID, int speciesID, int speciesIndex, Vector3 position, Quaternion rotation, Vector3 scale)
        : this(persistentID, speciesID, speciesIndex, position, rotation, scale, VegetationInstanceSource.Manual, 0, 0)
    {
    }

    public VegetationInstance(int persistentID, int speciesID, int speciesIndex, Vector3 position, Quaternion rotation, Vector3 scale,
        VegetationInstanceSource sourceType, int generationOwnerID, int generationBatchID)
    {
        this.persistentID = persistentID;
        this.speciesID = speciesID;
        this.speciesIndex = speciesIndex;
        this.sourceType = sourceType;
        this.generationOwnerID = generationOwnerID;
        this.generationBatchID = generationBatchID;
        this.position = position;
        this.rotation = rotation;
        this.scale = scale;
    }

    public VegetationInstance(int persistentID, int speciesIndex, Vector3 position, Quaternion rotation, Vector3 scale)
        : this(persistentID, 0, speciesIndex, position, rotation, scale)
    {
    }

    public Matrix4x4 LocalToWorldMatrix => Matrix4x4.TRS(position, rotation, scale);
}
