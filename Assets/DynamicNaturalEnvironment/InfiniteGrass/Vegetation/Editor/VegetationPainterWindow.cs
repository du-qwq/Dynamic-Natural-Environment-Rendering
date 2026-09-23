using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class VegetationPainterWindow : EditorWindow
{
    private enum PaintMode { Paint, Erase, Select }
    private enum SpeciesMode { Single, Mixed }

    private VegetationDatabase database;
    private VegetationRenderer renderer;
    private SpeciesMode speciesMode = SpeciesMode.Single;
    private VegetationSpecies selectedSpecies;
    private VegetationBrushPreset brushPreset;
    private PaintMode paintMode = PaintMode.Paint;

    private bool paintingEnabled;
    private bool eraseOnlySelectedSpecies = true;

    private float brushRadius = 5f;
    private float density = 2f;
    private float brushSpacing = 0.25f;
    private float densityVariation = 0.15f;
    private AnimationCurve brushFalloff = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.65f, 0.85f), new Keyframe(1f, 0f));

    private Texture2D brushMask;
    private float brushMaskRotation;
    private bool invertBrushMask;
    private float brushMaskStrength = 1f;

    private float minSlope;
    private float maxSlope = 45f;
    private float minHeight = -1000f;
    private float maxHeight = 10000f;

    private bool useTerrainLayerFilter;
    private TerrainLayer requiredTerrainLayer;
    private float minimumTerrainLayerWeight = 0.5f;

    private LayerMask groundMask = ~0;
    private float rayHeight = 1000f;
    private int maxInstancesPerStamp = 512;

    private float selectionRadius = 1.5f;
    private bool selectOnlyCurrentSpecies;
    private int selectedInstanceID = -1;
    private bool selectedTransformDirty;

    private bool isStrokeActive;
    private bool hasLastStampPosition;
    private Vector3 lastStampPosition;

    private System.Random random;
    private readonly Dictionary<Terrain, float[,,]> terrainAlphaCache = new Dictionary<Terrain, float[,,]>();

    [MenuItem("Tools/Vegetation/Vegetation Painter")]
    public static void Open()
    {
        GetWindow<VegetationPainterWindow>("Vegetation Painter");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += OnUndoRedo;
        random = new System.Random(Environment.TickCount);
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Undo.undoRedoPerformed -= OnUndoRedo;

        terrainAlphaCache.Clear();

        if (
            database != null &&
            isStrokeActive
        )
        {
            database.EndBatchMutation();
            isStrokeActive = false;
        }

        if (
            database != null &&
            selectedTransformDirty
        )
        {
            AssetDatabase.SaveAssets();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Vegetation Painter", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        database = (VegetationDatabase)EditorGUILayout.ObjectField("Database", database, typeof(VegetationDatabase), false);
        renderer = (VegetationRenderer)EditorGUILayout.ObjectField("Renderer", renderer, typeof(VegetationRenderer), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Species", EditorStyles.boldLabel);

        speciesMode = (SpeciesMode)GUILayout.Toolbar((int)speciesMode, new[] { "Single", "Mixed" });

        if (speciesMode == SpeciesMode.Single) selectedSpecies = (VegetationSpecies)EditorGUILayout.ObjectField("Species", selectedSpecies, typeof(VegetationSpecies), false);
        else brushPreset = (VegetationBrushPreset)EditorGUILayout.ObjectField("Brush Preset", brushPreset, typeof(VegetationBrushPreset), false);

        EditorGUILayout.Space();

        paintingEnabled = EditorGUILayout.Toggle("Enable Editing", paintingEnabled);
        paintMode = (PaintMode)GUILayout.Toolbar((int)paintMode, new[] { "Paint", "Erase", "Select" });

        EditorGUILayout.Space();

        if (paintMode == PaintMode.Paint) DrawPaintSettings();
        else if (paintMode == PaintMode.Erase) DrawEraseSettings();
        else DrawSelectSettings();

        if (paintMode != PaintMode.Select) DrawPlacementSettings();

        EditorGUILayout.Space();

        if (database != null) EditorGUILayout.HelpBox($"Instances: {database.GetTotalInstanceCount()}\nChunks: {database.chunks.Count}\nSpecies: {database.species.Count}", MessageType.Info);

        DrawWarnings();
    }

    private void DrawPaintSettings()
    {
        EditorGUILayout.LabelField("Brush", EditorStyles.boldLabel);

        brushRadius = EditorGUILayout.Slider("Radius", brushRadius, 0.25f, 50f);
        brushSpacing = EditorGUILayout.Slider("Stroke Spacing", brushSpacing, 0.05f, 1f);
        density = EditorGUILayout.Slider("Density / m²", density, 0.01f, 30f);
        densityVariation = EditorGUILayout.Slider("Density Variation", densityVariation, 0f, 1f);
        brushFalloff = EditorGUILayout.CurveField("Falloff", brushFalloff);
        maxInstancesPerStamp = EditorGUILayout.IntSlider("Max / Stamp", maxInstancesPerStamp, 1, 2048);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Brush Mask", EditorStyles.boldLabel);

        brushMask = (Texture2D)EditorGUILayout.ObjectField("Mask", brushMask, typeof(Texture2D), false);

        if (brushMask == null) return;

        brushMaskRotation = EditorGUILayout.Slider("Rotation", brushMaskRotation, 0f, 360f);
        brushMaskStrength = EditorGUILayout.Slider("Strength", brushMaskStrength, 0f, 1f);
        invertBrushMask = EditorGUILayout.Toggle("Invert", invertBrushMask);

        if (brushMask.isReadable) return;

        EditorGUILayout.HelpBox("Brush Mask需要开启Read/Write。", MessageType.Warning);

        if (GUILayout.Button("自动开启 Read/Write")) EnableTextureReadWrite(brushMask);
    }

    private void DrawEraseSettings()
    {
        EditorGUILayout.LabelField("Erase", EditorStyles.boldLabel);

        brushRadius = EditorGUILayout.Slider("Radius", brushRadius, 0.25f, 50f);
        eraseOnlySelectedSpecies = EditorGUILayout.Toggle("Only Selected Species", eraseOnlySelectedSpecies);
    }

    private void DrawSelectSettings()
    {
        EditorGUILayout.LabelField("Instance Edit", EditorStyles.boldLabel);

        selectionRadius = EditorGUILayout.Slider("Pick Radius", selectionRadius, 0.05f, 10f);

        using (new EditorGUI.DisabledScope(speciesMode != SpeciesMode.Single || selectedSpecies == null))
        {
            selectOnlyCurrentSpecies = EditorGUILayout.Toggle("Only Current Species", selectOnlyCurrentSpecies);
        }

        EditorGUILayout.HelpBox("左键选择实例。W移动，E旋转，R缩放，Delete删除。", MessageType.Info);

        if (database == null || selectedInstanceID < 0) return;

        if (!database.TryGetInstance(selectedInstanceID, out VegetationInstance instance))
        {
            selectedInstanceID = -1;
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Selected Instance", EditorStyles.boldLabel);

        EditorGUILayout.IntField("Persistent ID", instance.persistentID);

        string speciesName = "Unknown";

        if (instance.speciesIndex >= 0 && instance.speciesIndex < database.species.Count && database.species[instance.speciesIndex] != null) speciesName = database.species[instance.speciesIndex].speciesName;

        EditorGUILayout.TextField("Species", speciesName);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.Vector3Field("Position", instance.position);
            EditorGUILayout.Vector3Field("Rotation", instance.rotation.eulerAngles);
            EditorGUILayout.Vector3Field("Scale", instance.scale);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Delete Selected Instance")) DeleteSelectedInstance();
    }

    private void DrawPlacementSettings()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Placement Filter", EditorStyles.boldLabel);

        groundMask = LayerMaskField("Ground Mask", groundMask);
        minSlope = EditorGUILayout.Slider("Min Slope", minSlope, 0f, 90f);
        maxSlope = EditorGUILayout.Slider("Max Slope", maxSlope, 0f, 90f);
        minHeight = EditorGUILayout.FloatField("Min Height", minHeight);
        maxHeight = EditorGUILayout.FloatField("Max Height", maxHeight);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Terrain Layer Filter", EditorStyles.boldLabel);

        useTerrainLayerFilter = EditorGUILayout.Toggle("Enable", useTerrainLayerFilter);

        if (!useTerrainLayerFilter) return;

        requiredTerrainLayer = (TerrainLayer)EditorGUILayout.ObjectField("Terrain Layer", requiredTerrainLayer, typeof(TerrainLayer), false);
        minimumTerrainLayerWeight = EditorGUILayout.Slider("Minimum Weight", minimumTerrainLayerWeight, 0f, 1f);
    }

    private void DrawWarnings()
    {
        if (!paintingEnabled) return;

        if (database == null)
        {
            EditorGUILayout.HelpBox("请选择 VegetationDatabase。", MessageType.Warning);
            return;
        }

        if (paintMode != PaintMode.Paint) return;

        if (speciesMode == SpeciesMode.Single && selectedSpecies == null) EditorGUILayout.HelpBox("Single模式请选择VegetationSpecies。", MessageType.Warning);
        else if (speciesMode == SpeciesMode.Mixed && brushPreset == null) EditorGUILayout.HelpBox("Mixed模式请选择VegetationBrushPreset。", MessageType.Warning);
        else if (brushMask != null && !brushMask.isReadable) EditorGUILayout.HelpBox("Brush Mask当前不可读取。", MessageType.Warning);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!paintingEnabled || database == null) return;

        Event evt = Event.current;

        if (paintMode == PaintMode.Select)
        {
            HandleDeleteKey(evt);
            DrawSelectedInstanceHandle(evt);
        }

        if (evt.alt) return;

        if (paintMode == PaintMode.Paint && speciesMode == SpeciesMode.Single && selectedSpecies == null) return;
        if (paintMode == PaintMode.Paint && speciesMode == SpeciesMode.Mixed && brushPreset == null) return;
        if (paintMode == PaintMode.Paint && brushMask != null && !brushMask.isReadable) return;

        Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
        bool hasGroundHit = Physics.Raycast(ray, out RaycastHit hit, 10000f, groundMask, QueryTriggerInteraction.Ignore);

        int defaultControlID = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(defaultControlID);

        if (paintMode == PaintMode.Select)
        {
            if (hasGroundHit) DrawSelectionCursor(hit);

            if (hasGroundHit && evt.type == EventType.MouseDown && evt.button == 0 && HandleUtility.nearestControl == defaultControlID)
            {
                SelectInstanceAt(hit.point);
                evt.Use();
            }

            if (selectedTransformDirty && evt.rawType == EventType.MouseUp)
            {
                selectedTransformDirty = false;
                AssetDatabase.SaveAssets();
            }

            sceneView.Repaint();
            return;
        }

        if (!hasGroundHit) return;

        Handles.color = paintMode == PaintMode.Paint ? new Color(0.2f, 1f, 0.2f, 1f) : new Color(1f, 0.25f, 0.2f, 1f);
        Handles.DrawWireDisc(hit.point, hit.normal, brushRadius);

        if (paintMode == PaintMode.Paint)
        {
            Handles.color = new Color(0.2f, 1f, 0.2f, 0.3f);
            Handles.DrawWireDisc(hit.point, hit.normal, brushRadius * 0.5f);
        }

        if (evt.button != 0) return;

        if (evt.type == EventType.MouseDown)
        {
            BeginStroke();
            Stamp(hit.point);
            evt.Use();
        }
        else if (evt.type == EventType.MouseDrag && isStrokeActive)
        {
            float requiredDistance = Mathf.Max(0.05f, brushRadius * brushSpacing);

            if (!hasLastStampPosition || Vector3.Distance(lastStampPosition, hit.point) >= requiredDistance) Stamp(hit.point);

            evt.Use();
        }
        else if (evt.type == EventType.MouseUp && isStrokeActive)
        {
            EndStroke();
            evt.Use();
        }

        sceneView.Repaint();
    }

    private void DrawSelectionCursor(RaycastHit hit)
    {
        Handles.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Handles.DrawWireDisc(hit.point, hit.normal, selectionRadius);
    }

    private void SelectInstanceAt(Vector3 position)
    {
        int speciesIndex = -1;

        if (selectOnlyCurrentSpecies && speciesMode == SpeciesMode.Single && selectedSpecies != null)
        {
            speciesIndex = database.GetSpeciesIndex(selectedSpecies);

            if (speciesIndex < 0)
            {
                selectedInstanceID = -1;
                Repaint();
                return;
            }
        }

        if (database.TryFindNearestInstance(position, selectionRadius, speciesIndex, out VegetationInstance instance)) selectedInstanceID = instance.persistentID;
        else selectedInstanceID = -1;

        Repaint();
        SceneView.RepaintAll();
    }

    private void DrawSelectedInstanceHandle(Event evt)
    {
        if (selectedInstanceID < 0) return;

        if (!database.TryGetInstance(selectedInstanceID, out VegetationInstance instance))
        {
            selectedInstanceID = -1;
            return;
        }

        Handles.color = Color.yellow;

        float markerSize = HandleUtility.GetHandleSize(instance.position) * 0.15f;

        Handles.DrawWireDisc(instance.position, Vector3.up, markerSize);
        Handles.DrawLine(instance.position, instance.position + Vector3.up * markerSize * 2f);

        Vector3 newPosition = instance.position;
        Quaternion newRotation = instance.rotation;
        Vector3 newScale = instance.scale;

        EditorGUI.BeginChangeCheck();

        switch (Tools.current)
        {
            case Tool.Move:
            {
                Quaternion handleRotation = Tools.pivotRotation == PivotRotation.Local ? instance.rotation : Quaternion.identity;
                newPosition = Handles.PositionHandle(instance.position, handleRotation);
                break;
            }

            case Tool.Rotate:
                newRotation = Handles.RotationHandle(instance.rotation, instance.position);
                break;

            case Tool.Scale:
                newScale = Handles.ScaleHandle(instance.scale, instance.position, instance.rotation, HandleUtility.GetHandleSize(instance.position));
                break;
        }

        if (!EditorGUI.EndChangeCheck()) return;

        Undo.RecordObject(database, "Edit Vegetation Instance");

        if (!database.UpdateInstanceTransform(selectedInstanceID, newPosition, newRotation, newScale)) return;

        EditorUtility.SetDirty(database);

        if (renderer != null) renderer.Rebuild();

        selectedTransformDirty = true;

        Repaint();
    }

    private void HandleDeleteKey(Event evt)
    {
        if (selectedInstanceID < 0 || evt.type != EventType.KeyDown) return;
        if (evt.keyCode != KeyCode.Delete && evt.keyCode != KeyCode.Backspace) return;

        DeleteSelectedInstance();
        evt.Use();
    }

    private void DeleteSelectedInstance()
    {
        if (database == null || selectedInstanceID < 0) return;

        Undo.RecordObject(database, "Delete Vegetation Instance");

        if (!database.RemoveInstance(selectedInstanceID)) return;

        selectedInstanceID = -1;

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        if (renderer != null) renderer.Rebuild();

        SceneView.RepaintAll();
        Repaint();
    }

    private void BeginStroke()
    {
        Undo.RecordObject(
            database,
            paintMode == PaintMode.Paint
                ? "Paint Vegetation"
                : "Erase Vegetation"
        );

        database.BeginBatchMutation();

        isStrokeActive = true;
        hasLastStampPosition = false;

        random = new System.Random(
            Guid.NewGuid().GetHashCode()
        );

        terrainAlphaCache.Clear();
    }

    private void EndStroke()
    {
        isStrokeActive = false;
        hasLastStampPosition = false;

        terrainAlphaCache.Clear();

        database.EndBatchMutation();

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();

        if (renderer != null)
        {
            renderer.Rebuild();
        }

        SceneView.RepaintAll();
        Repaint();
    }

    private void Stamp(Vector3 center)
    {
        if (paintMode == PaintMode.Paint) PaintStamp(center);
        else EraseStamp(center);

        lastStampPosition = center;
        hasLastStampPosition = true;

        EditorUtility.SetDirty(database);
    }

    private void PaintStamp(Vector3 center)
    {
        float area = Mathf.PI * brushRadius * brushRadius;
        float densityMultiplier = Mathf.Lerp(1f - densityVariation, 1f + densityVariation, Random01());
        int targetCount = Mathf.Clamp(Mathf.CeilToInt(area * density * densityMultiplier), 1, maxInstancesPerStamp);

        int placedCount = 0;
        int attemptCount = targetCount * 10;

        for (int i = 0; i < attemptCount && placedCount < targetCount; i++)
        {
            Vector2 normalizedOffset = RandomPointInCircle();
            float normalizedDistance = normalizedOffset.magnitude;

            float falloffWeight = Mathf.Clamp01(brushFalloff.Evaluate(normalizedDistance));
            float maskWeight = SampleBrushMask(normalizedOffset);

            if (Random01() > falloffWeight * maskWeight) continue;

            Vector2 offset = normalizedOffset * brushRadius;
            Vector3 candidate = new Vector3(center.x + offset.x, center.y, center.z + offset.y);

            if (!TryFindGround(candidate, out RaycastHit hit)) continue;

            float slope = Vector3.Angle(hit.normal, Vector3.up);

            if (slope < minSlope || slope > maxSlope) continue;
            if (hit.point.y < minHeight || hit.point.y > maxHeight) continue;
            if (!PassesTerrainLayerFilter(hit)) continue;

            VegetationSpecies species = GetSpeciesForInstance();
            if (species == null) continue;

            int speciesIndex = database.GetOrAddSpecies(species);
            if (speciesIndex < 0) continue;
            if (species.minSpacing > 0f && database.HasInstanceNear(speciesIndex, hit.point, species.minSpacing)) continue;

            Quaternion rotation = CalculateRotation(species, hit.normal);
            float scaleValue = RandomRange(species.scaleRange.x, species.scaleRange.y);
            Vector3 scale = Vector3.one * scaleValue;
            Vector3 placementPosition = CalculatePlacementPosition(species, hit.point, hit.normal, scaleValue);

            database.AddInstance(species, placementPosition, rotation, scale);

            placedCount++;
        }
    }

    private void EraseStamp(Vector3 center)
    {
        int speciesIndex = -1;

        if (eraseOnlySelectedSpecies)
        {
            VegetationSpecies species = speciesMode == SpeciesMode.Single ? selectedSpecies : null;

            if (species == null) return;

            speciesIndex = database.GetSpeciesIndex(species);

            if (speciesIndex < 0) return;
        }

        database.RemoveInstancesInRadius(center, brushRadius, speciesIndex);
    }

    private VegetationSpecies GetSpeciesForInstance()
    {
        if (speciesMode == SpeciesMode.Single) return selectedSpecies;
        if (brushPreset == null) return null;

        return brushPreset.GetRandomSpecies(Random01());
    }

    private Quaternion CalculateRotation(VegetationSpecies species, Vector3 groundNormal)
    {
        float yaw = species.randomYRotation ? RandomRange(0f, 360f) : 0f;

        if (!species.alignToTerrainNormal) return Quaternion.Euler(0f, yaw, 0f);

        Quaternion terrainRotation = Quaternion.FromToRotation(Vector3.up, groundNormal);
        return Quaternion.AngleAxis(yaw, groundNormal) * terrainRotation;
    }

    private static Vector3 CalculatePlacementPosition(VegetationSpecies species, Vector3 groundPoint, Vector3 groundNormal, float scaleValue)
    {
        if (species == null) return groundPoint;

        Vector3 placementAxis = species.alignToTerrainNormal ? groundNormal.normalized : Vector3.up;
        float autoOffset = 0f;

        if (species.groundPlacementMode == VegetationGroundPlacementMode.MeshBounds)
        {
            float bottomY = species.GetGroundPlacementBottomY();
            autoOffset = -bottomY * Mathf.Abs(scaleValue);
        }

        return groundPoint + placementAxis * (autoOffset + species.groundOffset);
    }

    private float SampleBrushMask(Vector2 normalizedOffset)
    {
        if (brushMask == null) return 1f;

        Vector2 rotated = Rotate(normalizedOffset, brushMaskRotation * Mathf.Deg2Rad);
        Vector2 uv = rotated * 0.5f + Vector2.one * 0.5f;

        uv.x = Mathf.Clamp01(uv.x);
        uv.y = Mathf.Clamp01(uv.y);

        float value = brushMask.GetPixelBilinear(uv.x, uv.y).grayscale;

        if (invertBrushMask) value = 1f - value;

        return Mathf.Lerp(1f, value, brushMaskStrength);
    }

    private bool PassesTerrainLayerFilter(RaycastHit hit)
    {
        if (!useTerrainLayerFilter) return true;
        if (requiredTerrainLayer == null) return false;

        Terrain terrain = hit.collider.GetComponent<Terrain>();

        if (terrain == null) terrain = hit.collider.GetComponentInParent<Terrain>();
        if (terrain == null || terrain.terrainData == null) return false;

        TerrainData data = terrain.terrainData;
        int layerIndex = Array.IndexOf(data.terrainLayers, requiredTerrainLayer);

        if (layerIndex < 0) return false;

        Vector3 terrainPosition = terrain.transform.position;

        float normalizedX = Mathf.InverseLerp(terrainPosition.x, terrainPosition.x + data.size.x, hit.point.x);
        float normalizedZ = Mathf.InverseLerp(terrainPosition.z, terrainPosition.z + data.size.z, hit.point.z);

        if (normalizedX < 0f || normalizedX > 1f || normalizedZ < 0f || normalizedZ > 1f) return false;

        if (!terrainAlphaCache.TryGetValue(terrain, out float[,,] alphamaps))
        {
            alphamaps = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
            terrainAlphaCache.Add(terrain, alphamaps);
        }

        int x = Mathf.Clamp(Mathf.RoundToInt(normalizedX * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(normalizedZ * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);

        return alphamaps[z, x, layerIndex] >= minimumTerrainLayerWeight;
    }

    private bool TryFindGround(Vector3 candidate, out RaycastHit hit)
    {
        Vector3 origin = candidate + Vector3.up * rayHeight;
        return Physics.Raycast(origin, Vector3.down, out hit, rayHeight * 2f, groundMask, QueryTriggerInteraction.Ignore);
    }

    private Vector2 RandomPointInCircle()
    {
        float angle = RandomRange(0f, Mathf.PI * 2f);
        float radius = Mathf.Sqrt(Random01());

        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private float Random01()
    {
        return (float)random.NextDouble();
    }

    private float RandomRange(float min, float max)
    {
        return Mathf.Lerp(min, max, Random01());
    }

    private static Vector2 Rotate(Vector2 value, float radians)
    {
        float sin = Mathf.Sin(radians);
        float cos = Mathf.Cos(radians);

        return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
    }

    private void OnUndoRedo()
    {
        terrainAlphaCache.Clear();

        if (selectedInstanceID >= 0 && !database.TryGetInstance(selectedInstanceID, out _)) selectedInstanceID = -1;
        if (renderer != null) renderer.Rebuild();

        SceneView.RepaintAll();
        Repaint();
    }

    private static void EnableTextureReadWrite(Texture2D texture)
    {
        string path = AssetDatabase.GetAssetPath(texture);

        if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

        importer.isReadable = true;
        importer.SaveAndReimport();
    }

    private static LayerMask LayerMaskField(string label, LayerMask layerMask)
    {
        string[] layers = UnityEditorInternal.InternalEditorUtility.layers;
        int visibleMask = 0;

        for (int i = 0; i < layers.Length; i++)
        {
            int layer = LayerMask.NameToLayer(layers[i]);

            if ((layerMask.value & (1 << layer)) != 0) visibleMask |= 1 << i;
        }

        visibleMask = EditorGUILayout.MaskField(label, visibleMask, layers);

        int mask = 0;

        for (int i = 0; i < layers.Length; i++)
        {
            if ((visibleMask & (1 << i)) == 0) continue;
            mask |= 1 << LayerMask.NameToLayer(layers[i]);
        }

        layerMask.value = mask;

        return layerMask;
    }
}