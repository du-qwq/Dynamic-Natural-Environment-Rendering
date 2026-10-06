using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class VegetationGameObjectBakerWindow : EditorWindow
{
    private const string SpeciesMarkerPrefix = "VegetationSpeciesFactory:v1";
    private const string BakedSourcesContainerName = "_BakedSources";

    private enum SpeciesMatchMethod
    {
        None,
        PrefabGuid,
        RenderingSignature,
        Manual
    }

    private enum BakeSourceHandling
    {
        Keep,
        DisableRenderers,
        DisableGameObject,
        Delete
    }

    [Serializable]
    private class SourceLOD
    {
        public Mesh mesh;
        public Material[] materials = Array.Empty<Material>();

        public bool IsValid => mesh != null && materials != null && materials.Length > 0;
    }

    [Serializable]
    private class SourceInstance
    {
        public Transform transform;
        public GameObject sourceObject;
        public GameObject sourcePrefab;
        public string sourcePrefabGuid;

        [NonSerialized]
        public readonly List<Renderer> renderersToDisable = new List<Renderer>();
    }

    [Serializable]
    private class SourceGroup
    {
        public SourceLOD lod0 = new SourceLOD();
        public SourceLOD lod1 = new SourceLOD();
        public SourceLOD lod2 = new SourceLOD();
        public SourceLOD lod3 = new SourceLOD();

        public GameObject sourcePrefab;
        public string sourcePrefabGuid;
        public VegetationSpecies species;
        public int count;
        public bool expanded;

        [NonSerialized]
        public SpeciesMatchMethod matchMethod;

        [NonSerialized]
        public readonly List<SourceInstance> instances = new List<SourceInstance>();

        [NonSerialized]
        public readonly List<string> warnings = new List<string>();

        [NonSerialized]
        public readonly List<string> errors = new List<string>();
    }

    private GameObject sourceRoot;
    private VegetationDatabase database;
    private VegetationRenderer vegetationRenderer;

    private bool includeInactive = true;
    private bool skipExistingInstances = true;

    private BakeSourceHandling sourceHandling = BakeSourceHandling.DisableRenderers;
    private bool organizeBakedSources = true;

    private float duplicatePositionTolerance = 0.02f;
    private float duplicateRotationTolerance = 0.5f;
    private float duplicateScaleTolerance = 0.01f;

    private int scannedInstanceCount;
    private int skippedObjectCount;
    private int unsupportedLODGroupCount;
    private int invalidMaterialLayoutCount;

    private int previewNewCount;
    private int previewDuplicateCount;
    private int previewMissingSpeciesCount;
    private int previewInvalidCount;
    private bool previewDirty = true;

    private Vector2 scrollPosition;

    private readonly List<SourceGroup> sourceGroups = new List<SourceGroup>();
    private readonly List<VegetationSpecies> speciesCandidates = new List<VegetationSpecies>();

    [MenuItem("Tools/Vegetation/GameObject Baker")]
    public static void Open()
    {
        GetWindow<VegetationGameObjectBakerWindow>("Vegetation Baker");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("GameObject → GPU Vegetation", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "这是一次性场景迁移工具：Species 由 Species Factory 负责创建；Baker 只把已经摆好的 GameObject Transform 转成 Vegetation Instance。Bake 后请以 VegetationDatabase / Painter 为正式数据源。",
            MessageType.Info
        );

        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        sourceRoot = (GameObject)EditorGUILayout.ObjectField("Source Root", sourceRoot, typeof(GameObject), true);
        database = (VegetationDatabase)EditorGUILayout.ObjectField("Database", database, typeof(VegetationDatabase), false);
        vegetationRenderer = (VegetationRenderer)EditorGUILayout.ObjectField("Renderer", vegetationRenderer, typeof(VegetationRenderer), true);
        includeInactive = EditorGUILayout.Toggle("Include Inactive", includeInactive);
        if (EditorGUI.EndChangeCheck())
        {
            previewDirty = true;
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Scan Source Root", GUILayout.Height(28f)))
        {
            Scan();
        }

        DrawBakedSourcesManagement();

        if (sourceGroups.Count == 0)
        {
            return;
        }

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            $"扫描实例: {scannedInstanceCount}\n" +
            $"资源分组: {sourceGroups.Count}\n" +
            $"跳过对象: {skippedObjectCount}\n" +
            $"不支持的 LODGroup: {unsupportedLODGroupCount}\n" +
            $"SubMesh / Material 布局错误: {invalidMaterialLayoutCount}",
            MessageType.Info
        );

        if (unsupportedLODGroupCount > 0)
        {
            EditorGUILayout.HelpBox(
                "当前一个 Vegetation LOD 只支持一个 MeshRenderer。一个 LOD 内包含多个 Renderer 的 LODGroup 不会被 Bake。",
                MessageType.Warning
            );
        }

        if (invalidMaterialLayoutCount > 0)
        {
            EditorGUILayout.HelpBox(
                "检测到 Mesh.subMeshCount 与 Renderer Material 数量不一致的对象。这类对象无法被当前 Runtime 完整还原，已直接排除，而不是带警告继续 Bake。",
                MessageType.Error
            );
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Species Mapping", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.MinHeight(220f), GUILayout.MaxHeight(520f));
        for (int i = 0; i < sourceGroups.Count; i++)
        {
            DrawSourceGroup(sourceGroups[i], i);
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Bake Settings", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        sourceHandling = (BakeSourceHandling)EditorGUILayout.EnumPopup("After Bake", sourceHandling);
        organizeBakedSources = EditorGUILayout.Toggle("Move To _BakedSources", organizeBakedSources);
        skipExistingInstances = EditorGUILayout.Toggle("Skip Existing Instances", skipExistingInstances);

        if (skipExistingInstances)
        {
            duplicatePositionTolerance = Mathf.Max(0.0001f, EditorGUILayout.FloatField("Position Tolerance", duplicatePositionTolerance));
            duplicateRotationTolerance = Mathf.Max(0f, EditorGUILayout.FloatField("Rotation Tolerance (°)", duplicateRotationTolerance));
            duplicateScaleTolerance = Mathf.Max(0f, EditorGUILayout.FloatField("Scale Tolerance", duplicateScaleTolerance));
        }

        if (EditorGUI.EndChangeCheck())
        {
            previewDirty = true;
        }

        DrawSourceHandlingHelp();

        EditorGUILayout.Space();
        DrawBakePreview();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Refresh Preview", GUILayout.Height(26f)))
        {
            CalculatePreview();
        }

        using (new EditorGUI.DisabledScope(database == null || CountReadyInstances() == 0))
        {
            if (GUILayout.Button("Bake Ready Instances", GUILayout.Height(34f)))
            {
                Bake();
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawBakedSourcesManagement()
    {
        if (sourceRoot == null) return;

        GameObject archive = FindBakedSourcesContainer();
        int archivedCount = CountArchivedSourceObjects(archive);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Baked Source Archive", EditorStyles.boldLabel);

        if (archive == null)
        {
            EditorGUILayout.HelpBox(
                "还没有 _BakedSources。启用 Move To _BakedSources 后，成功 Bake 的源对象会被移到专用层级，避免继续混在 Authoring Root 中。",
                MessageType.Info
            );
            return;
        }

        EditorGUILayout.HelpBox(
            $"Archive: {GetHierarchyPath(archive.transform)}\nArchived Root Objects: {archivedCount}\n清理归档只会删除这里保留的源 GameObject，不会删除 VegetationDatabase 中已经生成的 GPU Instance。",
            MessageType.Info
        );

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Select _BakedSources", GUILayout.Height(24f)))
        {
            Selection.activeGameObject = archive;
            EditorGUIUtility.PingObject(archive);
        }

        using (new EditorGUI.DisabledScope(archivedCount == 0))
        {
            if (GUILayout.Button("Delete Archived Sources", GUILayout.Height(24f)))
            {
                DeleteArchivedSources(archive, archivedCount);
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DeleteArchivedSources(GameObject archive, int archivedCount)
    {
        if (archive == null) return;

        bool confirmed = EditorUtility.DisplayDialog(
            "Delete Archived Vegetation Sources?",
            $"将删除 _BakedSources 中归档的 {archivedCount} 个根对象。\n\nVegetationDatabase / GPU Instance 不会被删除。\n该操作支持 Unity Undo。",
            "Delete Archived Sources",
            "Cancel"
        );

        if (!confirmed) return;

        Undo.DestroyObjectImmediate(archive);

        if (sourceRoot != null && sourceRoot.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(sourceRoot.scene);
        }

        Repaint();
    }

    private static int CountArchivedSourceObjects(GameObject archive)
    {
        return archive != null ? archive.transform.childCount : 0;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null) return "<None>";

        string path = transform.name;
        Transform parent = transform.parent;

        while (parent != null)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    private void DrawSourceHandlingHelp()
    {
        switch (sourceHandling)
        {
            case BakeSourceHandling.Keep:
                EditorGUILayout.HelpBox(
                    "Keep：源 Renderer 继续显示，会与 GPU Instance 重叠。只建议短暂对比测试。",
                    MessageType.Warning
                );
                break;

            case BakeSourceHandling.DisableRenderers:
                EditorGUILayout.HelpBox(
                    "Disable Renderers（推荐）：保留源 GameObject / Script / Collider，只关闭参与 Bake 的 Renderer，便于检查和 Undo。",
                    MessageType.Info
                );
                break;

            case BakeSourceHandling.DisableGameObject:
                EditorGUILayout.HelpBox(
                    "Disable GameObject：会关闭整个源对象，因此该对象上的 Script / Collider / Trigger 也会停止工作。",
                    MessageType.Warning
                );
                break;

            case BakeSourceHandling.Delete:
                EditorGUILayout.HelpBox(
                    "Delete：Bake 后删除源对象。该操作支持 Unity Undo，但仍建议只在确认转换结果后使用。",
                    MessageType.Error
                );
                break;
        }

        if (organizeBakedSources && sourceHandling != BakeSourceHandling.Delete)
        {
            EditorGUILayout.HelpBox(
                "Move To _BakedSources：Bake 成功（包括检测为已存在的 Duplicate）的源对象会在应用上面的 Source Handling 后统一归档。归档对象会保持世界空间 Transform。",
                MessageType.Info
            );
        }
    }

    private void DrawSourceGroup(SourceGroup group, int index)
    {
        EditorGUILayout.BeginVertical("box");

        string status = GetGroupStatusLabel(group);
        group.expanded = EditorGUILayout.Foldout(
            group.expanded,
            $"{status}  Group {index + 1}    Instances: {group.count}    {group.lod0.mesh?.name ?? "No Mesh"}",
            true
        );

        if (!group.expanded)
        {
            EditorGUILayout.EndVertical();
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Source Prefab", group.sourcePrefab, typeof(GameObject), false);
            EditorGUILayout.TextField("Prefab GUID", string.IsNullOrEmpty(group.sourcePrefabGuid) ? "<Unpacked / None>" : group.sourcePrefabGuid);
            EditorGUILayout.TextField("Auto Match", GetMatchMethodLabel(group.matchMethod));
        }

        DrawLODInfo("LOD0", group.lod0);
        if (group.lod1.IsValid) DrawLODInfo("LOD1", group.lod1);
        if (group.lod2.IsValid) DrawLODInfo("LOD2", group.lod2);
        if (group.lod3.IsValid) DrawLODInfo("LOD3", group.lod3);

        EditorGUI.BeginChangeCheck();
        VegetationSpecies newSpecies = (VegetationSpecies)EditorGUILayout.ObjectField(
            "Species",
            group.species,
            typeof(VegetationSpecies),
            false
        );
        if (EditorGUI.EndChangeCheck())
        {
            group.species = newSpecies;
            group.matchMethod = newSpecies != null ? SpeciesMatchMethod.Manual : SpeciesMatchMethod.None;
            previewDirty = true;
        }

        if (group.species == null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("没有找到对应 VegetationSpecies。该组会被跳过。", MessageType.Warning);

            using (new EditorGUI.DisabledScope(group.sourcePrefab == null))
            {
                if (GUILayout.Button("Send To Species Factory", GUILayout.Width(175f), GUILayout.Height(38f)))
                {
                    VegetationSpeciesFactoryWindow.OpenWithPrefab(group.sourcePrefab, database);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        else if (!SpeciesRenderingMatchesSource(group.species, group))
        {
            EditorGUILayout.HelpBox(
                "当前 Species 的 LOD Mesh / SubMesh Material 结构与源对象不一致。为避免把场景对象转换成错误渲染资源，该组不会 Bake。",
                MessageType.Error
            );
        }

        for (int i = 0; i < group.warnings.Count; i++)
        {
            EditorGUILayout.HelpBox(group.warnings[i], MessageType.Warning);
        }

        for (int i = 0; i < group.errors.Count; i++)
        {
            EditorGUILayout.HelpBox(group.errors[i], MessageType.Error);
        }

        EditorGUILayout.EndVertical();
    }

    private static string GetGroupStatusLabel(SourceGroup group)
    {
        if (group.errors.Count > 0) return "[Invalid]";
        if (group.species == null) return "[Needs Species]";
        if (!SpeciesRenderingMatchesSource(group.species, group)) return "[Mismatch]";
        return "[Ready]";
    }

    private static string GetMatchMethodLabel(SpeciesMatchMethod method)
    {
        switch (method)
        {
            case SpeciesMatchMethod.PrefabGuid: return "Prefab GUID";
            case SpeciesMatchMethod.RenderingSignature: return "Rendering Signature";
            case SpeciesMatchMethod.Manual: return "Manual";
            default: return "None";
        }
    }

    private static void DrawLODInfo(string label, SourceLOD lod)
    {
        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Mesh", lod.mesh, typeof(Mesh), false);
            EditorGUILayout.IntField("SubMeshes", lod.mesh != null ? lod.mesh.subMeshCount : 0);
            EditorGUILayout.IntField("Source Materials", lod.materials != null ? lod.materials.Length : 0);
        }
    }

    private void DrawBakePreview()
    {
        EditorGUILayout.LabelField("Bake Preview", EditorStyles.boldLabel);

        if (previewDirty)
        {
            EditorGUILayout.HelpBox(
                "Preview 已过期。Bake 前会自动重新计算，也可以手动点击 Refresh Preview。",
                MessageType.Info
            );
        }

        EditorGUILayout.HelpBox(
            $"New Instances: {previewNewCount}\n" +
            $"Existing Duplicates: {previewDuplicateCount}\n" +
            $"Missing Species: {previewMissingSpeciesCount}\n" +
            $"Invalid / Mismatch: {previewInvalidCount}",
            previewInvalidCount > 0 || previewMissingSpeciesCount > 0 ? MessageType.Warning : MessageType.Info
        );
    }

    private void Scan()
    {
        sourceGroups.Clear();
        speciesCandidates.Clear();

        scannedInstanceCount = 0;
        skippedObjectCount = 0;
        unsupportedLODGroupCount = 0;
        invalidMaterialLayoutCount = 0;

        ResetPreview();

        if (sourceRoot == null)
        {
            Debug.LogWarning("Vegetation Baker：请先指定 Source Root。");
            return;
        }

        CollectSpeciesCandidates();

        MeshRenderer[] renderers = sourceRoot.GetComponentsInChildren<MeshRenderer>(includeInactive);
        Dictionary<string, SourceGroup> lookup = new Dictionary<string, SourceGroup>();
        HashSet<LODGroup> processedLODGroups = new HashSet<LODGroup>();

        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer meshRenderer = renderers[i];
            if (meshRenderer == null) continue;

            LODGroup lodGroup = meshRenderer.GetComponentInParent<LODGroup>();

            if (lodGroup != null)
            {
                if (processedLODGroups.Contains(lodGroup)) continue;
                processedLODGroups.Add(lodGroup);

                if (!TryCreateLODGroupSource(lodGroup, out SourceGroup source, out SourceInstance sourceInstance, out string error))
                {
                    unsupportedLODGroupCount++;
                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogWarning($"Vegetation Baker：跳过 LODGroup '{lodGroup.name}'：{error}", lodGroup);
                    }
                    continue;
                }

                AddToGroupedSources(lookup, source, sourceInstance);
                scannedInstanceCount++;
                continue;
            }

            if (!TryCreateStandaloneSource(meshRenderer, out SourceGroup standaloneSource, out SourceInstance standaloneInstance, out string standaloneError))
            {
                skippedObjectCount++;
                if (!string.IsNullOrEmpty(standaloneError))
                {
                    Debug.LogWarning($"Vegetation Baker：跳过 '{meshRenderer.name}'：{standaloneError}", meshRenderer);
                }
                continue;
            }

            AddToGroupedSources(lookup, standaloneSource, standaloneInstance);
            scannedInstanceCount++;
        }

        CalculatePreview();
        Repaint();
    }

    private bool TryCreateStandaloneSource(
        MeshRenderer renderer,
        out SourceGroup source,
        out SourceInstance instance,
        out string error
    )
    {
        source = null;
        instance = null;
        error = null;

        if (!TryBuildSourceLOD(renderer, out SourceLOD lod0, out error))
        {
            if (IsMaterialLayoutError(error)) invalidMaterialLayoutCount++;
            return false;
        }

        ResolvePrefabIdentity(renderer.gameObject, out GameObject sourcePrefab, out string sourcePrefabGuid);

        source = new SourceGroup
        {
            lod0 = lod0,
            sourcePrefab = sourcePrefab,
            sourcePrefabGuid = sourcePrefabGuid
        };

        instance = new SourceInstance
        {
            transform = renderer.transform,
            sourceObject = renderer.gameObject,
            sourcePrefab = sourcePrefab,
            sourcePrefabGuid = sourcePrefabGuid
        };

        instance.renderersToDisable.Add(renderer);
        return true;
    }

    private bool TryCreateLODGroupSource(
        LODGroup lodGroup,
        out SourceGroup source,
        out SourceInstance instance,
        out string error
    )
    {
        source = null;
        instance = null;
        error = null;

        LOD[] lods = lodGroup.GetLODs();
        if (lods == null || lods.Length == 0)
        {
            error = "没有任何 LOD。";
            return false;
        }

        if (!TryGetSingleMeshRenderer(lods[0], out MeshRenderer lod0Renderer))
        {
            error = "LOD0 必须且只能包含一个 MeshRenderer。";
            return false;
        }

        if (!TryBuildSourceLOD(lod0Renderer, out SourceLOD lod0, out error))
        {
            if (IsMaterialLayoutError(error)) invalidMaterialLayoutCount++;
            return false;
        }

        SourceLOD lod1 = new SourceLOD();
        SourceLOD lod2 = new SourceLOD();
        SourceLOD lod3 = new SourceLOD();

        if (lods.Length > 1 && !TryGetOptionalLOD(lods[1], out lod1, out error)) return false;
        if (lods.Length > 2 && !TryGetOptionalLOD(lods[2], out lod2, out error)) return false;
        if (lods.Length > 3 && !TryGetOptionalLOD(lods[3], out lod3, out error)) return false;

        if (IsMaterialLayoutError(error)) invalidMaterialLayoutCount++;

        ResolvePrefabIdentity(lodGroup.gameObject, out GameObject sourcePrefab, out string sourcePrefabGuid);

        source = new SourceGroup
        {
            lod0 = lod0,
            lod1 = lod1,
            lod2 = lod2,
            lod3 = lod3,
            sourcePrefab = sourcePrefab,
            sourcePrefabGuid = sourcePrefabGuid
        };

        if (lods.Length > 4)
        {
            source.warnings.Add($"Source LODGroup 有 {lods.Length} 层；当前 VegetationSpecies 只使用 LOD0~LOD3。LOD4 之后只会被关闭/删除，不会导入 Runtime LOD。 ");
        }

        ValidateLODRendererTransforms(lods, lod0Renderer, source);

        instance = new SourceInstance
        {
            transform = lod0Renderer.transform,
            sourceObject = lodGroup.gameObject,
            sourcePrefab = sourcePrefab,
            sourcePrefabGuid = sourcePrefabGuid
        };

        HashSet<Renderer> uniqueRenderers = new HashSet<Renderer>();

        for (int lodIndex = 0; lodIndex < lods.Length; lodIndex++)
        {
            Renderer[] lodRenderers = lods[lodIndex].renderers;
            if (lodRenderers == null) continue;

            for (int rendererIndex = 0; rendererIndex < lodRenderers.Length; rendererIndex++)
            {
                Renderer renderer = lodRenderers[rendererIndex];
                if (renderer != null && uniqueRenderers.Add(renderer))
                {
                    instance.renderersToDisable.Add(renderer);
                }
            }
        }

        return true;
    }

    private bool TryGetOptionalLOD(LOD lod, out SourceLOD sourceLOD, out string error)
    {
        sourceLOD = new SourceLOD();
        error = null;

        Renderer[] renderers = lod.renderers;
        if (renderers == null || renderers.Length == 0) return true;

        if (!TryGetSingleMeshRenderer(lod, out MeshRenderer renderer))
        {
            error = "某个 LOD 包含多个 Renderer 或不是 MeshRenderer。当前每个 LOD 只支持一个 MeshRenderer。";
            return false;
        }

        bool success = TryBuildSourceLOD(renderer, out sourceLOD, out error);
        if (!success && IsMaterialLayoutError(error)) invalidMaterialLayoutCount++;
        return success;
    }

    private static bool TryGetSingleMeshRenderer(LOD lod, out MeshRenderer meshRenderer)
    {
        meshRenderer = null;

        Renderer[] renderers = lod.renderers;
        if (renderers == null || renderers.Length != 1) return false;

        meshRenderer = renderers[0] as MeshRenderer;
        return meshRenderer != null;
    }

    private static bool TryBuildSourceLOD(
        MeshRenderer meshRenderer,
        out SourceLOD sourceLOD,
        out string error
    )
    {
        sourceLOD = null;
        error = null;

        MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            error = "没有有效 MeshFilter/sharedMesh。";
            return false;
        }

        Material[] materials = meshRenderer.sharedMaterials;
        if (meshRenderer.HasPropertyBlock())
        {
            error = "Renderer 使用 MaterialPropertyBlock 覆盖。当前 Species 无法保存这些渲染参数，请先将覆盖写入独立 Material 资产。";
            return false;
        }
        if (!AreMaterialsUsable(materials))
        {
            error = "存在空 Material，或 Material 列表为空。";
            return false;
        }

        if (meshFilter.sharedMesh.subMeshCount != materials.Length)
        {
            error = $"SubMesh/Material mismatch：Mesh 有 {meshFilter.sharedMesh.subMeshCount} 个 SubMesh，但 Renderer 有 {materials.Length} 个 Material。";
            return false;
        }

        sourceLOD = new SourceLOD
        {
            mesh = meshFilter.sharedMesh,
            materials = (Material[])materials.Clone()
        };

        return true;
    }

    private static bool IsMaterialLayoutError(string error)
    {
        return !string.IsNullOrEmpty(error) && error.StartsWith("SubMesh/Material mismatch", StringComparison.Ordinal);
    }

    private static void ValidateLODRendererTransforms(LOD[] lods, MeshRenderer lod0Renderer, SourceGroup source)
    {
        int count = Mathf.Min(4, lods.Length);

        for (int i = 1; i < count; i++)
        {
            Renderer[] renderers = lods[i].renderers;
            if (renderers == null || renderers.Length == 0) continue;
            if (renderers.Length != 1) continue;

            MeshRenderer renderer = renderers[0] as MeshRenderer;
            if (renderer == null) continue;

            if (!WorldTransformsApproximatelyEqual(lod0Renderer.transform, renderer.transform))
            {
                source.errors.Add(
                    $"LOD{i} Renderer Transform 与 LOD0 不一致。当前 GPU Runtime 对所有 LOD 共用同一个 Instance Matrix，直接 Bake 会导致切 LOD 时位置/旋转/缩放跳变。请先统一各 LOD Renderer Transform。"
                );
            }
        }
    }

    private static bool WorldTransformsApproximatelyEqual(Transform a, Transform b)
    {
        if (a == null || b == null) return false;

        if ((a.position - b.position).sqrMagnitude > 0.000001f) return false;
        if (Quaternion.Angle(a.rotation, b.rotation) > 0.01f) return false;
        if (!ScaleApproximatelyEqual(a.lossyScale, b.lossyScale, 0.0001f)) return false;

        return true;
    }

    private void AddToGroupedSources(
        Dictionary<string, SourceGroup> lookup,
        SourceGroup source,
        SourceInstance instance
    )
    {
        string key = BuildGroupKey(source);

        if (!lookup.TryGetValue(key, out SourceGroup group))
        {
            group = source;
            group.species = FindMatchingSpecies(group, out SpeciesMatchMethod matchMethod, out string matchWarning);
            group.matchMethod = matchMethod;
            group.count = 0;

            if (!string.IsNullOrEmpty(matchWarning))
            {
                group.warnings.Add(matchWarning);
            }

            lookup.Add(key, group);
            sourceGroups.Add(group);
        }

        if (!ReferenceEquals(group, source))
        {
            for (int i = 0; i < source.warnings.Count; i++)
            {
                if (!group.warnings.Contains(source.warnings[i])) group.warnings.Add(source.warnings[i]);
            }

            for (int i = 0; i < source.errors.Count; i++)
            {
                if (!group.errors.Contains(source.errors[i])) group.errors.Add(source.errors[i]);
            }
        }

        group.instances.Add(instance);
        group.count++;
    }

    private void CollectSpeciesCandidates()
    {
        speciesCandidates.Clear();
        HashSet<VegetationSpecies> unique = new HashSet<VegetationSpecies>();

        if (database != null && database.species != null)
        {
            for (int i = 0; i < database.species.Count; i++)
            {
                VegetationSpecies species = database.species[i];
                if (species != null && unique.Add(species)) speciesCandidates.Add(species);
            }
        }

        string[] guids = AssetDatabase.FindAssets("t:VegetationSpecies");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            VegetationSpecies species = AssetDatabase.LoadAssetAtPath<VegetationSpecies>(path);
            if (species != null && unique.Add(species)) speciesCandidates.Add(species);
        }
    }

    private VegetationSpecies FindMatchingSpecies(
        SourceGroup group,
        out SpeciesMatchMethod matchMethod,
        out string warning
    )
    {
        matchMethod = SpeciesMatchMethod.None;
        warning = null;

        if (!string.IsNullOrEmpty(group.sourcePrefabGuid))
        {
            VegetationSpecies guidMatch = null;
            int guidMatchCount = 0;

            for (int i = 0; i < speciesCandidates.Count; i++)
            {
                VegetationSpecies species = speciesCandidates[i];
                if (species == null || !SpeciesRenderingMatchesSource(species, group)) continue;

                if (!string.Equals(GetSpeciesSourcePrefabGuid(species), group.sourcePrefabGuid, StringComparison.Ordinal))
                {
                    continue;
                }

                guidMatch = species;
                guidMatchCount++;
            }

            if (guidMatchCount == 1)
            {
                matchMethod = SpeciesMatchMethod.PrefabGuid;
                return guidMatch;
            }

            if (guidMatchCount > 1)
            {
                warning = $"检测到 {guidMatchCount} 个 Species 使用同一个 Prefab GUID 标记，无法安全自动选择。请手动指定 Species。";
                return null;
            }
        }

        VegetationSpecies renderingMatch = null;
        int renderingMatchCount = 0;

        for (int i = 0; i < speciesCandidates.Count; i++)
        {
            VegetationSpecies species = speciesCandidates[i];
            if (species == null || !SpeciesRenderingMatchesSource(species, group)) continue;

            renderingMatch = species;
            renderingMatchCount++;
        }

        if (renderingMatchCount == 1)
        {
            matchMethod = SpeciesMatchMethod.RenderingSignature;
            return renderingMatch;
        }

        if (renderingMatchCount > 1)
        {
            warning = $"未找到唯一的 Prefab GUID + 渲染资源匹配，并且有 {renderingMatchCount} 个 Species 使用相同 LOD Rendering Signature。为避免误配，请手动指定。";
        }

        return null;
    }

    private static string GetSpeciesSourcePrefabGuid(VegetationSpecies species)
    {
        if (species == null) return null;

        string assetPath = AssetDatabase.GetAssetPath(species);
        if (string.IsNullOrEmpty(assetPath)) return null;

        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null || string.IsNullOrEmpty(importer.userData)) return null;

        string prefix = SpeciesMarkerPrefix + "|source=";
        if (!importer.userData.StartsWith(prefix, StringComparison.Ordinal)) return null;

        return importer.userData.Substring(prefix.Length);
    }

    private static void ResolvePrefabIdentity(GameObject sourceObject, out GameObject prefabAsset, out string prefabGuid)
    {
        prefabAsset = null;
        prefabGuid = null;

        if (sourceObject == null) return;

        string assetPath = null;

        if (PrefabUtility.IsPartOfPrefabInstance(sourceObject))
        {
            assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(sourceObject);
        }
        else if (PrefabUtility.IsPartOfPrefabAsset(sourceObject))
        {
            assetPath = AssetDatabase.GetAssetPath(sourceObject);
        }

        if (string.IsNullOrEmpty(assetPath)) return;

        prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        prefabGuid = AssetDatabase.AssetPathToGUID(assetPath);
    }

    private void CalculatePreview()
    {
        previewNewCount = 0;
        previewDuplicateCount = 0;
        previewMissingSpeciesCount = 0;
        previewInvalidCount = 0;

        if (database == null)
        {
            previewDirty = false;
            return;
        }

        for (int groupIndex = 0; groupIndex < sourceGroups.Count; groupIndex++)
        {
            SourceGroup group = sourceGroups[groupIndex];

            if (group.species == null)
            {
                previewMissingSpeciesCount += group.count;
                continue;
            }

            if (!IsGroupReady(group))
            {
                previewInvalidCount += group.count;
                continue;
            }

            int speciesIndex = database.GetSpeciesIndex(group.species);

            for (int i = 0; i < group.instances.Count; i++)
            {
                SourceInstance sourceInstance = group.instances[i];
                if (!TryGetSourceTRS(sourceInstance, out Vector3 position, out Quaternion rotation, out Vector3 scale))
                {
                    previewInvalidCount++;
                    continue;
                }

                bool duplicate =
                    skipExistingInstances &&
                    speciesIndex >= 0 &&
                    HasExistingInstance(
                        speciesIndex,
                        position,
                        rotation,
                        scale,
                        duplicatePositionTolerance,
                        duplicateRotationTolerance,
                        duplicateScaleTolerance
                    );

                if (duplicate) previewDuplicateCount++;
                else previewNewCount++;
            }
        }

        previewDirty = false;
    }

    private void ResetPreview()
    {
        previewNewCount = 0;
        previewDuplicateCount = 0;
        previewMissingSpeciesCount = 0;
        previewInvalidCount = 0;
        previewDirty = true;
    }

    private void Bake()
    {
        if (database == null || sourceRoot == null) return;

        CalculatePreview();

        if (previewNewCount == 0 && previewDuplicateCount == 0)
        {
            EditorUtility.DisplayDialog(
                "Vegetation Baker",
                "没有可 Bake 的 Ready Instance。请先解决 Missing Species / Invalid Mapping。",
                "OK"
            );
            return;
        }

        if (sourceHandling == BakeSourceHandling.Delete)
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Source Objects?",
                "Bake 成功后将删除对应源 GameObject。虽然可以 Undo，但建议只在确认转换结果后使用。是否继续？",
                "Delete After Bake",
                "Cancel"
            );

            if (!confirmed) return;
        }

        Undo.RegisterCompleteObjectUndo(database, "Bake Vegetation GameObjects");

        int bakedCount = 0;
        int duplicateCount = 0;
        int skippedCount = 0;

        List<SourceInstance> sourcesToHandle = new List<SourceInstance>();

        database.BeginBatchMutation();

        try
        {
            for (int groupIndex = 0; groupIndex < sourceGroups.Count; groupIndex++)
            {
                SourceGroup group = sourceGroups[groupIndex];

                if (!IsGroupReady(group))
                {
                    skippedCount += group.count;
                    continue;
                }

                int speciesIndex = database.GetOrAddSpecies(group.species);

                for (int i = 0; i < group.instances.Count; i++)
                {
                    SourceInstance sourceInstance = group.instances[i];

                    if (!TryGetSourceTRS(sourceInstance, out Vector3 position, out Quaternion rotation, out Vector3 scale))
                    {
                        skippedCount++;
                        continue;
                    }

                    bool duplicate =
                        skipExistingInstances &&
                        HasExistingInstance(
                            speciesIndex,
                            position,
                            rotation,
                            scale,
                            duplicatePositionTolerance,
                            duplicateRotationTolerance,
                            duplicateScaleTolerance
                        );

                    if (duplicate)
                    {
                        duplicateCount++;
                        sourcesToHandle.Add(sourceInstance);
                        continue;
                    }

                    database.AddInstance(group.species, position, rotation, scale, VegetationInstanceSource.Imported, 0, 0);
                    bakedCount++;
                    sourcesToHandle.Add(sourceInstance);
                }
            }
        }
        finally
        {
            database.EndBatchMutation();
        }

        ApplySourceHandling(sourcesToHandle);

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        if (sourceRoot != null && sourceRoot.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(sourceRoot.scene);
        }

        if (vegetationRenderer != null)
        {
            vegetationRenderer.Rebuild();
        }

        SceneView.RepaintAll();

        Debug.Log(
            $"Vegetation Baker 完成：新增 {bakedCount}，跳过重复 {duplicateCount}，未处理 {skippedCount}。Source Handling = {sourceHandling}."
        );

        if (sourceRoot != null)
        {
            Scan();
        }
        else
        {
            sourceGroups.Clear();
            ResetPreview();
            Repaint();
        }
    }

    private void ApplySourceHandling(List<SourceInstance> sourceInstances)
    {
        if (sourceInstances == null || sourceInstances.Count == 0) return;

        HashSet<GameObject> rawObjects = new HashSet<GameObject>();
        for (int i = 0; i < sourceInstances.Count; i++)
        {
            GameObject sourceObject = sourceInstances[i]?.sourceObject;
            if (sourceObject != null) rawObjects.Add(GetArchivableSourceObject(sourceObject));
        }

        rawObjects.Remove(null);
        List<GameObject> topLevelObjects = GetTopLevelSourceObjects(rawObjects);

        if (sourceHandling == BakeSourceHandling.DisableRenderers)
        {
            HashSet<Renderer> handledRenderers = new HashSet<Renderer>();

            for (int i = 0; i < sourceInstances.Count; i++)
            {
                SourceInstance sourceInstance = sourceInstances[i];
                if (sourceInstance == null) continue;

                for (int rendererIndex = 0; rendererIndex < sourceInstance.renderersToDisable.Count; rendererIndex++)
                {
                    Renderer renderer = sourceInstance.renderersToDisable[rendererIndex];
                    if (renderer == null || !handledRenderers.Add(renderer) || !renderer.enabled) continue;

                    Undo.RecordObject(renderer, "Disable Baked Vegetation Renderer");
                    renderer.enabled = false;
                    EditorUtility.SetDirty(renderer);
                }
            }
        }
        else if (sourceHandling == BakeSourceHandling.DisableGameObject)
        {
            for (int i = 0; i < topLevelObjects.Count; i++)
            {
                GameObject sourceObject = topLevelObjects[i];
                if (sourceObject == null || !sourceObject.activeSelf) continue;

                Undo.RecordObject(sourceObject, "Disable Baked Vegetation GameObject");
                sourceObject.SetActive(false);
                EditorUtility.SetDirty(sourceObject);
            }
        }
        else if (sourceHandling == BakeSourceHandling.Delete)
        {
            for (int i = topLevelObjects.Count - 1; i >= 0; i--)
            {
                GameObject sourceObject = topLevelObjects[i];
                if (sourceObject == null) continue;

                Undo.DestroyObjectImmediate(sourceObject);
            }

            return;
        }

        if (organizeBakedSources)
        {
            ArchiveBakedSourceObjects(topLevelObjects);
        }
    }

    private GameObject GetArchivableSourceObject(GameObject sourceObject)
    {
        if (sourceObject == null) return null;

        GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(sourceObject);
        if (prefabRoot == null) return sourceObject;

        // 对 Prefab Instance，移动 Instance Root 而不是内部 Renderer 子节点，避免产生非法的 Prefab 层级结构修改。
        if (sourceRoot != null && prefabRoot == sourceRoot)
        {
            return sourceObject;
        }

        return prefabRoot;
    }

    private void ArchiveBakedSourceObjects(List<GameObject> sourceObjects)
    {
        if (sourceRoot == null || sourceObjects == null || sourceObjects.Count == 0) return;

        GameObject archive = GetOrCreateBakedSourcesContainer();
        if (archive == null) return;

        for (int i = 0; i < sourceObjects.Count; i++)
        {
            GameObject sourceObject = sourceObjects[i];
            if (sourceObject == null || sourceObject == archive) continue;

            if (sourceObject.transform.IsChildOf(archive.transform)) continue;

            // 如果 Source Root 自身就是一个 Prefab Instance Root，可以安全整体归档。
            // 如果要移动的是 Prefab 内部不可重排的子节点，则跳过归档，但仍保留前面的 Disable 操作。
            if (PrefabUtility.IsPartOfPrefabInstance(sourceObject))
            {
                GameObject nearestRoot = PrefabUtility.GetNearestPrefabInstanceRoot(sourceObject);
                if (nearestRoot != sourceObject)
                {
                    Debug.LogWarning(
                        $"Vegetation Baker：'{sourceObject.name}' 是 Prefab Instance 内部子节点，无法安全移动到 {BakedSourcesContainerName}。已保留 Source Handling，但跳过层级归档。",
                        sourceObject
                    );
                    continue;
                }
            }

            Undo.SetTransformParent(sourceObject.transform, archive.transform, "Archive Baked Vegetation Source");
            EditorUtility.SetDirty(sourceObject);
        }
    }

    private GameObject GetOrCreateBakedSourcesContainer()
    {
        GameObject existing = FindBakedSourcesContainer();
        if (existing != null) return existing;
        if (sourceRoot == null || !sourceRoot.scene.IsValid()) return null;

        Transform desiredParent = sourceRoot.transform.parent;

        GameObject archive = new GameObject(BakedSourcesContainerName);
        Undo.RegisterCreatedObjectUndo(archive, "Create Baked Vegetation Source Archive");
        SceneManager.MoveGameObjectToScene(archive, sourceRoot.scene);

        if (desiredParent != null)
        {
            archive.transform.SetParent(desiredParent, false);
            archive.transform.SetSiblingIndex(sourceRoot.transform.GetSiblingIndex() + 1);
        }

        EditorUtility.SetDirty(archive);
        EditorSceneManager.MarkSceneDirty(sourceRoot.scene);
        return archive;
    }

    private GameObject FindBakedSourcesContainer()
    {
        if (sourceRoot == null || !sourceRoot.scene.IsValid()) return null;

        // Source Root 本身可能已经被整体归档。
        Transform ancestor = sourceRoot.transform.parent;
        while (ancestor != null)
        {
            if (ancestor.name == BakedSourcesContainerName) return ancestor.gameObject;
            ancestor = ancestor.parent;
        }

        Transform parent = sourceRoot.transform.parent;
        if (parent != null)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child != null && child.name == BakedSourcesContainerName) return child.gameObject;
            }

            return null;
        }

        GameObject[] roots = sourceRoot.scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].name == BakedSourcesContainerName) return roots[i];
        }

        return null;
    }

    private static List<GameObject> GetTopLevelSourceObjects(HashSet<GameObject> rawObjects)
    {
        List<GameObject> result = new List<GameObject>();

        foreach (GameObject candidate in rawObjects)
        {
            if (candidate == null) continue;

            bool hasSelectedAncestor = false;
            Transform parent = candidate.transform.parent;

            while (parent != null)
            {
                if (rawObjects.Contains(parent.gameObject))
                {
                    hasSelectedAncestor = true;
                    break;
                }

                parent = parent.parent;
            }

            if (!hasSelectedAncestor) result.Add(candidate);
        }

        return result;
    }

    private static bool TryGetSourceTRS(
        SourceInstance sourceInstance,
        out Vector3 position,
        out Quaternion rotation,
        out Vector3 scale
    )
    {
        position = default;
        rotation = Quaternion.identity;
        scale = Vector3.one;

        if (sourceInstance == null || sourceInstance.transform == null) return false;

        Transform sourceTransform = sourceInstance.transform;
        position = sourceTransform.position;
        rotation = sourceTransform.rotation;
        scale = sourceTransform.lossyScale;
        return true;
    }

    private bool HasExistingInstance(
        int speciesIndex,
        Vector3 position,
        Quaternion rotation,
        Vector3 scale,
        float positionTolerance,
        float rotationTolerance,
        float scaleTolerance
    )
    {
        positionTolerance = Mathf.Max(0.0001f, positionTolerance);
        rotationTolerance = Mathf.Max(0f, rotationTolerance);
        scaleTolerance = Mathf.Max(0f, scaleTolerance);

        float positionToleranceSqr = positionTolerance * positionTolerance;

        Vector2Int min = database.WorldToChunkCoordinate(
            position - new Vector3(positionTolerance, 0f, positionTolerance)
        );

        Vector2Int max = database.WorldToChunkCoordinate(
            position + new Vector3(positionTolerance, 0f, positionTolerance)
        );

        for (int x = min.x; x <= max.x; x++)
        {
            for (int z = min.y; z <= max.y; z++)
            {
                VegetationChunkData chunk = database.GetChunk(new Vector2Int(x, z));
                if (chunk == null || chunk.instances == null) continue;

                for (int i = 0; i < chunk.instances.Count; i++)
                {
                    VegetationInstance instance = chunk.instances[i];
                    if (instance.speciesIndex != speciesIndex) continue;
                    if ((instance.position - position).sqrMagnitude > positionToleranceSqr) continue;
                    if (Quaternion.Angle(instance.rotation, rotation) > rotationTolerance) continue;
                    if (!ScaleApproximatelyEqual(instance.scale, scale, scaleTolerance)) continue;

                    return true;
                }
            }
        }

        return false;
    }

    private static bool ScaleApproximatelyEqual(Vector3 a, Vector3 b, float tolerance)
    {
        return
            Mathf.Abs(a.x - b.x) <= tolerance &&
            Mathf.Abs(a.y - b.y) <= tolerance &&
            Mathf.Abs(a.z - b.z) <= tolerance;
    }

    private static bool SpeciesRenderingMatchesSource(VegetationSpecies species, SourceGroup group)
    {
        if (species == null || group == null) return false;

        return
            LODRenderingMatches(species.lod0, group.lod0) &&
            LODRenderingMatches(species.lod1, group.lod1) &&
            LODRenderingMatches(species.lod2, group.lod2) &&
            LODRenderingMatches(species.lod3, group.lod3);
    }

    private static bool LODRenderingMatches(VegetationLODAsset speciesLOD, SourceLOD sourceLOD)
    {
        return VegetationSpeciesRenderingSignature.LODMatches(
            speciesLOD, sourceLOD?.mesh, sourceLOD?.materials);
    }

    private static bool AreMaterialsUsable(Material[] materials)
    {
        if (materials == null || materials.Length == 0) return false;

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null) return false;
        }

        return true;
    }

    private static string BuildGroupKey(SourceGroup group)
    {
        string prefabPart = string.IsNullOrEmpty(group.sourcePrefabGuid)
            ? "NoPrefab"
            : group.sourcePrefabGuid;

        return
            prefabPart + "|" +
            BuildLODKey(group.lod0) + "|" +
            BuildLODKey(group.lod1) + "|" +
            BuildLODKey(group.lod2) + "|" +
            BuildLODKey(group.lod3);
    }

    private static string BuildLODKey(SourceLOD lod)
    {
        return VegetationSpeciesRenderingSignature.BuildLODKey(lod?.mesh, lod?.materials);
    }

    private bool IsGroupReady(SourceGroup group)
    {
        return
            group != null &&
            group.errors.Count == 0 &&
            group.species != null &&
            SpeciesRenderingMatchesSource(group.species, group);
    }

    private int CountReadyInstances()
    {
        int count = 0;

        for (int i = 0; i < sourceGroups.Count; i++)
        {
            if (IsGroupReady(sourceGroups[i])) count += sourceGroups[i].count;
        }

        return count;
    }
}
