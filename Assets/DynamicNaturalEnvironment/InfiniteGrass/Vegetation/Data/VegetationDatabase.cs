using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "VegetationDatabase", menuName = "Vegetation/Database")]
public class VegetationDatabase : ScriptableObject
{
    [Header("世界分块")]
    [Min(1f)]
    [Tooltip("每个Chunk在XZ平面的尺寸")]
    public float chunkSize = 32f;

    [Header("植被种类")]
    [Tooltip("当前Database使用的所有VegetationSpecies")]
    public List<VegetationSpecies> species = new List<VegetationSpecies>();

    [Header("实例数据")]
    [Tooltip("世界中的所有植被Chunk")]
    public List<VegetationChunkData> chunks = new List<VegetationChunkData>();

    [SerializeField, HideInInspector] private int nextPersistentID = 1;
    [NonSerialized] private int dataRevision = 1;

    [NonSerialized] private Dictionary<Vector2Int, VegetationChunkData> chunkLookup;
    [NonSerialized] private Dictionary<int, InstanceLocation> instanceLookup;
    [NonSerialized] private HashSet<VegetationChunkData> dirtyBoundsChunks;
    [NonSerialized] private int cachedTotalInstanceCount;
    [NonSerialized] private bool runtimeCachesValid;
    [NonSerialized] private int batchDepth;
    [NonSerialized] private bool pendingDataChange;
    [NonSerialized] private bool pendingChunkLookupInvalidation;
    [NonSerialized] private bool pendingInstanceLookupInvalidation;

    public int DataRevision => dataRevision;
    public bool IsBatchMutationActive => batchDepth > 0;
    public int TotalInstanceCount
    {
        get
        {
            EnsureRuntimeCaches();
            return cachedTotalInstanceCount;
        }
    }
    public int SpeciesCount => species != null ? species.Count : 0;
    public int ChunkCount => chunks != null ? chunks.Count : 0;

    private struct InstanceLocation
    {
        public VegetationChunkData chunk;
        public int index;

        public InstanceLocation(VegetationChunkData chunk, int index)
        {
            this.chunk = chunk;
            this.index = index;
        }
    }

    private void OnEnable()
    {
        ResetRuntimeState();
        RebuildRuntimeCaches();
    }

    private void OnValidate()
    {
        chunkSize = Mathf.Max(1f, chunkSize);
        ResetRuntimeState();
        RebuildRuntimeCaches();
        AdvanceRevision();
    }

    private void ResetRuntimeState()
    {
        chunkLookup = null;
        instanceLookup = null;
        dirtyBoundsChunks = null;
        runtimeCachesValid = false;
        batchDepth = 0;
        pendingDataChange = false;
        pendingChunkLookupInvalidation = false;
        pendingInstanceLookupInvalidation = false;
    }

    private void RebuildRuntimeCaches()
    {
        cachedTotalInstanceCount = 0;
        int maxPersistentID = 0;

        if (chunks != null)
        {
            for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
            {
                VegetationChunkData chunk = chunks[chunkIndex];
                if (chunk == null || chunk.instances == null) continue;

                cachedTotalInstanceCount += chunk.instances.Count;

                for (int instanceIndex = 0; instanceIndex < chunk.instances.Count; instanceIndex++)
                {
                    maxPersistentID = Mathf.Max(
                        maxPersistentID,
                        chunk.instances[instanceIndex].persistentID
                    );
                }
            }
        }

        nextPersistentID = Mathf.Max(nextPersistentID, maxPersistentID + 1, 1);
        runtimeCachesValid = true;
    }

    private void EnsureRuntimeCaches()
    {
        if (!runtimeCachesValid)
        {
            RebuildRuntimeCaches();
        }
    }

    private void EnsureChunkLookup()
    {
        if (chunkLookup != null) return;

        chunkLookup = new Dictionary<Vector2Int, VegetationChunkData>();

        if (chunks == null) return;

        for (int i = 0; i < chunks.Count; i++)
        {
            VegetationChunkData chunk = chunks[i];
            if (chunk == null) continue;

            chunkLookup[chunk.coordinate] = chunk;
        }
    }

    private void EnsureInstanceLookup()
    {
        if (instanceLookup != null) return;

        instanceLookup = new Dictionary<int, InstanceLocation>();

        if (chunks == null) return;

        for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
        {
            VegetationChunkData chunk = chunks[chunkIndex];

            if (chunk == null || chunk.instances == null) continue;

            for (int instanceIndex = 0; instanceIndex < chunk.instances.Count; instanceIndex++)
            {
                VegetationInstance instance = chunk.instances[instanceIndex];

                instanceLookup[instance.persistentID] =
                    new InstanceLocation(chunk, instanceIndex);
            }
        }
    }

    public void BeginBatchMutation()
    {
        EnsureRuntimeCaches();
        batchDepth++;
    }

    public void EndBatchMutation()
    {
        if (batchDepth <= 0)
        {
            Debug.LogWarning(
                "VegetationDatabase.EndBatchMutation() called without a matching BeginBatchMutation().",
                this
            );

            batchDepth = 0;
            return;
        }

        batchDepth--;

        if (batchDepth == 0)
        {
            FlushPendingChanges();
        }
    }

    private void MarkChunkBoundsDirty(VegetationChunkData chunk)
    {
        if (chunk == null) return;

        if (dirtyBoundsChunks == null)
        {
            dirtyBoundsChunks = new HashSet<VegetationChunkData>();
        }

        dirtyBoundsChunks.Add(chunk);
    }

    private void MarkDataChanged(
        bool invalidateChunkLookup = false,
        bool invalidateInstanceLookup = true
    )
    {
        pendingDataChange = true;

        pendingChunkLookupInvalidation |= invalidateChunkLookup;
        pendingInstanceLookupInvalidation |= invalidateInstanceLookup;

        if (batchDepth == 0)
        {
            FlushPendingChanges();
        }
    }

    private void FlushPendingChanges()
    {
        if (dirtyBoundsChunks != null && dirtyBoundsChunks.Count > 0)
        {
            foreach (VegetationChunkData chunk in dirtyBoundsChunks)
            {
                if (
                    chunk == null ||
                    chunk.instances == null ||
                    chunk.instances.Count == 0
                )
                {
                    continue;
                }

                chunk.RecalculateBounds(chunkSize);
            }

            dirtyBoundsChunks.Clear();
        }

        bool removedEmptyChunk = RemoveEmptyChunksInternal();

        if (removedEmptyChunk)
        {
            pendingChunkLookupInvalidation = true;
        }

        if (pendingChunkLookupInvalidation)
        {
            chunkLookup = null;
        }

        if (pendingInstanceLookupInvalidation)
        {
            instanceLookup = null;
        }

        if (pendingDataChange)
        {
            AdvanceRevision();
        }

        pendingDataChange = false;
        pendingChunkLookupInvalidation = false;
        pendingInstanceLookupInvalidation = false;
    }

    private bool RemoveEmptyChunksInternal()
    {
        if (chunks == null) return false;

        bool removed = false;

        for (int i = chunks.Count - 1; i >= 0; i--)
        {
            VegetationChunkData chunk = chunks[i];

            if (
                chunk != null &&
                chunk.instances != null &&
                chunk.instances.Count > 0
            )
            {
                continue;
            }

            chunks.RemoveAt(i);
            removed = true;
        }

        return removed;
    }

    private void AdvanceRevision()
    {
        unchecked
        {
            dataRevision++;
        }

        if (dataRevision == 0)
        {
            dataRevision = 1;
        }
    }

    public Vector2Int WorldToChunkCoordinate(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPosition.x / chunkSize),
            Mathf.FloorToInt(worldPosition.z / chunkSize)
        );
    }

    public VegetationChunkData GetChunk(Vector2Int coordinate)
    {
        EnsureChunkLookup();

        chunkLookup.TryGetValue(
            coordinate,
            out VegetationChunkData chunk
        );

        return chunk;
    }

    public VegetationChunkData GetOrCreateChunk(Vector2Int coordinate)
    {
        EnsureChunkLookup();

        if (
            chunkLookup.TryGetValue(
                coordinate,
                out VegetationChunkData chunk
            )
        )
        {
            return chunk;
        }

        chunk = new VegetationChunkData(coordinate, chunkSize);

        chunks.Add(chunk);
        chunkLookup.Add(coordinate, chunk);

        return chunk;
    }

    public int GetSpeciesIndex(VegetationSpecies vegetationSpecies)
    {
        if (vegetationSpecies == null) return -1;

        return species.IndexOf(vegetationSpecies);
    }

    public int GetOrAddSpecies(VegetationSpecies vegetationSpecies)
    {
        int index = GetOrAddSpeciesInternal(
            vegetationSpecies,
            out bool added
        );

        if (added)
        {
            MarkDataChanged(
                invalidateInstanceLookup: false
            );
        }

        return index;
    }

    private int GetOrAddSpeciesInternal(
        VegetationSpecies vegetationSpecies,
        out bool added
    )
    {
        added = false;

        if (vegetationSpecies == null)
        {
            return -1;
        }

        int index = species.IndexOf(vegetationSpecies);

        if (index >= 0)
        {
            return index;
        }

        species.Add(vegetationSpecies);

        added = true;

        return species.Count - 1;
    }

    public int AddInstance(
        VegetationSpecies vegetationSpecies,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale
    )
    {
        EnsureRuntimeCaches();

        int speciesIndex = GetOrAddSpeciesInternal(
            vegetationSpecies,
            out _
        );

        if (speciesIndex < 0)
        {
            return -1;
        }

        int persistentID = nextPersistentID++;

        VegetationInstance instance = new VegetationInstance(
            persistentID,
            speciesIndex,
            position,
            rotation,
            scale
        );

        Vector2Int coordinate =
            WorldToChunkCoordinate(position);

        VegetationChunkData chunk =
            GetOrCreateChunk(coordinate);

        chunk.AddInstance(
            instance,
            chunkSize,
            recalculateBounds: false
        );

        cachedTotalInstanceCount++;

        MarkChunkBoundsDirty(chunk);
        MarkDataChanged();

        return persistentID;
    }

    public bool HasInstanceNear(
        int speciesIndex,
        Vector3 position,
        float radius
    )
    {
        if (radius <= 0f)
        {
            return false;
        }

        float radiusSqr = radius * radius;

        Vector2Int min = WorldToChunkCoordinate(
            position - new Vector3(radius, 0f, radius)
        );

        Vector2Int max = WorldToChunkCoordinate(
            position + new Vector3(radius, 0f, radius)
        );

        for (int x = min.x; x <= max.x; x++)
        {
            for (int z = min.y; z <= max.y; z++)
            {
                VegetationChunkData chunk =
                    GetChunk(new Vector2Int(x, z));

                if (chunk == null) continue;

                for (int i = 0; i < chunk.instances.Count; i++)
                {
                    VegetationInstance instance =
                        chunk.instances[i];

                    if (instance.speciesIndex != speciesIndex)
                    {
                        continue;
                    }

                    float dx =
                        instance.position.x - position.x;

                    float dz =
                        instance.position.z - position.z;

                    if (dx * dx + dz * dz < radiusSqr)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public int RemoveInstancesInRadius(
        Vector3 position,
        float radius,
        int speciesIndex = -1
    )
    {
        EnsureRuntimeCaches();

        float radiusSqr = radius * radius;
        int removedCount = 0;

        Vector2Int min = WorldToChunkCoordinate(
            position - new Vector3(radius, 0f, radius)
        );

        Vector2Int max = WorldToChunkCoordinate(
            position + new Vector3(radius, 0f, radius)
        );

        for (int x = min.x; x <= max.x; x++)
        {
            for (int z = min.y; z <= max.y; z++)
            {
                VegetationChunkData chunk =
                    GetChunk(new Vector2Int(x, z));

                if (chunk == null) continue;

                int removedFromChunk = 0;

                for (
                    int i = chunk.instances.Count - 1;
                    i >= 0;
                    i--
                )
                {
                    VegetationInstance instance =
                        chunk.instances[i];

                    if (
                        speciesIndex >= 0 &&
                        instance.speciesIndex != speciesIndex
                    )
                    {
                        continue;
                    }

                    float dx =
                        instance.position.x - position.x;

                    float dz =
                        instance.position.z - position.z;

                    if (dx * dx + dz * dz > radiusSqr)
                    {
                        continue;
                    }

                    chunk.instances.RemoveAt(i);

                    removedFromChunk++;
                }

                if (removedFromChunk == 0)
                {
                    continue;
                }

                removedCount += removedFromChunk;

                cachedTotalInstanceCount -=
                    removedFromChunk;

                MarkChunkBoundsDirty(chunk);
            }
        }

        if (removedCount > 0)
        {
            MarkDataChanged(
                invalidateChunkLookup: true
            );
        }

        return removedCount;
    }

    public bool RemoveInstance(int persistentID)
    {
        EnsureRuntimeCaches();
        EnsureInstanceLookup();

        if (
            !instanceLookup.TryGetValue(
                persistentID,
                out InstanceLocation location
            )
        )
        {
            return false;
        }

        if (
            location.chunk == null ||
            location.chunk.instances == null
        )
        {
            return false;
        }

        if (
            location.index < 0 ||
            location.index >= location.chunk.instances.Count
        )
        {
            return false;
        }

        if (
            location.chunk.instances[location.index].persistentID !=
            persistentID
        )
        {
            instanceLookup = null;

            EnsureInstanceLookup();

            if (
                !instanceLookup.TryGetValue(
                    persistentID,
                    out location
                )
            )
            {
                return false;
            }
        }

        location.chunk.instances.RemoveAt(
            location.index
        );

        cachedTotalInstanceCount--;

        MarkChunkBoundsDirty(location.chunk);

        MarkDataChanged(
            invalidateChunkLookup:
                location.chunk.instances.Count == 0
        );

        return true;
    }

    public void RecalculateAllBounds()
    {
        if (chunks == null)
        {
            return;
        }

        for (int i = 0; i < chunks.Count; i++)
        {
            VegetationChunkData chunk = chunks[i];

            if (chunk == null)
            {
                continue;
            }

            chunk.RecalculateBounds(chunkSize);
        }
    }

    public void ClearAllInstances()
    {
        EnsureRuntimeCaches();

        if (
            (chunks == null || chunks.Count == 0) &&
            cachedTotalInstanceCount == 0
        )
        {
            return;
        }

        chunks.Clear();

        nextPersistentID = 1;
        cachedTotalInstanceCount = 0;

        chunkLookup = null;
        instanceLookup = null;

        dirtyBoundsChunks?.Clear();

        MarkDataChanged(
            invalidateChunkLookup: true
        );
    }

    public int GetTotalInstanceCount()
    {
        EnsureRuntimeCaches();

        return cachedTotalInstanceCount;
    }

    public bool TryGetInstance(
        int persistentID,
        out VegetationInstance result
    )
    {
        EnsureInstanceLookup();

        if (
            !instanceLookup.TryGetValue(
                persistentID,
                out InstanceLocation location
            ) ||
            location.chunk == null ||
            location.chunk.instances == null ||
            location.index < 0 ||
            location.index >= location.chunk.instances.Count
        )
        {
            result = default;
            return false;
        }

        VegetationInstance instance =
            location.chunk.instances[location.index];

        if (instance.persistentID != persistentID)
        {
            instanceLookup = null;

            EnsureInstanceLookup();

            if (
                !instanceLookup.TryGetValue(
                    persistentID,
                    out location
                )
            )
            {
                result = default;
                return false;
            }

            instance =
                location.chunk.instances[location.index];
        }

        result = instance;

        return true;
    }

    public bool TryFindNearestInstance(
        Vector3 position,
        float radius,
        int speciesIndex,
        out VegetationInstance result
    )
    {
        float radiusSqr = radius * radius;
        float nearestDistanceSqr = float.MaxValue;

        bool found = false;

        result = default;

        Vector2Int min = WorldToChunkCoordinate(
            position - new Vector3(radius, 0f, radius)
        );

        Vector2Int max = WorldToChunkCoordinate(
            position + new Vector3(radius, 0f, radius)
        );

        for (int x = min.x; x <= max.x; x++)
        {
            for (int z = min.y; z <= max.y; z++)
            {
                VegetationChunkData chunk =
                    GetChunk(new Vector2Int(x, z));

                if (chunk == null)
                {
                    continue;
                }

                for (int i = 0; i < chunk.instances.Count; i++)
                {
                    VegetationInstance instance =
                        chunk.instances[i];

                    if (
                        speciesIndex >= 0 &&
                        instance.speciesIndex != speciesIndex
                    )
                    {
                        continue;
                    }

                    float dx =
                        instance.position.x - position.x;

                    float dz =
                        instance.position.z - position.z;

                    float distanceSqr =
                        dx * dx + dz * dz;

                    if (
                        distanceSqr > radiusSqr ||
                        distanceSqr >= nearestDistanceSqr
                    )
                    {
                        continue;
                    }

                    nearestDistanceSqr =
                        distanceSqr;

                    result = instance;
                    found = true;
                }
            }
        }

        return found;
    }

    public bool UpdateInstanceTransform(
        int persistentID,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale
    )
    {
        EnsureInstanceLookup();

        if (
            !instanceLookup.TryGetValue(
                persistentID,
                out InstanceLocation location
            )
        )
        {
            return false;
        }

        if (
            location.chunk == null ||
            location.chunk.instances == null
        )
        {
            return false;
        }

        if (
            location.index < 0 ||
            location.index >= location.chunk.instances.Count
        )
        {
            return false;
        }

        VegetationInstance instance =
            location.chunk.instances[location.index];

        if (instance.persistentID != persistentID)
        {
            instanceLookup = null;

            EnsureInstanceLookup();

            if (
                !instanceLookup.TryGetValue(
                    persistentID,
                    out location
                )
            )
            {
                return false;
            }

            instance =
                location.chunk.instances[location.index];
        }

        VegetationChunkData oldChunk =
            location.chunk;

        instance.position = position;
        instance.rotation = rotation;
        instance.scale = scale;

        Vector2Int newCoordinate =
            WorldToChunkCoordinate(position);

        if (newCoordinate == oldChunk.coordinate)
        {
            oldChunk.instances[location.index] =
                instance;

            MarkChunkBoundsDirty(oldChunk);
            MarkDataChanged();

            return true;
        }

        oldChunk.instances.RemoveAt(
            location.index
        );

        MarkChunkBoundsDirty(oldChunk);

        VegetationChunkData newChunk =
            GetOrCreateChunk(newCoordinate);

        newChunk.AddInstance(
            instance,
            chunkSize,
            recalculateBounds: false
        );

        MarkChunkBoundsDirty(newChunk);

        MarkDataChanged(
            invalidateChunkLookup:
                oldChunk.instances.Count == 0
        );

        return true;
    }
    
    public int RemoveInstancesByPersistentIDRanges(IReadOnlyList<Vector2Int> ranges)
    {
        EnsureRuntimeCaches();

        if (ranges == null || ranges.Count == 0 || chunks == null || chunks.Count == 0)
        {
            return 0;
        }

        int removedCount = 0;

        for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
        {
            VegetationChunkData chunk = chunks[chunkIndex];

            if (chunk == null || chunk.instances == null || chunk.instances.Count == 0)
            {
                continue;
            }

            int removedFromChunk = 0;

            for (int instanceIndex = chunk.instances.Count - 1; instanceIndex >= 0; instanceIndex--)
            {
                int persistentID = chunk.instances[instanceIndex].persistentID;
                bool remove = false;

                for (int rangeIndex = 0; rangeIndex < ranges.Count; rangeIndex++)
                {
                    Vector2Int range = ranges[rangeIndex];
                    int minID = Mathf.Min(range.x, range.y);
                    int maxID = Mathf.Max(range.x, range.y);

                    if (persistentID >= minID && persistentID <= maxID)
                    {
                        remove = true;
                        break;
                    }
                }

                if (!remove)
                {
                    continue;
                }

                chunk.instances.RemoveAt(instanceIndex);
                removedFromChunk++;
            }

            if (removedFromChunk <= 0)
            {
                continue;
            }

            removedCount += removedFromChunk;
            MarkChunkBoundsDirty(chunk);
        }

        if (removedCount <= 0)
        {
            return 0;
        }

        cachedTotalInstanceCount = Mathf.Max(0, cachedTotalInstanceCount - removedCount);
        instanceLookup = null;

        MarkDataChanged(
            invalidateChunkLookup: true,
            invalidateInstanceLookup: true
        );

        return removedCount;
    }
}
