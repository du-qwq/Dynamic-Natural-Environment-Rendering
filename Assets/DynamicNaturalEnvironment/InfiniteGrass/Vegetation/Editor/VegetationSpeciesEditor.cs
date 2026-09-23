using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[CustomEditor(typeof(VegetationSpecies))]
public class VegetationSpeciesEditor : Editor
{
    private readonly CapsuleBoundsHandle capsuleHandle = new CapsuleBoundsHandle();
    private readonly BoxBoundsHandle boxHandle = new BoxBoundsHandle();
    private readonly SphereBoundsHandle sphereHandle = new SphereBoundsHandle();

    private VegetationDatabase previewDatabaseOverride;
    private VegetationDatabase previewDatabase;
    private VegetationInstance previewInstance;
    private bool hasPreviewInstance;
    private bool sceneEditEnabled;
    private string autoEstimateMessage;
    private MessageType autoEstimateMessageType = MessageType.None;

    private VegetationSpecies Species => (VegetationSpecies)target;

    private void OnEnable()
    {
        SceneView.duringSceneGui += DuringSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10f);
        DrawAutoEstimateTools();
        EditorGUILayout.Space(6f);
        DrawSceneColliderEditor();
        if (sceneEditEnabled) SceneView.RepaintAll();
    }

    private void DrawAutoEstimateTools()
    {
        VegetationSpecies species = Species;
        if (species == null) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Collider 自动估算", EditorStyles.boldLabel);

        if (species.vegetationType == VegetationType.Tree)
        {
            EditorGUILayout.HelpBox("优先识别 Trunk / Bark / Wood / Stem 等树干材质对应的 SubMesh；识别不到时使用模型下半部几何推断。结果只是初始值，建议再用下面的 Scene Collider 编辑手柄微调。", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("当前不是 Tree 类型。自动估算仍可使用，但算法是按“竖直树干”设计的，岩石通常更适合 Box Collider。", MessageType.None);
        }

        string autoButtonLabel = species.enableCollision ? "自动估算树干 Capsule" : "启用碰撞并自动估算树干 Capsule";
        if (GUILayout.Button(autoButtonLabel, GUILayout.Height(28f))) ApplyAutoEstimate(false);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Bounds 粗略估算")) ApplyAutoEstimate(true);
        if (GUILayout.Button("切换为 Capsule"))
        {
            Undo.RecordObject(species, "Set Vegetation Capsule Collider");
            species.enableCollision = true;
            species.colliderType = VegetationColliderType.Capsule;
            EditorUtility.SetDirty(species);
            serializedObject.Update();
            SceneView.RepaintAll();
        }
        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(autoEstimateMessage)) EditorGUILayout.HelpBox(autoEstimateMessage, autoEstimateMessageType);
        EditorGUILayout.EndVertical();
    }

    private void ApplyAutoEstimate(bool boundsOnly)
    {
        VegetationSpecies species = Species;
        if (species == null) return;

        VegetationColliderAutoEstimator.CapsuleEstimate estimate;
        string error;
        bool success = boundsOnly
            ? VegetationColliderAutoEstimator.TryEstimateFromBounds(species, out estimate, out error)
            : VegetationColliderAutoEstimator.TryEstimateTreeCapsule(species, out estimate, out error);

        if (!success)
        {
            autoEstimateMessage = "自动估算失败：" + error;
            autoEstimateMessageType = MessageType.Error;
            Repaint();
            return;
        }

        Undo.RecordObject(species, boundsOnly ? "Estimate Vegetation Capsule From Bounds" : "Auto Estimate Vegetation Tree Capsule");
        species.enableCollision = true;
        species.colliderType = VegetationColliderType.Capsule;
        species.colliderCenter = estimate.center;
        species.capsuleRadius = Mathf.Max(0.001f, estimate.radius);
        species.capsuleHeight = Mathf.Max(species.capsuleRadius * 2f, estimate.height);
        EditorUtility.SetDirty(species);
        serializedObject.Update();

        string meshName = estimate.sourceMesh != null ? estimate.sourceMesh.name : "Unknown";
        string sampleText = estimate.sampleCount > 0 ? "，采样 " + estimate.sampleCount + " 个顶点" : string.Empty;
        autoEstimateMessage = "已估算：" + estimate.method + "（Mesh: " + meshName + sampleText + "）\nCenter = " + FormatVector3(estimate.center) + "，Radius = " + estimate.radius.ToString("0.###") + "，Height = " + estimate.height.ToString("0.###") + "。";
        autoEstimateMessageType = MessageType.Info;

        if (sceneEditEnabled) FindNearestPreviewInstance(SceneView.lastActiveSceneView);
        SceneView.RepaintAll();
        Repaint();
    }

    private void DrawSceneColliderEditor()
    {
        VegetationSpecies species = Species;
        if (species == null) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Scene Collider 编辑", EditorStyles.boldLabel);

        if (!species.enableCollision)
        {
            EditorGUILayout.HelpBox("当前 Species 没有启用碰撞。可以先使用上面的“启用碰撞并自动估算树干 Capsule”，或者手动勾选 Enable Collision。", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUI.BeginChangeCheck();
        sceneEditEnabled = EditorGUILayout.ToggleLeft("在 Scene 视图中编辑碰撞体", sceneEditEnabled);
        if (EditorGUI.EndChangeCheck())
        {
            if (sceneEditEnabled) FindNearestPreviewInstance(SceneView.lastActiveSceneView);
            SceneView.RepaintAll();
        }

        EditorGUI.BeginChangeCheck();
        previewDatabaseOverride = (VegetationDatabase)EditorGUILayout.ObjectField(new GUIContent("Preview Database", "可选。为空时自动寻找当前场景 VegetationRenderer 使用且包含该 Species 的 Database。"), previewDatabaseOverride, typeof(VegetationDatabase), false);
        if (EditorGUI.EndChangeCheck() && sceneEditEnabled) FindNearestPreviewInstance(SceneView.lastActiveSceneView);

        if (!sceneEditEnabled)
        {
            EditorGUILayout.HelpBox("开启后会把编辑手柄套到当前 Scene 视图附近的一棵真实 Vegetation 实例上。不会创建或修改场景 GameObject。", MessageType.None);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("查找当前视图最近实例")) FindNearestPreviewInstance(SceneView.lastActiveSceneView);
        using (new EditorGUI.DisabledScope(!hasPreviewInstance))
        {
            if (GUILayout.Button("定位到预览实例")) FocusPreviewInstance(SceneView.lastActiveSceneView);
        }
        EditorGUILayout.EndHorizontal();

        if (hasPreviewInstance && previewDatabase != null)
        {
            EditorGUILayout.LabelField("Database", previewDatabase.name);
            EditorGUILayout.LabelField("Persistent ID", previewInstance.persistentID.ToString());
            EditorGUILayout.LabelField("Instance Scale", FormatVector3(previewInstance.scale));
            EditorGUILayout.HelpBox("Scene 中：中心的移动手柄用于修改 Collider Center；外侧尺寸手柄用于修改 Radius / Height / Size。所有修改都会直接写回这个 VegetationSpecies。", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("没有找到该 Species 的场景实例。确认当前场景 VegetationRenderer 使用的 Database 中包含这个 Species，或者手动指定 Preview Database。", MessageType.Warning);
        }

        EditorGUILayout.EndVertical();
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        VegetationSpecies species = Species;
        if (!sceneEditEnabled || species == null || !species.enableCollision) return;

        if (!ValidatePreviewInstance()) FindNearestPreviewInstance(sceneView);
        if (!hasPreviewInstance) return;

        Matrix4x4 matrix = previewInstance.LocalToWorldMatrix;

        using (new Handles.DrawingScope(matrix))
        {
            DrawCenterHandle(species);
            DrawColliderBoundsHandle(species);
        }

        DrawSceneLabel(species);
    }

    private void DrawCenterHandle(VegetationSpecies species)
    {
        EditorGUI.BeginChangeCheck();
        Vector3 newCenter = Handles.PositionHandle(species.colliderCenter, Quaternion.identity);
        if (!EditorGUI.EndChangeCheck()) return;

        Undo.RecordObject(species, "Move Vegetation Collider Center");
        species.colliderCenter = newCenter;
        EditorUtility.SetDirty(species);
        Repaint();
    }

    private void DrawColliderBoundsHandle(VegetationSpecies species)
    {
        switch (species.colliderType)
        {
            case VegetationColliderType.Box:
                DrawBoxHandle(species);
                break;

            case VegetationColliderType.Sphere:
                DrawSphereHandle(species);
                break;

            default:
                DrawCapsuleHandle(species);
                break;
        }
    }

    private void DrawCapsuleHandle(VegetationSpecies species)
    {
        capsuleHandle.center = species.colliderCenter;
        capsuleHandle.radius = Mathf.Max(0.001f, species.capsuleRadius);
        capsuleHandle.height = Mathf.Max(capsuleHandle.radius * 2f, species.capsuleHeight);
        capsuleHandle.heightAxis = CapsuleBoundsHandle.HeightAxis.Y;
        capsuleHandle.SetColor(new Color(0.2f, 1f, 0.35f, 1f));

        EditorGUI.BeginChangeCheck();
        capsuleHandle.DrawHandle();
        if (!EditorGUI.EndChangeCheck()) return;

        Undo.RecordObject(species, "Edit Vegetation Capsule Collider");
        species.colliderCenter = capsuleHandle.center;
        species.capsuleRadius = Mathf.Max(0.001f, capsuleHandle.radius);
        species.capsuleHeight = Mathf.Max(species.capsuleRadius * 2f, capsuleHandle.height);
        EditorUtility.SetDirty(species);
        Repaint();
    }

    private void DrawBoxHandle(VegetationSpecies species)
    {
        boxHandle.center = species.colliderCenter;
        boxHandle.size = new Vector3(Mathf.Max(0.001f, species.boxColliderSize.x), Mathf.Max(0.001f, species.boxColliderSize.y), Mathf.Max(0.001f, species.boxColliderSize.z));
        boxHandle.SetColor(new Color(0.2f, 1f, 0.35f, 1f));

        EditorGUI.BeginChangeCheck();
        boxHandle.DrawHandle();
        if (!EditorGUI.EndChangeCheck()) return;

        Undo.RecordObject(species, "Edit Vegetation Box Collider");
        species.colliderCenter = boxHandle.center;
        species.boxColliderSize = new Vector3(Mathf.Max(0.001f, boxHandle.size.x), Mathf.Max(0.001f, boxHandle.size.y), Mathf.Max(0.001f, boxHandle.size.z));
        EditorUtility.SetDirty(species);
        Repaint();
    }

    private void DrawSphereHandle(VegetationSpecies species)
    {
        sphereHandle.center = species.colliderCenter;
        sphereHandle.radius = Mathf.Max(0.001f, species.sphereRadius);
        sphereHandle.SetColor(new Color(0.2f, 1f, 0.35f, 1f));

        EditorGUI.BeginChangeCheck();
        sphereHandle.DrawHandle();
        if (!EditorGUI.EndChangeCheck()) return;

        Undo.RecordObject(species, "Edit Vegetation Sphere Collider");
        species.colliderCenter = sphereHandle.center;
        species.sphereRadius = Mathf.Max(0.001f, sphereHandle.radius);
        EditorUtility.SetDirty(species);
        Repaint();
    }

    private void DrawSceneLabel(VegetationSpecies species)
    {
        Vector3 worldCenter = previewInstance.LocalToWorldMatrix.MultiplyPoint3x4(species.colliderCenter);
        float handleSize = HandleUtility.GetHandleSize(worldCenter);
        GUIStyle style = new GUIStyle(EditorStyles.helpBox);
        style.alignment = TextAnchor.MiddleCenter;
        style.fontSize = 11;
        style.normal.textColor = Color.white;

        Handles.Label(worldCenter + Vector3.up * handleSize * 0.8f, species.speciesName + "  Collider\nID " + previewInstance.persistentID, style);
    }

    private bool ValidatePreviewInstance()
    {
        if (!hasPreviewInstance || previewDatabase == null) return false;
        if (!DatabaseContainsSpecies(previewDatabase, Species)) return false;
        if (!previewDatabase.TryGetInstance(previewInstance.persistentID, out VegetationInstance current)) return false;

        int speciesIndex = previewDatabase.species.IndexOf(Species);
        if (current.speciesIndex != speciesIndex) return false;

        previewInstance = current;
        return true;
    }

    private void FindNearestPreviewInstance(SceneView sceneView)
    {
        hasPreviewInstance = false;
        previewDatabase = null;

        VegetationSpecies species = Species;
        if (species == null) return;

        Vector3 referencePosition = sceneView != null ? sceneView.pivot : Vector3.zero;
        float bestDistanceSqr = float.MaxValue;
        VegetationInstance bestInstance = default;
        VegetationDatabase bestDatabase = null;

        if (previewDatabaseOverride != null)
        {
            FindNearestInDatabase(previewDatabaseOverride, species, referencePosition, ref bestDistanceSqr, ref bestDatabase, ref bestInstance);
        }
        else
        {
            VegetationRenderer[] renderers = UnityEngine.Object.FindObjectsOfType<VegetationRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                VegetationRenderer renderer = renderers[i];
                if (renderer == null || renderer.database == null) continue;
                if (!renderer.gameObject.scene.IsValid() || !renderer.gameObject.scene.isLoaded) continue;

                FindNearestInDatabase(renderer.database, species, referencePosition, ref bestDistanceSqr, ref bestDatabase, ref bestInstance);
            }
        }

        if (bestDatabase != null)
        {
            previewDatabase = bestDatabase;
            previewInstance = bestInstance;
            hasPreviewInstance = true;
        }

        Repaint();
        SceneView.RepaintAll();
    }

    private static void FindNearestInDatabase(VegetationDatabase database, VegetationSpecies species, Vector3 referencePosition, ref float bestDistanceSqr, ref VegetationDatabase bestDatabase, ref VegetationInstance bestInstance)
    {
        if (!DatabaseContainsSpecies(database, species) || database.chunks == null) return;

        int speciesIndex = database.species.IndexOf(species);
        for (int chunkIndex = 0; chunkIndex < database.chunks.Count; chunkIndex++)
        {
            VegetationChunkData chunk = database.chunks[chunkIndex];
            if (chunk == null || chunk.instances == null) continue;

            for (int instanceIndex = 0; instanceIndex < chunk.instances.Count; instanceIndex++)
            {
                VegetationInstance instance = chunk.instances[instanceIndex];
                if (instance.speciesIndex != speciesIndex) continue;

                float dx = instance.position.x - referencePosition.x;
                float dz = instance.position.z - referencePosition.z;
                float distanceSqr = dx * dx + dz * dz;
                if (distanceSqr >= bestDistanceSqr) continue;

                bestDistanceSqr = distanceSqr;
                bestDatabase = database;
                bestInstance = instance;
            }
        }
    }

    private static bool DatabaseContainsSpecies(VegetationDatabase database, VegetationSpecies species)
    {
        return database != null && species != null && database.species != null && database.species.Contains(species);
    }

    private void FocusPreviewInstance(SceneView sceneView)
    {
        if (sceneView == null || !ValidatePreviewInstance()) return;

        VegetationSpecies species = Species;
        Bounds localBounds = species.GetLocalMeshBounds();
        Vector3 worldCenter = previewInstance.LocalToWorldMatrix.MultiplyPoint3x4(localBounds.center);
        float maxScale = Mathf.Max(Mathf.Abs(previewInstance.scale.x), Mathf.Abs(previewInstance.scale.y), Mathf.Abs(previewInstance.scale.z));
        float size = Mathf.Max(1f, localBounds.extents.magnitude * maxScale * 1.5f);

        sceneView.pivot = worldCenter;
        sceneView.size = size;
        sceneView.Repaint();
    }

    private static string FormatVector3(Vector3 value)
    {
        return "(" + value.x.ToString("0.###") + ", " + value.y.ToString("0.###") + ", " + value.z.ToString("0.###") + ")";
    }
}
