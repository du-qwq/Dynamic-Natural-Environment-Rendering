using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum VegetationDatabaseChangeType
{
    Added,
    Removed,
    TransformUpdated
}

public struct VegetationDatabaseChange
{
    public VegetationDatabaseChangeType type;
    public VegetationInstance instance;
    public Vector2Int oldChunkCoordinate;
    public Vector2Int newChunkCoordinate;

    public VegetationDatabaseChange(
        VegetationDatabaseChangeType type,
        VegetationInstance instance,
        Vector2Int oldChunkCoordinate,
        Vector2Int newChunkCoordinate
    )
    {
        this.type = type;
        this.instance = instance;
        this.oldChunkCoordinate = oldChunkCoordinate;
        this.newChunkCoordinate = newChunkCoordinate;
    }
}

public sealed class VegetationDatabaseChangeSet
{
    public readonly int fromRevision;
    public readonly int toRevision;
    public readonly bool requiresFullRebuild;
    public readonly List<int> rebuildSpeciesIndices;
    public readonly List<VegetationDatabaseChange> changes;

    public VegetationDatabaseChangeSet(
        int fromRevision,
        int toRevision,
        bool requiresFullRebuild,
        List<int> rebuildSpeciesIndices,
        List<VegetationDatabaseChange> changes
    )
    {
        this.fromRevision = fromRevision;
        this.toRevision = toRevision;
        this.requiresFullRebuild = requiresFullRebuild;
        this.rebuildSpeciesIndices = rebuildSpeciesIndices ?? new List<int>();
        this.changes = changes ?? new List<VegetationDatabaseChange>();
    }
}

public struct VegetationLegacyMigrationResult
{
    public int migratedCount;
    public int conflictCount;
    public int missingRangeCount;
    public long missingIDCount;
}

[CreateAssetMenu(fileName = "VegetationDatabase", menuName = "Vegetation/Database")]
public class VegetationDatabase : ScriptableObject
{
    [Serializable]
    private struct SpeciesIdentityRecord
    {
        public int speciesID;
        public VegetationSpecies species;

        public SpeciesIdentityRecord(int speciesID, VegetationSpecies species)
        {
            this.speciesID = speciesID;
            this.species = species;
        }
    }

    [Header("世界分块")]
    [SerializeField, HideInInspector, FormerlySerializedAs("chunkSize")]
    private float committedChunkSize = 32f;

    [SerializeField, HideInInspector]
    private float rechunkTargetSize;

    public float chunkSize
    {
        get => committedChunkSize;
        set => Rechunk(value);
    }
    public float RechunkTargetSize => rechunkTargetSize;

    [Header("植被种类")]
    [Tooltip("当前Database使用的所有VegetationSpecies")]
    public List<VegetationSpecies> species = new List<VegetationSpecies>();

    [Header("实例数据")]
    [Tooltip("世界中的所有植被Chunk")]
    public List<VegetationChunkData> chunks = new List<VegetationChunkData>();

    [SerializeField, HideInInspector] private int nextPersistentID = 1;
    [SerializeField, HideInInspector] private List<SpeciesIdentityRecord> speciesIdentityRegistry = new List<SpeciesIdentityRecord>();
    [SerializeField, HideInInspector] private int nextSpeciesID = 1;
    [SerializeField, HideInInspector] private int nextGenerationOwnerID = 1;
    [SerializeField, HideInInspector] private int nextGenerationBatchID = 1;
    [NonSerialized] private int dataRevision = 1;

    [NonSerialized] private Dictionary<VegetationSpecies, int> speciesIDBySpecies;
    [NonSerialized] private Dictionary<int, VegetationSpecies> speciesByID;
    [NonSerialized] private Dictionary<int, int> speciesIndexByID;
    [NonSerialized] private List<VegetationSpecies> activeSpeciesSnapshot;

    [NonSerialized] private Dictionary<Vector2Int, VegetationChunkData> chunkLookup;
    [NonSerialized] private Dictionary<int, InstanceLocation> instanceLookup;
    [NonSerialized] private HashSet<VegetationChunkData> dirtyBoundsChunks;
    [NonSerialized] private int cachedTotalInstanceCount;
    [NonSerialized] private bool runtimeCachesValid;
    [NonSerialized] private int batchDepth;
    [NonSerialized] private bool pendingDataChange;
    [NonSerialized] private bool pendingChunkLookupInvalidation;
    [NonSerialized] private bool pendingInstanceLookupInvalidation;
    [NonSerialized] private List<VegetationDatabaseChange> pendingChanges;
    [NonSerialized] private Dictionary<int, int> pendingChangeCountsBySpecies;
    [NonSerialized] private HashSet<int> pendingSpeciesRebuilds;
    [NonSerialized] private VegetationDatabaseChangeSet lastChangeSet;
    [NonSerialized] private List<VegetationDatabaseChangeSet> changeHistory;
    [NonSerialized] private bool pendingRequiresFullRebuild;

    private const int MaxJournaledChangesPerSpecies = 32768;
    private const int MaxChangeHistoryCount = 32;

    public int DataRevision
    {
        get
        {
            EnsureSpeciesIdentity();
            return dataRevision;
        }
    }
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

    public bool TryGetChangeSet(int fromRevision, out VegetationDatabaseChangeSet changeSet)
    {
        EnsureSpeciesIdentity();
        changeSet = lastChangeSet;

        return
            changeSet != null &&
            changeSet.fromRevision == fromRevision &&
            changeSet.toRevision == dataRevision;
    }

    public bool TryGetChangeSets(int fromRevision, List<VegetationDatabaseChangeSet> results)
    {
        if (results == null) return false;

        EnsureSpeciesIdentity();

        results.Clear();

        if (fromRevision == dataRevision)
        {
            return true;
        }

        if (changeHistory == null || changeHistory.Count == 0)
        {
            return false;
        }

        int expectedRevision = fromRevision;
        bool started = false;

        for (int i = 0; i < changeHistory.Count; i++)
        {
            VegetationDatabaseChangeSet changeSet = changeHistory[i];
            if (changeSet == null) continue;

            if (!started)
            {
                if (changeSet.fromRevision != expectedRevision) continue;
                started = true;
            }
            else if (changeSet.fromRevision != expectedRevision)
            {
                results.Clear();
                return false;
            }

            results.Add(changeSet);
            expectedRevision = changeSet.toRevision;

            if (expectedRevision == dataRevision)
            {
                return true;
            }
        }

        results.Clear();
        return false;
    }

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
        if (rechunkTargetSize == 0f) rechunkTargetSize = committedChunkSize;
        ResetRuntimeState();
        RebuildRuntimeCaches();
    }

    private void OnValidate()
    {
        if (rechunkTargetSize == 0f) rechunkTargetSize = committedChunkSize;
        ResetRuntimeState();
        RebuildRuntimeCaches();
        AdvanceRevision();
    }

    private void ResetRuntimeState()
    {
        speciesIDBySpecies = null;
        speciesByID = null;
        speciesIndexByID = null;
        activeSpeciesSnapshot = null;
        chunkLookup = null;
        instanceLookup = null;
        dirtyBoundsChunks = null;
        runtimeCachesValid = false;
        batchDepth = 0;
        pendingDataChange = false;
        pendingChunkLookupInvalidation = false;
        pendingInstanceLookupInvalidation = false;
        pendingChanges = null;
        pendingChangeCountsBySpecies = null;
        pendingSpeciesRebuilds = null;
        lastChangeSet = null;
        changeHistory = null;
        pendingRequiresFullRebuild = false;
    }

    private void EnsureSpeciesIdentity()
    {
        if (speciesIDBySpecies != null && ActiveSpeciesMatchesSnapshot()) return;

        bool hadSnapshot = activeSpeciesSnapshot != null;
        RebuildSpeciesIdentity(hadSnapshot);
    }

    private bool ActiveSpeciesMatchesSnapshot()
    {
        int count = species != null ? species.Count : 0;
        if (activeSpeciesSnapshot == null || activeSpeciesSnapshot.Count != count) return false;

        for (int i = 0; i < count; i++)
        {
            if (activeSpeciesSnapshot[i] != species[i]) return false;
        }

        return true;
    }

    private int AllocateSpeciesID()
    {
        if (nextSpeciesID <= 0 || nextSpeciesID == int.MaxValue)
        {
            throw new InvalidOperationException("VegetationDatabase has exhausted its Species IDs.");
        }

        return nextSpeciesID++;
    }

    private void RebuildSpeciesIdentity(bool hadSnapshot)
    {
        bool activeListChanged = hadSnapshot && !ActiveSpeciesMatchesSnapshot();
        bool identityChanged = false;
        bool invalidLegacyInstance = false;
        bool unknownSpeciesID = false;

        if (speciesIdentityRegistry == null)
        {
            speciesIdentityRegistry = new List<SpeciesIdentityRecord>();
            identityChanged = true;
        }

        int maxRecordedID = 0;
        Dictionary<int, int> registryIDCounts = new Dictionary<int, int>();
        for (int i = 0; i < speciesIdentityRegistry.Count; i++)
        {
            int recordedID = speciesIdentityRegistry[i].speciesID;
            maxRecordedID = Mathf.Max(maxRecordedID, recordedID);
            if (recordedID > 0)
            {
                registryIDCounts.TryGetValue(recordedID, out int count);
                registryIDCounts[recordedID] = count + 1;
            }
        }

        if (maxRecordedID == int.MaxValue)
        {
            throw new InvalidOperationException("VegetationDatabase has exhausted its Species IDs.");
        }

        int correctedNextSpeciesID = Mathf.Max(nextSpeciesID, maxRecordedID + 1, 1);
        if (nextSpeciesID != correctedNextSpeciesID)
        {
            nextSpeciesID = correctedNextSpeciesID;
            identityChanged = true;
        }
        speciesIDBySpecies = new Dictionary<VegetationSpecies, int>();
        speciesByID = new Dictionary<int, VegetationSpecies>();
        speciesIndexByID = new Dictionary<int, int>();
        Dictionary<int, int> duplicateIDRemap = new Dictionary<int, int>();
        HashSet<int> usedIDs = new HashSet<int>();
        List<SpeciesIdentityRecord> repairedRegistry = new List<SpeciesIdentityRecord>(speciesIdentityRegistry.Count);

        for (int i = 0; i < speciesIdentityRegistry.Count; i++)
        {
            SpeciesIdentityRecord record = speciesIdentityRegistry[i];

            if (record.species != null && speciesIDBySpecies.TryGetValue(record.species, out int existingID))
            {
                if (record.speciesID > 0 && record.speciesID != existingID &&
                    registryIDCounts[record.speciesID] == 1)
                {
                    duplicateIDRemap[record.speciesID] = existingID;
                }

                Debug.LogWarning("VegetationDatabase repaired duplicate Species registry record for " + record.species.name + ".", this);
                identityChanged = true;
                continue;
            }

            if (record.speciesID <= 0 || !usedIDs.Add(record.speciesID))
            {
                Debug.LogWarning("VegetationDatabase repaired an invalid or duplicate Species ID in its registry.", this);
                record.speciesID = AllocateSpeciesID();
                usedIDs.Add(record.speciesID);
                identityChanged = true;
            }

            repairedRegistry.Add(record);

            if (record.species != null)
            {
                speciesIDBySpecies.Add(record.species, record.speciesID);
                speciesByID.Add(record.speciesID, record.species);
            }
        }

        speciesIdentityRegistry = repairedRegistry;
        activeSpeciesSnapshot = new List<VegetationSpecies>();

        if (species != null)
        {
            for (int i = 0; i < species.Count; i++)
            {
                VegetationSpecies vegetationSpecies = species[i];
                activeSpeciesSnapshot.Add(vegetationSpecies);
                if (vegetationSpecies == null) continue;

                if (!speciesIDBySpecies.TryGetValue(vegetationSpecies, out int speciesID))
                {
                    speciesID = AllocateSpeciesID();
                    speciesIdentityRegistry.Add(new SpeciesIdentityRecord(speciesID, vegetationSpecies));
                    speciesIDBySpecies.Add(vegetationSpecies, speciesID);
                    speciesByID.Add(speciesID, vegetationSpecies);
                    identityChanged = true;
                }

                if (!speciesIndexByID.ContainsKey(speciesID)) speciesIndexByID.Add(speciesID, i);
            }
        }

        if (chunks != null)
        {
            for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
            {
                VegetationChunkData chunk = chunks[chunkIndex];
                if (chunk == null || chunk.instances == null) continue;

                for (int instanceIndex = 0; instanceIndex < chunk.instances.Count; instanceIndex++)
                {
                    VegetationInstance instance = chunk.instances[instanceIndex];

                    if (instance.speciesID == 0)
                    {
                        int oldIndex = instance.speciesIndex;
                        if (species != null && oldIndex >= 0 && oldIndex < species.Count &&
                            species[oldIndex] != null && speciesIDBySpecies.TryGetValue(species[oldIndex], out int migratedID))
                        {
                            instance.speciesID = migratedID;
                        }
                        else
                        {
                            invalidLegacyInstance = true;
                        }
                    }
                    else if (duplicateIDRemap.TryGetValue(instance.speciesID, out int canonicalID))
                    {
                        instance.speciesID = canonicalID;
                    }

                    int currentIndex = speciesIndexByID.TryGetValue(instance.speciesID, out int activeIndex)
                        ? activeIndex : -1;
                    if (instance.speciesID > 0 && !speciesByID.ContainsKey(instance.speciesID)) unknownSpeciesID = true;

                    if (instance.speciesIndex != currentIndex || instance.speciesID != chunk.instances[instanceIndex].speciesID)
                    {
                        instance.speciesIndex = currentIndex;
                        chunk.instances[instanceIndex] = instance;
                        identityChanged = true;
                    }
                }
            }
        }

        if (invalidLegacyInstance)
        {
            Debug.LogWarning("VegetationDatabase contains legacy instances with invalid speciesIndex; their speciesIndex is now -1 because their Species cannot be inferred.", this);
        }

        if (unknownSpeciesID)
        {
            Debug.LogWarning("VegetationDatabase contains instances whose speciesID has no registry record; their speciesIndex is now -1.", this);
        }

#if UNITY_EDITOR
        if (identityChanged && !Application.isPlaying) UnityEditor.EditorUtility.SetDirty(this);
#endif

        if (hadSnapshot && (activeListChanged || identityChanged))
        {
            RequireFullRebuild();
            MarkDataChanged(invalidateInstanceLookup: false);
        }
    }

    private void RebuildRuntimeCaches()
    {
        EnsureSpeciesIdentity();
        cachedTotalInstanceCount = 0;
        int maxPersistentID = 0;
        int maxGenerationOwnerID = 0;
        int maxGenerationBatchID = 0;

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
                    maxGenerationOwnerID = Mathf.Max(maxGenerationOwnerID, chunk.instances[instanceIndex].generationOwnerID);
                    maxGenerationBatchID = Mathf.Max(maxGenerationBatchID, chunk.instances[instanceIndex].generationBatchID);
                }
            }
        }

        nextPersistentID = AdvanceCounterPastExisting(nextPersistentID, maxPersistentID, "Persistent");
        nextGenerationOwnerID = AdvanceCounterPastExisting(nextGenerationOwnerID, maxGenerationOwnerID, "Generation Owner");
        nextGenerationBatchID = AdvanceCounterPastExisting(nextGenerationBatchID, maxGenerationBatchID, "Generation Batch");
        runtimeCachesValid = true;
    }

    private static int AdvanceCounterPastExisting(int counter, int maxExistingID, string identityName)
    {
        if (maxExistingID == int.MaxValue)
            throw new InvalidOperationException("VegetationDatabase has exhausted its " + identityName + " IDs.");
        return Mathf.Max(counter, maxExistingID + 1, 1);
    }

    private static int AllocateIdentity(ref int counter, string identityName)
    {
        if (counter <= 0 || counter == int.MaxValue)
            throw new InvalidOperationException("VegetationDatabase has exhausted its " + identityName + " IDs.");
        return counter++;
    }

    public int AllocateGenerationOwnerID()
    {
        EnsureRuntimeCaches();
        return AllocateIdentity(ref nextGenerationOwnerID, "Generation Owner");
    }

    public int AllocateGenerationBatchID()
    {
        EnsureRuntimeCaches();
        return AllocateIdentity(ref nextGenerationBatchID, "Generation Batch");
    }

    public bool ReserveGenerationOwnerID(int ownerID)
    {
        if (ownerID <= 0) throw new ArgumentOutOfRangeException(nameof(ownerID));
        if (ownerID == int.MaxValue) throw new InvalidOperationException("VegetationDatabase has exhausted its Generation Owner IDs.");
        EnsureRuntimeCaches();
        if (nextGenerationOwnerID > ownerID) return false;
        nextGenerationOwnerID = Mathf.Max(nextGenerationOwnerID, ownerID + 1);
        return true;
    }

    public bool ReserveGenerationBatchID(int batchID)
    {
        if (batchID <= 0) throw new ArgumentOutOfRangeException(nameof(batchID));
        if (batchID == int.MaxValue) throw new InvalidOperationException("VegetationDatabase has exhausted its Generation Batch IDs.");
        EnsureRuntimeCaches();
        if (nextGenerationBatchID > batchID) return false;
        nextGenerationBatchID = batchID + 1;
        return true;
    }

    private void EnsureRuntimeCaches()
    {
        EnsureSpeciesIdentity();
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
        EnsureSpeciesIdentity();
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

    private void RecordChange(VegetationDatabaseChange change)
    {
        if (pendingRequiresFullRebuild) return;

        int speciesIndex = change.instance.speciesIndex;

        if (pendingSpeciesRebuilds != null && pendingSpeciesRebuilds.Contains(speciesIndex))
        {
            return;
        }

        if (pendingChangeCountsBySpecies == null)
        {
            pendingChangeCountsBySpecies = new Dictionary<int, int>();
        }

        pendingChangeCountsBySpecies.TryGetValue(speciesIndex, out int count);
        count++;

        if (count > MaxJournaledChangesPerSpecies)
        {
            MarkSpeciesForRebuild(speciesIndex);
            return;
        }

        pendingChangeCountsBySpecies[speciesIndex] = count;

        if (pendingChanges == null)
        {
            pendingChanges = new List<VegetationDatabaseChange>();
        }

        pendingChanges.Add(change);
    }

    private void MarkSpeciesForRebuild(int speciesIndex)
    {
        if (pendingRequiresFullRebuild || speciesIndex < 0) return;

        if (pendingSpeciesRebuilds == null)
        {
            pendingSpeciesRebuilds = new HashSet<int>();
        }

        if (!pendingSpeciesRebuilds.Add(speciesIndex)) return;

        pendingChangeCountsBySpecies?.Remove(speciesIndex);

        if (pendingChanges == null || pendingChanges.Count == 0) return;

        for (int i = pendingChanges.Count - 1; i >= 0; i--)
        {
            if (pendingChanges[i].instance.speciesIndex == speciesIndex)
            {
                pendingChanges.RemoveAt(i);
            }
        }
    }

    private void RequireFullRebuild()
    {
        pendingRequiresFullRebuild = true;
        pendingChanges?.Clear();
        pendingChangeCountsBySpecies?.Clear();
        pendingSpeciesRebuilds?.Clear();
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
            int fromRevision = dataRevision;
            AdvanceRevision();

            List<int> rebuildSpeciesIndices =
                pendingSpeciesRebuilds != null
                    ? new List<int>(pendingSpeciesRebuilds)
                    : new List<int>();

            List<VegetationDatabaseChange> changes =
                pendingChanges != null
                    ? new List<VegetationDatabaseChange>(pendingChanges)
                    : new List<VegetationDatabaseChange>();

            lastChangeSet = new VegetationDatabaseChangeSet(
                fromRevision,
                dataRevision,
                pendingRequiresFullRebuild,
                rebuildSpeciesIndices,
                changes
            );

            if (changeHistory == null)
            {
                changeHistory = new List<VegetationDatabaseChangeSet>();
            }

            changeHistory.Add(lastChangeSet);

            while (changeHistory.Count > MaxChangeHistoryCount)
            {
                changeHistory.RemoveAt(0);
            }
        }

        pendingChanges?.Clear();
        pendingChangeCountsBySpecies?.Clear();
        pendingSpeciesRebuilds?.Clear();
        pendingRequiresFullRebuild = false;
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
        return WorldToChunkCoordinate(worldPosition, committedChunkSize);
    }

    public Vector2Int WorldToChunkCoordinate(Vector3 worldPosition, float explicitChunkSize)
    {
        if (!IsValidChunkSize(explicitChunkSize))
            throw new ArgumentOutOfRangeException(nameof(explicitChunkSize), "Chunk Size must be finite and greater than zero.");

        return new Vector2Int(
            FloorChunkCoordinate(worldPosition.x, explicitChunkSize),
            FloorChunkCoordinate(worldPosition.z, explicitChunkSize)
        );
    }

    private static int FloorChunkCoordinate(float worldCoordinate, float size)
    {
        double coordinate = Math.Floor((double)worldCoordinate / size);
        if (double.IsNaN(coordinate) || double.IsInfinity(coordinate) ||
            coordinate < int.MinValue || coordinate > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(worldCoordinate), "World position exceeds the supported Chunk coordinate range.");
        return (int)coordinate;
    }

    public static bool IsValidChunkSize(float size)
    {
        return size > 0f && !float.IsNaN(size) && !float.IsInfinity(size);
    }

    public bool Rechunk(float targetChunkSize)
    {
        if (!IsValidChunkSize(targetChunkSize)) return false;
        if (!IsValidChunkSize(committedChunkSize))
            throw new InvalidOperationException("VegetationDatabase has an invalid committed Chunk Size.");
        if (Mathf.Approximately(targetChunkSize, committedChunkSize)) return false;

        int instanceCount = 0;
        Dictionary<Vector2Int, VegetationChunkData> newChunkLookup = new Dictionary<Vector2Int, VegetationChunkData>();
        Dictionary<int, InstanceLocation> newInstanceLookup = new Dictionary<int, InstanceLocation>();
        List<VegetationChunkData> newChunks = new List<VegetationChunkData>();

        if (chunks != null)
        {
            for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
            {
                VegetationChunkData oldChunk = chunks[chunkIndex];
                if (oldChunk == null || oldChunk.instances == null) continue;

                for (int instanceIndex = 0; instanceIndex < oldChunk.instances.Count; instanceIndex++)
                {
                    VegetationInstance instance = oldChunk.instances[instanceIndex];
                    Vector2Int coordinate = WorldToChunkCoordinate(instance.position, targetChunkSize);
                    if (!newChunkLookup.TryGetValue(coordinate, out VegetationChunkData newChunk))
                    {
                        newChunk = new VegetationChunkData(coordinate, targetChunkSize);
                        newChunkLookup.Add(coordinate, newChunk);
                        newChunks.Add(newChunk);
                    }

                    int newIndex = newChunk.instances.Count;
                    newChunk.instances.Add(instance);
                    newInstanceLookup[instance.persistentID] = new InstanceLocation(newChunk, newIndex);
                    checked { instanceCount++; }
                }
            }
        }

        int verifiedCount = 0;
        for (int i = 0; i < newChunks.Count; i++)
        {
            VegetationChunkData newChunk = newChunks[i];
            if (newChunk.instances.Count == 0) throw new InvalidOperationException("Rechunk produced an empty Chunk.");
            newChunk.RecalculateBounds(targetChunkSize);
            checked { verifiedCount += newChunk.instances.Count; }
        }

        if (verifiedCount != instanceCount)
            throw new InvalidOperationException("Rechunk instance count validation failed.");

        chunks = newChunks;
        committedChunkSize = targetChunkSize;
        rechunkTargetSize = targetChunkSize;
        chunkLookup = newChunkLookup;
        instanceLookup = newInstanceLookup;
        dirtyBoundsChunks = null;
        cachedTotalInstanceCount = instanceCount;
        runtimeCachesValid = true;

        RequireFullRebuild();
        MarkDataChanged(invalidateChunkLookup: false, invalidateInstanceLookup: false);
        return true;
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

        EnsureSpeciesIdentity();

        if (!speciesIDBySpecies.TryGetValue(vegetationSpecies, out int speciesID)) return -1;
        return speciesIndexByID.TryGetValue(speciesID, out int index) ? index : -1;
    }

    public int GetSpeciesID(VegetationSpecies vegetationSpecies)
    {
        if (vegetationSpecies == null) return 0;

        EnsureSpeciesIdentity();
        return speciesIDBySpecies.TryGetValue(vegetationSpecies, out int speciesID) ? speciesID : 0;
    }

    public VegetationSpecies GetSpeciesByID(int speciesID)
    {
        EnsureSpeciesIdentity();
        return speciesByID.TryGetValue(speciesID, out VegetationSpecies vegetationSpecies)
            ? vegetationSpecies : null;
    }

    public int GetSpeciesIndexByID(int speciesID)
    {
        EnsureSpeciesIdentity();
        return speciesIndexByID.TryGetValue(speciesID, out int index) ? index : -1;
    }

    public int GetOrAddSpecies(VegetationSpecies vegetationSpecies)
    {
        return GetOrAddSpeciesInternal(vegetationSpecies, out _);
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

        EnsureSpeciesIdentity();
        if (species == null) species = new List<VegetationSpecies>();

        int index = GetSpeciesIndex(vegetationSpecies);

        if (index >= 0)
        {
            return index;
        }

        species.Add(vegetationSpecies);

        added = true;

        EnsureSpeciesIdentity();

        return species.Count - 1;
    }

    public int AddInstance(
        VegetationSpecies vegetationSpecies,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale
    )
    {
        return AddInstance(vegetationSpecies, position, rotation, scale, VegetationInstanceSource.Manual, 0, 0);
    }

    public int AddInstance(
        VegetationSpecies vegetationSpecies,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        VegetationInstanceSource sourceType,
        int generationOwnerID,
        int generationBatchID
    )
    {
        if (sourceType == VegetationInstanceSource.Procedural)
        {
            if (generationOwnerID <= 0 || generationBatchID <= 0)
                throw new ArgumentException("Procedural instances require positive Generation Owner and Batch IDs.");
        }
        else if (generationOwnerID != 0 || generationBatchID != 0)
        {
            throw new ArgumentException("Manual and Imported instances must have zero Generation Owner and Batch IDs.");
        }

        EnsureRuntimeCaches();

        if (sourceType == VegetationInstanceSource.Procedural)
        {
            ReserveGenerationOwnerID(generationOwnerID);
            ReserveGenerationBatchID(generationBatchID);
        }

        int speciesIndex = GetOrAddSpeciesInternal(
            vegetationSpecies,
            out bool speciesAdded
        );

        if (speciesIndex < 0)
        {
            return -1;
        }

        int persistentID = AllocateIdentity(ref nextPersistentID, "Persistent");

        VegetationInstance instance = new VegetationInstance(
            persistentID,
            GetSpeciesID(vegetationSpecies),
            speciesIndex,
            position,
            rotation,
            scale,
            sourceType,
            generationOwnerID,
            generationBatchID
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

        if (speciesAdded)
        {
            RequireFullRebuild();
        }

        RecordChange(new VegetationDatabaseChange(
            VegetationDatabaseChangeType.Added,
            instance,
            coordinate,
            coordinate
        ));

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
        EnsureSpeciesIdentity();
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

    public int RemoveInstancesInRadius(Vector3 position, float radius, int speciesIndex = -1)
    {
        EnsureRuntimeCaches();

        float radiusSqr = radius * radius;
        int removedCount = 0;
        Vector2Int min = WorldToChunkCoordinate(position - new Vector3(radius, 0f, radius));
        Vector2Int max = WorldToChunkCoordinate(position + new Vector3(radius, 0f, radius));

        for (int x = min.x; x <= max.x; x++)
        {
            for (int z = min.y; z <= max.y; z++)
            {
                VegetationChunkData chunk = GetChunk(new Vector2Int(x, z));
                if (chunk == null || chunk.instances == null || chunk.instances.Count == 0) continue;

                List<VegetationInstance> list = chunk.instances;
                int originalCount = list.Count;
                int writeIndex = 0;

                for (int readIndex = 0; readIndex < originalCount; readIndex++)
                {
                    VegetationInstance instance = list[readIndex];
                    bool speciesMatches = speciesIndex < 0 || instance.speciesIndex == speciesIndex;
                    float dx = instance.position.x - position.x;
                    float dz = instance.position.z - position.z;
                    bool remove = speciesMatches && dx * dx + dz * dz <= radiusSqr;

                    if (remove)
                    {
                        RecordChange(new VegetationDatabaseChange(VegetationDatabaseChangeType.Removed, instance, chunk.coordinate, chunk.coordinate));
                        removedCount++;
                        continue;
                    }

                    if (writeIndex != readIndex) list[writeIndex] = instance;
                    writeIndex++;
                }

                int removedFromChunk = originalCount - writeIndex;
                if (removedFromChunk <= 0) continue;

                list.RemoveRange(writeIndex, removedFromChunk);
                cachedTotalInstanceCount -= removedFromChunk;
                MarkChunkBoundsDirty(chunk);
            }
        }

        if (removedCount > 0) MarkDataChanged(invalidateChunkLookup: true);
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

        VegetationInstance removedInstance =
            location.chunk.instances[location.index];

        RecordChange(new VegetationDatabaseChange(
            VegetationDatabaseChangeType.Removed,
            removedInstance,
            location.chunk.coordinate,
            location.chunk.coordinate
        ));

        int lastIndex = location.chunk.instances.Count - 1;
        if (location.index != lastIndex) location.chunk.instances[location.index] = location.chunk.instances[lastIndex];
        location.chunk.instances.RemoveAt(lastIndex);

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

        cachedTotalInstanceCount = 0;

        chunkLookup = null;
        instanceLookup = null;

        dirtyBoundsChunks?.Clear();

        RequireFullRebuild();
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
        EnsureSpeciesIdentity();
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

            RecordChange(new VegetationDatabaseChange(
                VegetationDatabaseChangeType.TransformUpdated,
                instance,
                oldChunk.coordinate,
                newCoordinate
            ));

            MarkChunkBoundsDirty(oldChunk);
            MarkDataChanged();

            return true;
        }

        int lastIndex = oldChunk.instances.Count - 1;
        if (location.index != lastIndex) oldChunk.instances[location.index] = oldChunk.instances[lastIndex];
        oldChunk.instances.RemoveAt(lastIndex);

        MarkChunkBoundsDirty(oldChunk);

        VegetationChunkData newChunk =
            GetOrCreateChunk(newCoordinate);

        newChunk.AddInstance(
            instance,
            chunkSize,
            recalculateBounds: false
        );

        RecordChange(new VegetationDatabaseChange(
            VegetationDatabaseChangeType.TransformUpdated,
            instance,
            oldChunk.coordinate,
            newCoordinate
        ));

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
        if (ranges == null || ranges.Count == 0 || chunks == null || chunks.Count == 0) return 0;

        int removedCount = 0;

        for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
        {
            VegetationChunkData chunk = chunks[chunkIndex];
            if (chunk == null || chunk.instances == null || chunk.instances.Count == 0) continue;

            List<VegetationInstance> list = chunk.instances;
            int originalCount = list.Count;
            int writeIndex = 0;

            for (int readIndex = 0; readIndex < originalCount; readIndex++)
            {
                VegetationInstance instance = list[readIndex];
                int persistentID = instance.persistentID;
                bool remove = false;

                for (int rangeIndex = 0; rangeIndex < ranges.Count; rangeIndex++)
                {
                    Vector2Int range = ranges[rangeIndex];
                    int minID = Mathf.Min(range.x, range.y);
                    int maxID = Mathf.Max(range.x, range.y);
                    if (persistentID < minID || persistentID > maxID) continue;
                    remove = true;
                    break;
                }

                if (remove)
                {
                    RecordChange(new VegetationDatabaseChange(VegetationDatabaseChangeType.Removed, instance, chunk.coordinate, chunk.coordinate));
                    removedCount++;
                    continue;
                }

                if (writeIndex != readIndex) list[writeIndex] = instance;
                writeIndex++;
            }

            int removedFromChunk = originalCount - writeIndex;
            if (removedFromChunk <= 0) continue;

            list.RemoveRange(writeIndex, removedFromChunk);
            MarkChunkBoundsDirty(chunk);
        }

        if (removedCount <= 0) return 0;

        cachedTotalInstanceCount = Mathf.Max(0, cachedTotalInstanceCount - removedCount);
        instanceLookup = null;
        MarkDataChanged(invalidateChunkLookup: true, invalidateInstanceLookup: true);
        return removedCount;
    }

    public VegetationLegacyMigrationResult MigrateLegacyGenerationOwnership(
        int ownerID, int batchID, IReadOnlyList<Vector2Int> ranges, Func<int, bool> hasConflictingClaim = null)
    {
        if (ownerID <= 0 || batchID <= 0) throw new ArgumentOutOfRangeException("Generation Owner and Batch IDs must be positive.");
        EnsureRuntimeCaches();
        ReserveGenerationOwnerID(ownerID);
        ReserveGenerationBatchID(batchID);

        VegetationLegacyMigrationResult result = default;
        if (ranges == null || ranges.Count == 0) return result;

        int[] matchedCounts = new int[ranges.Count];
        HashSet<int> countedIDs = new HashSet<int>();
        if (chunks != null)
        {
            for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
            {
                VegetationChunkData chunk = chunks[chunkIndex];
                if (chunk == null || chunk.instances == null) continue;

                for (int instanceIndex = 0; instanceIndex < chunk.instances.Count; instanceIndex++)
                {
                    VegetationInstance instance = chunk.instances[instanceIndex];
                    bool firstOccurrence = countedIDs.Add(instance.persistentID);
                    bool matched = false;
                    for (int rangeIndex = 0; rangeIndex < ranges.Count; rangeIndex++)
                    {
                        Vector2Int range = ranges[rangeIndex];
                        if (instance.persistentID < Mathf.Min(range.x, range.y) ||
                            instance.persistentID > Mathf.Max(range.x, range.y)) continue;
                        if (firstOccurrence) matchedCounts[rangeIndex]++;
                        matched = true;
                    }

                    if (!matched) continue;
                    if ((hasConflictingClaim != null && hasConflictingClaim(instance.persistentID)) ||
                        (instance.sourceType == VegetationInstanceSource.Procedural && instance.generationOwnerID != ownerID) ||
                        instance.sourceType == VegetationInstanceSource.Imported)
                    {
                        result.conflictCount++;
                        continue;
                    }

                    if (instance.sourceType == VegetationInstanceSource.Procedural) continue;
                    instance.sourceType = VegetationInstanceSource.Procedural;
                    instance.generationOwnerID = ownerID;
                    instance.generationBatchID = batchID;
                    chunk.instances[instanceIndex] = instance;
                    result.migratedCount++;
                }
            }
        }

        for (int i = 0; i < matchedCounts.Length; i++)
        {
            Vector2Int range = ranges[i];
            long expectedCount = (long)Mathf.Max(range.x, range.y) - Mathf.Min(range.x, range.y) + 1;
            result.missingIDCount += Math.Max(0L, expectedCount - matchedCounts[i]);
            if (matchedCounts[i] == 0) result.missingRangeCount++;
        }

        if (result.migratedCount > 0) MarkDataChanged(invalidateInstanceLookup: false);
        return result;
    }

    public int RemoveInstancesByGenerationOwner(int ownerID)
    {
        if (ownerID <= 0) return 0;
        EnsureRuntimeCaches();
        if (chunks == null || chunks.Count == 0) return 0;

        int removedCount = 0;
        for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
        {
            VegetationChunkData chunk = chunks[chunkIndex];
            if (chunk == null || chunk.instances == null || chunk.instances.Count == 0) continue;

            List<VegetationInstance> list = chunk.instances;
            int writeIndex = 0;
            int originalCount = list.Count;
            for (int readIndex = 0; readIndex < originalCount; readIndex++)
            {
                VegetationInstance instance = list[readIndex];
                if (instance.sourceType == VegetationInstanceSource.Procedural && instance.generationOwnerID == ownerID)
                {
                    RecordChange(new VegetationDatabaseChange(VegetationDatabaseChangeType.Removed, instance, chunk.coordinate, chunk.coordinate));
                    removedCount++;
                    continue;
                }

                if (writeIndex != readIndex) list[writeIndex] = instance;
                writeIndex++;
            }

            int removedFromChunk = originalCount - writeIndex;
            if (removedFromChunk <= 0) continue;
            list.RemoveRange(writeIndex, removedFromChunk);
            MarkChunkBoundsDirty(chunk);
        }

        if (removedCount == 0) return 0;
        cachedTotalInstanceCount -= removedCount;
        instanceLookup = null;
        MarkDataChanged(invalidateChunkLookup: true);
        return removedCount;
    }
}
