using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class VegetationSpeciesFactoryWindow : EditorWindow
{
    [Serializable]
    private class SourceLOD
    {
        public Mesh mesh;
        public Material[] materials = Array.Empty<Material>();
        public bool IsValid => mesh != null && materials != null && materials.Length > 0;
    }

    private class PrefabScanResult
    {
        public GameObject prefab;
        public string assetPath;
        public string assetGuid;
        public SourceLOD[] lods = { new SourceLOD(), new SourceLOD(), new SourceLOD(), new SourceLOD() };
        public readonly List<string> errors = new List<string>();
        public readonly List<string> warnings = new List<string>();
        public VegetationSpecies existingSpecies;
        public int uniqueMaterialCount;
        public bool expanded;
        public bool IsValid => prefab != null && lods[0].IsValid && errors.Count == 0;
        public int LODCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < lods.Length; i++) if (lods[i] != null && lods[i].IsValid) count++;
                return count;
            }
        }
    }

    private const string SpeciesMarkerPrefix = "VegetationSpeciesFactory:v1";

    private VegetationShaderAdapterProfile shaderAdapterProfile;
    private VegetationDatabase database;
    private VegetationType newSpeciesType = VegetationType.Tree;
    private string outputRoot = "Assets/VegetationGenerated";
    private GameObject prefabToAdd;
    private DefaultAsset folderToAdd;
    private Vector2 scrollPosition;

    private readonly List<GameObject> sourcePrefabs = new List<GameObject>();
    private readonly List<PrefabScanResult> scanResults = new List<PrefabScanResult>();
    private bool scanDirty = true;

    [MenuItem("Tools/Vegetation/Species Factory")]
    public static void Open()
    {
        GetWindow<VegetationSpeciesFactoryWindow>("Species Factory");
    }

    public static void OpenWithPrefab(GameObject prefab, VegetationDatabase targetDatabase = null)
    {
        VegetationSpeciesFactoryWindow window = GetWindow<VegetationSpeciesFactoryWindow>("Species Factory");

        if (targetDatabase != null)
        {
            window.database = targetDatabase;
        }

        if (prefab != null)
        {
            window.AddPrefab(prefab);
            window.scanDirty = true;
        }

        window.Show();
        window.Focus();
        window.Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Vegetation Species Factory", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUI.BeginChangeCheck();
        shaderAdapterProfile = (VegetationShaderAdapterProfile)EditorGUILayout.ObjectField("Shader Adapter", shaderAdapterProfile, typeof(VegetationShaderAdapterProfile), false);
        database = (VegetationDatabase)EditorGUILayout.ObjectField("Database", database, typeof(VegetationDatabase), false);
        newSpeciesType = (VegetationType)EditorGUILayout.EnumPopup("New Species Type", newSpeciesType);
        outputRoot = EditorGUILayout.TextField("Output Root", outputRoot);
        if (EditorGUI.EndChangeCheck()) scanDirty = true;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Sources", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        prefabToAdd = (GameObject)EditorGUILayout.ObjectField("Prefab", prefabToAdd, typeof(GameObject), false);
        using (new EditorGUI.DisabledScope(prefabToAdd == null))
        {
            if (GUILayout.Button("Add", GUILayout.Width(70f)))
            {
                AddPrefab(prefabToAdd);
                prefabToAdd = null;
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        folderToAdd = (DefaultAsset)EditorGUILayout.ObjectField("Folder", folderToAdd, typeof(DefaultAsset), false);
        using (new EditorGUI.DisabledScope(folderToAdd == null))
        {
            if (GUILayout.Button("Add All", GUILayout.Width(70f)))
            {
                AddFolder(folderToAdd);
                folderToAdd = null;
            }
        }
        EditorGUILayout.EndHorizontal();

        DrawDropArea();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Add Project Selection")) AddProjectSelection();
        using (new EditorGUI.DisabledScope(sourcePrefabs.Count == 0))
        {
            if (GUILayout.Button("Clear"))
            {
                sourcePrefabs.Clear();
                scanResults.Clear();
                scanDirty = true;
            }
        }
        EditorGUILayout.EndHorizontal();

        DrawSourcePrefabList();

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(sourcePrefabs.Count == 0))
        {
            if (GUILayout.Button("Scan Sources", GUILayout.Height(28f))) ScanSources();
        }
        using (new EditorGUI.DisabledScope(sourcePrefabs.Count == 0 || shaderAdapterProfile == null))
        {
            if (GUILayout.Button("Produce Valid Species", GUILayout.Height(28f))) ProduceAllValid();
        }
        EditorGUILayout.EndHorizontal();

        if (shaderAdapterProfile == null) EditorGUILayout.HelpBox("需要指定 Shader Adapter Profile 才能检查材质映射和生产 Species。", MessageType.Warning);
        if (database == null) EditorGUILayout.HelpBox("未指定 Database。Species 仍可生成，但不会自动加入 VegetationDatabase。", MessageType.Info);
        if (scanDirty && sourcePrefabs.Count > 0) EditorGUILayout.HelpBox("Sources 或设置已变化，请重新 Scan。Produce 时也会自动重新扫描。", MessageType.Info);

        DrawScanResults();
    }

    private void DrawDropArea()
    {
        Rect rect = GUILayoutUtility.GetRect(0f, 54f, GUILayout.ExpandWidth(true));
        GUI.Box(rect, "把 Prefab 或文件夹拖到这里", EditorStyles.helpBox);

        Event current = Event.current;
        if (!rect.Contains(current.mousePosition)) return;
        if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform) return;

        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (current.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            UnityEngine.Object[] references = DragAndDrop.objectReferences;
            for (int i = 0; i < references.Length; i++) AddObjectReference(references[i]);
        }

        current.Use();
    }

    private void DrawSourcePrefabList()
    {
        if (sourcePrefabs.Count == 0) return;

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField($"Queued Prefabs ({sourcePrefabs.Count})", EditorStyles.miniBoldLabel);

        for (int i = sourcePrefabs.Count - 1; i >= 0; i--)
        {
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.ObjectField(sourcePrefabs[i], typeof(GameObject), false);
            if (GUILayout.Button("X", GUILayout.Width(24f)))
            {
                sourcePrefabs.RemoveAt(i);
                scanDirty = true;
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawScanResults()
    {
        if (scanResults.Count == 0) return;

        int validCount = 0;
        int invalidCount = 0;
        int uniqueMaterialCount = 0;
        HashSet<Material> allMaterials = new HashSet<Material>();

        for (int i = 0; i < scanResults.Count; i++)
        {
            PrefabScanResult result = scanResults[i];
            if (result.IsValid) validCount++; else invalidCount++;
            for (int lodIndex = 0; lodIndex < result.lods.Length; lodIndex++)
            {
                SourceLOD lod = result.lods[lodIndex];
                if (lod == null || lod.materials == null) continue;
                for (int materialIndex = 0; materialIndex < lod.materials.Length; materialIndex++) if (lod.materials[materialIndex] != null) allMaterials.Add(lod.materials[materialIndex]);
            }
        }

        uniqueMaterialCount = allMaterials.Count;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox($"扫描完成：{scanResults.Count} Prefabs / {validCount} Valid / {invalidCount} Invalid / {uniqueMaterialCount} Unique Materials", invalidCount > 0 ? MessageType.Warning : MessageType.Info);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < scanResults.Count; i++)
        {
            PrefabScanResult result = scanResults[i];
            EditorGUILayout.BeginVertical("box");

            string status = result.IsValid ? "✓" : "✕";
            result.expanded = EditorGUILayout.Foldout(result.expanded, $"{status} {result.prefab.name}    LODs: {result.LODCount}    Materials: {result.uniqueMaterialCount}", true);

            if (result.expanded)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Prefab", result.prefab, typeof(GameObject), false);
                    EditorGUILayout.ObjectField("Existing Species", result.existingSpecies, typeof(VegetationSpecies), false);
                }

                for (int lodIndex = 0; lodIndex < result.lods.Length; lodIndex++)
                {
                    SourceLOD lod = result.lods[lodIndex];
                    if (lod == null || !lod.IsValid) continue;
                    DrawLODInfo($"LOD{lodIndex}", lod);
                }

                for (int errorIndex = 0; errorIndex < result.errors.Count; errorIndex++) EditorGUILayout.HelpBox(result.errors[errorIndex], MessageType.Error);
                for (int warningIndex = 0; warningIndex < result.warnings.Count; warningIndex++) EditorGUILayout.HelpBox(result.warnings[warningIndex], MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawLODInfo(string label, SourceLOD lod)
    {
        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Mesh", lod.mesh, typeof(Mesh), false);
            for (int i = 0; i < lod.materials.Length; i++) EditorGUILayout.ObjectField($"Material {i}", lod.materials[i], typeof(Material), false);
        }
    }

    private void AddObjectReference(UnityEngine.Object reference)
    {
        if (reference == null) return;
        if (reference is GameObject gameObject)
        {
            AddPrefab(gameObject);
            return;
        }

        if (reference is DefaultAsset folder) AddFolder(folder);
    }

    private void AddProjectSelection()
    {
        UnityEngine.Object[] selection = Selection.objects;
        for (int i = 0; i < selection.Length; i++) AddObjectReference(selection[i]);
    }

    private void AddPrefab(GameObject prefab)
    {
        if (prefab == null) return;

        string path = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning($"Species Factory：'{prefab.name}' 不是 Project 中的资产。", prefab);
            return;
        }

        if (!PrefabUtility.IsPartOfPrefabAsset(prefab))
        {
            Debug.LogWarning($"Species Factory：'{prefab.name}' 不是 Prefab Asset。", prefab);
            return;
        }

        if (sourcePrefabs.Contains(prefab)) return;

        sourcePrefabs.Add(prefab);
        scanDirty = true;
    }

    private void AddFolder(DefaultAsset folder)
    {
        if (folder == null) return;

        string folderPath = AssetDatabase.GetAssetPath(folder);
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogWarning($"Species Factory：'{folder.name}' 不是有效文件夹。", folder);
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        Array.Sort(guids, delegate(string a, string b)
        {
            return string.CompareOrdinal(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b));
        });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            AddPrefab(prefab);
        }
    }

    private void ScanSources()
    {
        scanResults.Clear();

        string speciesFolder = GetSpeciesFolder();
        for (int i = 0; i < sourcePrefabs.Count; i++)
        {
            GameObject prefab = sourcePrefabs[i];
            if (prefab == null) continue;
            scanResults.Add(ScanPrefab(prefab, speciesFolder));
        }

        scanDirty = false;
        Repaint();
    }

    private PrefabScanResult ScanPrefab(GameObject prefab, string speciesFolder)
    {
        PrefabScanResult result = new PrefabScanResult
        {
            prefab = prefab,
            assetPath = AssetDatabase.GetAssetPath(prefab)
        };

        result.assetGuid = AssetDatabase.AssetPathToGUID(result.assetPath);
        if (string.IsNullOrEmpty(result.assetGuid)) result.errors.Add("无法获取 Prefab GUID。");

        LODGroup[] lodGroups = prefab.GetComponentsInChildren<LODGroup>(true);

        if (lodGroups.Length > 1)
        {
            result.errors.Add($"检测到 {lodGroups.Length} 个 LODGroup。当前一个 Species 只支持一个 LODGroup。请拆分 Prefab 或先合并资产结构。");
        }
        else if (lodGroups.Length == 1)
        {
            ScanLODGroup(lodGroups[0], result);
        }
        else
        {
            ScanStandalonePrefab(prefab, result);
        }

        ValidateLODContinuity(result);
        ValidateMaterialMappings(result);
        result.existingSpecies = FindExistingSpecies(speciesFolder, BuildSpeciesMarker(result.assetGuid), result.lods, shaderAdapterProfile);

        HashSet<Material> uniqueMaterials = new HashSet<Material>();
        for (int lodIndex = 0; lodIndex < result.lods.Length; lodIndex++)
        {
            Material[] materials = result.lods[lodIndex].materials;
            if (materials == null) continue;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++) if (materials[materialIndex] != null) uniqueMaterials.Add(materials[materialIndex]);
        }
        result.uniqueMaterialCount = uniqueMaterials.Count;

        return result;
    }

    private void ScanLODGroup(LODGroup lodGroup, PrefabScanResult result)
    {
        LOD[] unityLODs = lodGroup.GetLODs();
        if (unityLODs == null || unityLODs.Length == 0)
        {
            result.errors.Add("LODGroup 没有任何 LOD。");
            return;
        }

        if (unityLODs.Length > 4) result.warnings.Add($"LODGroup 有 {unityLODs.Length} 层，但当前 VegetationSpecies 只支持 LOD0~LOD3，LOD4 之后会忽略。");

        int count = Mathf.Min(4, unityLODs.Length);
        for (int i = 0; i < count; i++)
        {
            Renderer[] renderers = unityLODs[i].renderers;
            if (renderers == null || renderers.Length == 0)
            {
                if (i == 0) result.errors.Add("LOD0 没有 Renderer。");
                continue;
            }

            if (!TryBuildLOD(renderers, out SourceLOD sourceLOD, out string error))
            {
                result.errors.Add($"LOD{i}: {error}");
                continue;
            }

            result.lods[i] = sourceLOD;
        }
    }

    private void ScanStandalonePrefab(GameObject prefab, PrefabScanResult result)
    {
        MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
        if (renderers.Length == 0)
        {
            result.errors.Add("没有找到 LODGroup，也没有找到 MeshRenderer。");
            return;
        }

        if (renderers.Length > 1)
        {
            result.errors.Add($"没有 LODGroup，但检测到 {renderers.Length} 个 MeshRenderer。当前一个 Species 只能对应一个 Mesh，请先整理为单 MeshRenderer 或 LODGroup。");
            return;
        }

        if (!TryBuildLOD(new Renderer[] { renderers[0] }, out SourceLOD sourceLOD, out string error))
        {
            result.errors.Add($"LOD0: {error}");
            return;
        }

        result.lods[0] = sourceLOD;
    }

    private static bool TryBuildLOD(Renderer[] renderers, out SourceLOD sourceLOD, out string error)
    {
        sourceLOD = new SourceLOD();
        error = null;

        if (renderers == null || renderers.Length == 0)
        {
            error = "没有 Renderer。";
            return false;
        }

        if (renderers.Length != 1)
        {
            error = $"包含 {renderers.Length} 个 Renderer。当前 VegetationLODAsset 只支持一个 Mesh + SubMesh Materials。";
            return false;
        }

        MeshRenderer meshRenderer = renderers[0] as MeshRenderer;
        if (meshRenderer == null)
        {
            error = $"Renderer 类型是 {renderers[0].GetType().Name}，当前只支持 MeshRenderer。";
            return false;
        }

        MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            error = "MeshRenderer 没有有效 MeshFilter/sharedMesh。";
            return false;
        }

        Material[] materials = meshRenderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
        {
            error = "没有材质。";
            return false;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null) continue;
            error = $"Material {i} 为空。";
            return false;
        }

        if (meshFilter.sharedMesh.subMeshCount != materials.Length)
        {
            error = $"Mesh 有 {meshFilter.sharedMesh.subMeshCount} 个 SubMesh，但 Renderer 有 {materials.Length} 个 Material。你的 Runtime 会按 SubMesh 索引取材质，因此这里必须一致。";
            return false;
        }

        sourceLOD.mesh = meshFilter.sharedMesh;
        sourceLOD.materials = (Material[])materials.Clone();
        return true;
    }

    private static void ValidateLODContinuity(PrefabScanResult result)
    {
        bool foundGap = false;
        for (int i = 0; i < result.lods.Length; i++)
        {
            bool valid = result.lods[i] != null && result.lods[i].IsValid;
            if (!valid)
            {
                if (i == 0) continue;
                foundGap = true;
                continue;
            }

            if (foundGap) result.errors.Add($"LOD 层级不连续：LOD{i} 有资源，但前面存在空 LOD。请保证 LOD0 → LOD1 → LOD2 → LOD3 连续。");
        }
    }

    private void ValidateMaterialMappings(PrefabScanResult result)
    {
        if (shaderAdapterProfile == null) return;

        HashSet<Material> checkedMaterials = new HashSet<Material>();
        for (int lodIndex = 0; lodIndex < result.lods.Length; lodIndex++)
        {
            SourceLOD lod = result.lods[lodIndex];
            if (lod == null || lod.materials == null) continue;

            for (int materialIndex = 0; materialIndex < lod.materials.Length; materialIndex++)
            {
                Material material = lod.materials[materialIndex];
                if (material == null || !checkedMaterials.Add(material)) continue;

                if (material.shader == null)
                {
                    result.errors.Add($"Material '{material.name}' 没有 Shader。");
                    continue;
                }

                if (!VegetationMaterialAdapter.CanAdaptMaterial(material, shaderAdapterProfile, out Shader targetShader))
                {
                    result.errors.Add($"没有 Shader Mapping：{material.shader.name}  （Material: {material.name}）");
                    continue;
                }

                if (targetShader == null) result.errors.Add($"Shader Mapping 的目标 Shader 为空：{material.shader.name}");
            }
        }
    }

    private void ProduceAllValid()
    {
        if (shaderAdapterProfile == null)
        {
            EditorUtility.DisplayDialog("Species Factory", "请先指定 Shader Adapter Profile。", "OK");
            return;
        }

        if (sourcePrefabs.Count == 0) return;

        ScanSources();

        if (!EnsureFolder(NormalizeFolder(outputRoot), out string rootError))
        {
            EditorUtility.DisplayDialog("Species Factory", rootError, "OK");
            return;
        }

        string materialFolder = GetMaterialFolder();
        string speciesFolder = GetSpeciesFolder();

        if (!EnsureFolder(materialFolder, out string materialFolderError))
        {
            EditorUtility.DisplayDialog("Species Factory", materialFolderError, "OK");
            return;
        }

        if (!EnsureFolder(speciesFolder, out string speciesFolderError))
        {
            EditorUtility.DisplayDialog("Species Factory", speciesFolderError, "OK");
            return;
        }

        int createdCount = 0;
        int reusedCount = 0;
        int skippedCount = 0;
        List<string> failures = new List<string>();
        VegetationSpecies lastProducedSpecies = null;

        if (database != null) Undo.RecordObject(database, "Produce Vegetation Species");

        try
        {
            for (int i = 0; i < scanResults.Count; i++)
            {
                PrefabScanResult result = scanResults[i];
                if (!result.IsValid)
                {
                    skippedCount++;
                    continue;
                }

                if (!TryProduceSpecies(result, materialFolder, speciesFolder, out VegetationSpecies species, out bool created, out string error))
                {
                    failures.Add($"{result.prefab.name}: {error}");
                    continue;
                }

                if (created) createdCount++; else reusedCount++;
                lastProducedSpecies = species;

                if (database != null) database.GetOrAddSpecies(species);
            }
        }
        finally
        {
            if (database != null) EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        if (lastProducedSpecies != null) Selection.activeObject = lastProducedSpecies;

        string message = $"生产完成\n新建 Species: {createdCount}\n复用 Species: {reusedCount}\n跳过无效 Prefab: {skippedCount}\n失败: {failures.Count}";
        if (failures.Count > 0)
        {
            message += "\n\n" + string.Join("\n", failures);
            Debug.LogError("Species Factory：\n" + string.Join("\n", failures));
        }
        else
        {
            Debug.Log($"Species Factory完成：新建 {createdCount}，复用 {reusedCount}，跳过 {skippedCount}。");
        }

        EditorUtility.DisplayDialog("Species Factory", message, "OK");
        ScanSources();
    }

    private bool TryProduceSpecies(PrefabScanResult result, string materialFolder, string speciesFolder, out VegetationSpecies species, out bool created, out string error)
    {
        species = null;
        created = false;
        error = null;

        string marker = BuildSpeciesMarker(result.assetGuid);
        species = FindExistingSpecies(speciesFolder, marker, result.lods, shaderAdapterProfile);
        if (species != null) return true;

        SourceLOD[] adaptedLODs = { new SourceLOD(), new SourceLOD(), new SourceLOD(), new SourceLOD() };

        for (int i = 0; i < result.lods.Length; i++)
        {
            SourceLOD sourceLOD = result.lods[i];
            if (sourceLOD == null || !sourceLOD.IsValid) continue;

            if (!VegetationMaterialAdapter.TryAdaptMaterials(sourceLOD.materials, shaderAdapterProfile, materialFolder, out Material[] adaptedMaterials, out error, preserveExisting: true))
            {
                error = $"LOD{i} 材质适配失败：{error}";
                return false;
            }

            adaptedLODs[i].mesh = sourceLOD.mesh;
            adaptedLODs[i].materials = adaptedMaterials;
        }

        species = FindExistingSpecies(speciesFolder, marker, adaptedLODs);

        // A matching resource set is reusable without rewriting any existing Species.
        if (species != null) return true;

        if (species == null)
        {
            species = CreateInstance<VegetationSpecies>();
            species.speciesName = result.prefab.name;
            species.vegetationType = newSpeciesType;

            string path = BuildNewSpeciesPath(result.prefab.name, result.assetGuid, speciesFolder);
            AssetDatabase.CreateAsset(species, path);
            WriteSpeciesMarker(path, marker);
            created = true;
        }

        CopyLOD(adaptedLODs[0], species.lod0);
        CopyLOD(adaptedLODs[1], species.lod1);
        CopyLOD(adaptedLODs[2], species.lod2);
        CopyLOD(adaptedLODs[3], species.lod3);

        EditorUtility.SetDirty(species);
        return true;
    }

    private static void CopyLOD(SourceLOD source, VegetationLODAsset destination)
    {
        if (destination == null) return;

        if (source == null || !source.IsValid)
        {
            destination.mesh = null;
            destination.materials = Array.Empty<Material>();
            return;
        }

        destination.mesh = source.mesh;
        destination.materials = (Material[])source.materials.Clone();
    }

    private static VegetationSpecies FindExistingSpecies(string speciesFolder, string marker, SourceLOD[] lods,
        VegetationShaderAdapterProfile profile = null)
    {
        if (string.IsNullOrEmpty(marker) || !AssetDatabase.IsValidFolder(speciesFolder)) return null;

        string[] guids = AssetDatabase.FindAssets("t:VegetationSpecies", new[] { speciesFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer == null || importer.userData != marker) continue;

            VegetationSpecies species = AssetDatabase.LoadAssetAtPath<VegetationSpecies>(path);
            if (species != null &&
                VegetationSpeciesRenderingSignature.LODMatches(species.lod0, lods[0].mesh, lods[0].materials, profile) &&
                VegetationSpeciesRenderingSignature.LODMatches(species.lod1, lods[1].mesh, lods[1].materials, profile) &&
                VegetationSpeciesRenderingSignature.LODMatches(species.lod2, lods[2].mesh, lods[2].materials, profile) &&
                VegetationSpeciesRenderingSignature.LODMatches(species.lod3, lods[3].mesh, lods[3].materials, profile)) return species;
        }

        return null;
    }

    private static string BuildNewSpeciesPath(string prefabName, string prefabGuid, string speciesFolder)
    {
        string cleanName = SanitizeFileName(prefabName);
        string preferredPath = $"{speciesFolder}/VS_{cleanName}.asset";
        if (AssetDatabase.LoadAssetAtPath<VegetationSpecies>(preferredPath) == null && AssetDatabase.LoadMainAssetAtPath(preferredPath) == null) return preferredPath;

        string shortGuid = prefabGuid != null && prefabGuid.Length >= 8 ? prefabGuid.Substring(0, 8) : "Generated";
        string fallbackPath = $"{speciesFolder}/VS_{cleanName}_{shortGuid}.asset";
        return AssetDatabase.GenerateUniqueAssetPath(fallbackPath);
    }

    private static string BuildSpeciesMarker(string prefabGuid)
    {
        if (string.IsNullOrEmpty(prefabGuid)) return null;
        return $"{SpeciesMarkerPrefix}|source={prefabGuid}";
    }

    private static void WriteSpeciesMarker(string assetPath, string marker)
    {
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null) return;

        importer.userData = marker;
        AssetDatabase.WriteImportSettingsIfDirty(assetPath);
    }

    private string GetMaterialFolder()
    {
        return NormalizeFolder(outputRoot) + "/Materials";
    }

    private string GetSpeciesFolder()
    {
        return NormalizeFolder(outputRoot) + "/Species";
    }

    private static string NormalizeFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return "Assets/VegetationGenerated";

        folder = folder.Replace("\\", "/").Trim();
        while (folder.EndsWith("/", StringComparison.Ordinal)) folder = folder.Substring(0, folder.Length - 1);
        return folder;
    }

    private static bool EnsureFolder(string folder, out string error)
    {
        error = null;
        folder = NormalizeFolder(folder);

        if (AssetDatabase.IsValidFolder(folder)) return true;

        string[] parts = folder.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
        {
            error = $"输出目录必须位于 Assets 中：{folder}";
            return false;
        }

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(parts[i])) continue;
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }

        if (AssetDatabase.IsValidFolder(folder)) return true;

        error = $"无法创建输出目录：{folder}";
        return false;
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Vegetation";

        char[] invalidChars = Path.GetInvalidFileNameChars();
        for (int i = 0; i < invalidChars.Length; i++) value = value.Replace(invalidChars[i], '_');
        return value.Replace('/', '_').Replace('\\', '_');
    }
}
