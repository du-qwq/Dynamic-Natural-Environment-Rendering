using System;
using UnityEngine;

[Serializable]
public struct VegetationInstance
{
    [Tooltip("该实例在整个VegetationDatabase中的唯一ID")]
    public int persistentID;

    [Tooltip("该实例使用的VegetationSpecies索引")]
    public int speciesIndex;

    [Tooltip("世界空间位置")]
    public Vector3 position;

    [Tooltip("世界空间旋转")]
    public Quaternion rotation;

    [Tooltip("实例缩放")]
    public Vector3 scale;

    public VegetationInstance(int persistentID, int speciesIndex, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        this.persistentID = persistentID;
        this.speciesIndex = speciesIndex;
        this.position = position;
        this.rotation = rotation;
        this.scale = scale;
    }

    public Matrix4x4 LocalToWorldMatrix => Matrix4x4.TRS(position, rotation, scale);
}