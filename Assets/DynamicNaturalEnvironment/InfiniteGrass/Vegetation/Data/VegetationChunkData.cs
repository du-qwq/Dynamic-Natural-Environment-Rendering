using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class VegetationChunkData
{
    [Tooltip("Chunk在XZ网格中的坐标")]
    public Vector2Int coordinate;

    [Tooltip("该Chunk的世界空间包围盒")]
    public Bounds bounds;

    [Tooltip("该Chunk包含的所有植被实例")]
    public List<VegetationInstance> instances =
        new List<VegetationInstance>();

    public VegetationChunkData(
        Vector2Int coordinate,
        float chunkSize
    )
    {
        this.coordinate = coordinate;

        bounds = CreateDefaultBounds(
            coordinate,
            chunkSize
        );
    }

    public void AddInstance(
        VegetationInstance instance,
        float chunkSize,
        bool recalculateBounds = true
    )
    {
        instances.Add(instance);

        if (recalculateBounds)
        {
            RecalculateBounds(chunkSize);
        }
    }

    public bool RemoveInstance(
        int persistentID,
        float chunkSize
    )
    {
        for (int i = 0; i < instances.Count; i++)
        {
            if (
                instances[i].persistentID !=
                persistentID
            )
            {
                continue;
            }

            instances.RemoveAt(i);

            RecalculateBounds(chunkSize);

            return true;
        }

        return false;
    }

    public void RecalculateBounds(float chunkSize)
    {
        if (instances.Count == 0)
        {
            bounds = CreateDefaultBounds(
                coordinate,
                chunkSize
            );

            return;
        }

        float minY = instances[0].position.y;
        float maxY = minY;

        for (int i = 1; i < instances.Count; i++)
        {
            float y = instances[i].position.y;

            if (y < minY)
            {
                minY = y;
            }

            if (y > maxY)
            {
                maxY = y;
            }
        }

        float centerX =
            (coordinate.x + 0.5f) * chunkSize;

        float centerZ =
            (coordinate.y + 0.5f) * chunkSize;

        float centerY =
            (minY + maxY) * 0.5f;

        float height =
            Mathf.Max(maxY - minY, 1f);

        bounds = new Bounds(
            new Vector3(
                centerX,
                centerY,
                centerZ
            ),
            new Vector3(
                chunkSize,
                height,
                chunkSize
            )
        );
    }

    private static Bounds CreateDefaultBounds(
        Vector2Int coordinate,
        float chunkSize
    )
    {
        Vector3 center = new Vector3(
            (coordinate.x + 0.5f) * chunkSize,
            0f,
            (coordinate.y + 0.5f) * chunkSize
        );

        return new Bounds(
            center,
            new Vector3(
                chunkSize,
                1f,
                chunkSize
            )
        );
    }
}