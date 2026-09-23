using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[CustomEditor(typeof(VegetationProceduralGenerator))]
public class VegetationProceduralGeneratorEditor : Editor
{
    private enum BiomeMaskPreviewMode
    {
        BiomeStructure,
        SelectedLayer
    }

    private bool showBiomeMaskPreview = false;
    private BiomeMaskPreviewMode previewMode = BiomeMaskPreviewMode.BiomeStructure;
    private int previewResolution = 24;
    private float previewOpacity = 0.32f;
    private float previewHeightOffset = 0.15f;
    private float structureThreshold = 0.50f;
    private float structureEdgeWidth = 0.08f;
    private int selectedLayerIndex = 0;

    private static readonly Color ForestColor = new Color(0.10f, 0.85f, 0.22f, 1f);
    private static readonly Color EdgeColor = new Color(1.00f, 0.58f, 0.08f, 1f);
    private static readonly Color ClearingColor = new Color(0.12f, 0.45f, 0.95f, 1f);

    private static readonly Color LayerLowColor = new Color(0.95f, 0.15f, 0.10f, 1f);
    private static readonly Color LayerMidColor = new Color(1.00f, 0.72f, 0.10f, 1f);
    private static readonly Color LayerHighColor = new Color(0.10f, 0.90f, 0.20f, 1f);

    public override void OnInspectorGUI()
    {
        VegetationProceduralGenerator generator = (VegetationProceduralGenerator)target;

        serializedObject.Update();

        EditorGUI.BeginChangeCheck();

        DrawDefaultInspector();

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(10f);

        DrawGenerationSection(generator);

        EditorGUILayout.Space(10f);

        DrawBiomeMaskPreviewSection(generator);

        if (EditorGUI.EndChangeCheck())
        {
            SceneView.RepaintAll();
        }
    }

    private void DrawGenerationSection(VegetationProceduralGenerator generator)
    {
        EditorGUILayout.LabelField("Procedural Generation", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "不会创建Prefab或临时GameObject。生成结果会直接写入VegetationDatabase，并由GPU Vegetation Renderer显示。\n\n建议Layer顺序：Trees -> Rocks -> Bushes -> Flowers -> Grass。",
            MessageType.Info
        );

        using (new EditorGUI.DisabledScope(Application.isPlaying || generator.database == null || generator.biome == null))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Generate", GUILayout.Height(34f)))
            {
                RunGenerate(generator, false);
            }

            if (GUILayout.Button("Regenerate", GUILayout.Height(34f)))
            {
                if (EditorUtility.DisplayDialog(
                    "Regenerate Vegetation",
                    "会先删除这个Generator以前生成的实例，然后按照当前Biome重新生成。\n\n手工绘制的实例不会被删除。",
                    "Regenerate",
                    "Cancel"))
                {
                    RunGenerate(generator, true);
                }
            }

            EditorGUILayout.EndHorizontal();

            Color oldBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(1f, 0.55f, 0.45f);

            if (GUILayout.Button("Clear Generated", GUILayout.Height(30f)))
            {
                if (EditorUtility.DisplayDialog(
                    "Clear Generated Vegetation",
                    "只会删除这个Generator自己生成的实例。\n\n确定清除？",
                    "Clear",
                    "Cancel"))
                {
                    ClearGenerated(generator);
                }
            }

            GUI.backgroundColor = oldBackground;
        }

        EditorGUILayout.Space(8f);

        EditorGUILayout.LabelField("Last Generated", generator.LastGeneratedCount.ToString("N0"));
        EditorGUILayout.LabelField("Last Removed", generator.LastRemovedCount.ToString("N0"));
        EditorGUILayout.LabelField("Generation Time", $"{generator.LastGenerationSeconds:F2} s");
        EditorGUILayout.LabelField("Managed ID Ranges", generator.GeneratedRangeCount.ToString());

        EditorGUILayout.LabelField("Biome Mask Rejected", generator.LastBiomeMaskRejectCount.ToString("N0"));
        EditorGUILayout.LabelField("Terrain Layer Rejected", generator.LastTerrainLayerRejectCount.ToString("N0"));
        EditorGUILayout.LabelField("Terrain Density Rejected", generator.LastTerrainDensityRejectCount.ToString("N0"));
        EditorGUILayout.LabelField("Parent Influence Rejected", generator.LastParentInfluenceRejectCount.ToString("N0"));
        EditorGUILayout.LabelField("Exclusion Rejected", generator.LastExclusionRejectCount.ToString("N0"));
        EditorGUILayout.LabelField("Density Volume Rejected", generator.LastDensityRejectCount.ToString("N0"));

        if (generator.database != null)
        {
            EditorGUILayout.Space(4f);

            EditorGUILayout.LabelField("Database Instances", generator.database.GetTotalInstanceCount().ToString("N0"));
            EditorGUILayout.LabelField(
                "Database Chunks",
                generator.database.chunks != null ? generator.database.chunks.Count.ToString("N0") : "0"
            );
        }

        EditorGUILayout.Space(6f);

        EditorGUILayout.HelpBox(
            "Generate / Regenerate 会直接修改VegetationDatabase资产。需要回退时使用Clear Generated或版本控制。",
            MessageType.Warning
        );
    }

    private void DrawBiomeMaskPreviewSection(VegetationProceduralGenerator generator)
    {
        EditorGUILayout.LabelField("Biome Mask Scene Preview", EditorStyles.boldLabel);

        if (generator.biome == null)
        {
            EditorGUILayout.HelpBox("先指定Biome Profile。", MessageType.Info);
            return;
        }

        if (generator.biome.biomeMask == null || !generator.biome.biomeMask.enabled)
        {
            EditorGUILayout.HelpBox("当前Biome的Biome Mask没有启用。", MessageType.Info);
            return;
        }

        showBiomeMaskPreview = EditorGUILayout.Toggle("Show In Scene", showBiomeMaskPreview);

        if (!showBiomeMaskPreview)
        {
            return;
        }

        previewMode = (BiomeMaskPreviewMode)EditorGUILayout.EnumPopup("Preview Mode", previewMode);
        previewResolution = EditorGUILayout.IntSlider("Resolution", previewResolution, 8, 48);
        previewOpacity = EditorGUILayout.Slider("Opacity", previewOpacity, 0.05f, 0.8f);
        previewHeightOffset = EditorGUILayout.Slider("Height Offset", previewHeightOffset, 0.01f, 1f);

        if (previewMode == BiomeMaskPreviewMode.BiomeStructure)
        {
            structureThreshold = EditorGUILayout.Slider("Forest Threshold", structureThreshold, 0f, 1f);
            structureEdgeWidth = EditorGUILayout.Slider("Edge Width", structureEdgeWidth, 0.01f, 0.30f);

            EditorGUILayout.HelpBox(
                "绿色 = Forest\n橙色 = Forest Edge\n蓝灰 = Clearing\n\n这里只用于观察Biome的大尺度结构，不影响真正的生成参数。",
                MessageType.None
            );
        }
        else
        {
            DrawLayerSelector(generator);

            EditorGUILayout.HelpBox(
                "绿色 = 当前Layer高生成概率\n黄色 = 过渡区域\n红色 = 当前Layer低生成概率\n\n这里直接使用该Layer的Biome Mask设置进行计算。",
                MessageType.None
            );
        }

        if (GUILayout.Button("Repaint Scene Preview", GUILayout.Height(26f)))
        {
            SceneView.RepaintAll();
        }
    }

    private void DrawLayerSelector(VegetationProceduralGenerator generator)
    {
        if (generator.biome.layers == null || generator.biome.layers.Count == 0)
        {
            EditorGUILayout.HelpBox("Biome中没有Scatter Layer。", MessageType.Warning);
            return;
        }

        string[] names = new string[generator.biome.layers.Count];

        for (int i = 0; i < names.Length; i++)
        {
            VegetationScatterLayer layer = generator.biome.layers[i];

            if (layer == null)
            {
                names[i] = $"Layer {i} - NULL";
            }
            else if (string.IsNullOrWhiteSpace(layer.layerName))
            {
                names[i] = $"Layer {i}";
            }
            else
            {
                names[i] = layer.layerName;
            }
        }

        selectedLayerIndex = Mathf.Clamp(selectedLayerIndex, 0, names.Length - 1);
        selectedLayerIndex = EditorGUILayout.Popup("Preview Layer", selectedLayerIndex, names);
    }

    private void OnSceneGUI()
    {
        VegetationProceduralGenerator generator = (VegetationProceduralGenerator)target;

        if (!showBiomeMaskPreview)
        {
            return;
        }

        if (generator == null || generator.biome == null || generator.biome.biomeMask == null || !generator.biome.biomeMask.enabled)
        {
            return;
        }

        DrawBiomeMask(generator);
        DrawPreviewLegend(generator);
    }

    private void DrawBiomeMask(VegetationProceduralGenerator generator)
    {
        int resolution = Mathf.Clamp(previewResolution, 8, 48);

        float width = generator.areaSize.x;
        float depth = generator.areaSize.y;

        if (width <= 0f || depth <= 0f)
        {
            return;
        }

        float cellWidth = width / resolution;
        float cellDepth = depth / resolution;

        Quaternion rotation = generator.useTransformYRotation
            ? Quaternion.Euler(0f, generator.transform.eulerAngles.y, 0f)
            : Quaternion.identity;

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float localX0 = -width * 0.5f + x * cellWidth;
                float localX1 = localX0 + cellWidth;

                float localZ0 = -depth * 0.5f + z * cellDepth;
                float localZ1 = localZ0 + cellDepth;

                Vector3 world00 = generator.transform.position + rotation * new Vector3(localX0, 0f, localZ0);
                Vector3 world01 = generator.transform.position + rotation * new Vector3(localX0, 0f, localZ1);
                Vector3 world11 = generator.transform.position + rotation * new Vector3(localX1, 0f, localZ1);
                Vector3 world10 = generator.transform.position + rotation * new Vector3(localX1, 0f, localZ0);

                Vector3 center = (world00 + world01 + world11 + world10) * 0.25f;

                float mask = SampleBiomeMask(generator, center);
                Color color = GetPreviewColor(generator, mask);

                color.a = previewOpacity;

                if (!TryProjectToGround(generator, world00, out Vector3 p00)) continue;
                if (!TryProjectToGround(generator, world01, out Vector3 p01)) continue;
                if (!TryProjectToGround(generator, world11, out Vector3 p11)) continue;
                if (!TryProjectToGround(generator, world10, out Vector3 p10)) continue;

                p00.y += previewHeightOffset;
                p01.y += previewHeightOffset;
                p11.y += previewHeightOffset;
                p10.y += previewHeightOffset;

                Handles.color = color;

                Handles.DrawAAConvexPolygon(
                    p00,
                    p01,
                    p11,
                    p10
                );
            }
        }

        Handles.color = Color.white;
    }

    private Color GetPreviewColor(VegetationProceduralGenerator generator, float mask)
    {
        if (previewMode == BiomeMaskPreviewMode.BiomeStructure)
        {
            float minEdge = structureThreshold - structureEdgeWidth;
            float maxEdge = structureThreshold + structureEdgeWidth;

            if (mask < minEdge)
            {
                return ClearingColor;
            }

            if (mask > maxEdge)
            {
                return ForestColor;
            }

            return EdgeColor;
        }

        if (generator.biome.layers == null || generator.biome.layers.Count == 0)
        {
            return ClearingColor;
        }

        int index = Mathf.Clamp(selectedLayerIndex, 0, generator.biome.layers.Count - 1);
        VegetationScatterLayer layer = generator.biome.layers[index];

        if (layer == null)
        {
            return ClearingColor;
        }

        float density = EvaluateLayerBiomeMaskDensity(layer, mask);

        if (density <= 0.5f)
        {
            return Color.Lerp(LayerLowColor, LayerMidColor, density * 2f);
        }

        return Color.Lerp(LayerMidColor, LayerHighColor, (density - 0.5f) * 2f);
    }

    private static float EvaluateLayerBiomeMaskDensity(VegetationScatterLayer layer, float mask)
    {
        if (layer.biomeMaskMode == VegetationBiomeMaskMode.Ignore)
        {
            return 1f;
        }

        float gate = 1f;

        float threshold = layer.biomeMaskThreshold;
        float softness = Mathf.Max(0.001f, layer.biomeMaskSoftness);

        switch (layer.biomeMaskMode)
        {
            case VegetationBiomeMaskMode.High:
            {
                float start = threshold - softness;
                float end = threshold + softness;

                gate = Smooth01(
                    Mathf.InverseLerp(
                        start,
                        end,
                        mask
                    )
                );

                break;
            }

            case VegetationBiomeMaskMode.Low:
            {
                float start = threshold - softness;
                float end = threshold + softness;

                float highGate = Smooth01(
                    Mathf.InverseLerp(
                        start,
                        end,
                        mask
                    )
                );

                gate = 1f - highGate;

                break;
            }

            case VegetationBiomeMaskMode.EdgeBand:
            {
                float distance = Mathf.Abs(mask - threshold);
                float width = Mathf.Max(0.001f, layer.biomeMaskEdgeWidth);
                float edgeEnd = width + softness;

                gate = 1f - Smooth01(
                    Mathf.InverseLerp(
                        width,
                        edgeEnd,
                        distance
                    )
                );

                break;
            }
        }

        return Mathf.Lerp(
            layer.biomeMaskDensityOutside,
            layer.biomeMaskDensityInside,
            Mathf.Clamp01(gate)
        );
    }

    private static float SampleBiomeMask(VegetationProceduralGenerator generator, Vector3 worldPosition)
    {
        VegetationBiomeMaskSettings settings = generator.biome.biomeMask;

        int maskSeed = CombineSeed(
            generator.seed,
            settings.seedOffset,
            0x6B31
        );

        Vector2 baseOffset = GetSeedOffset(maskSeed, 0x1337);
        Vector2 detailOffset = GetSeedOffset(maskSeed, 0x2749);
        Vector2 warpOffsetA = GetSeedOffset(maskSeed, 0x3811);
        Vector2 warpOffsetB = GetSeedOffset(maskSeed, 0x4927);

        Vector2 worldXZ = new Vector2(
            worldPosition.x,
            worldPosition.z
        );

        float warpX = FractalNoise(
            worldXZ.x * settings.warpScale + warpOffsetA.x,
            worldXZ.y * settings.warpScale + warpOffsetA.y
        ) - 0.5f;

        float warpY = FractalNoise(
            worldXZ.x * settings.warpScale + warpOffsetB.x,
            worldXZ.y * settings.warpScale + warpOffsetB.y
        ) - 0.5f;

        Vector2 warpedPosition = worldXZ + new Vector2(warpX, warpY) * settings.warpStrength;

        float largeScale = FractalNoise(
            warpedPosition.x * settings.baseScale + baseOffset.x,
            warpedPosition.y * settings.baseScale + baseOffset.y
        );

        float detail = FractalNoise(
            warpedPosition.x * settings.detailScale + detailOffset.x,
            warpedPosition.y * settings.detailScale + detailOffset.y
        );

        float value = largeScale + (detail - 0.5f) * settings.detailStrength;

        value = Mathf.Clamp01(value);
        value = Smooth01(value);

        if (settings.invert)
        {
            value = 1f - value;
        }

        return value;
    }

    private bool TryProjectToGround(VegetationProceduralGenerator generator, Vector3 worldPosition, out Vector3 projected)
    {
        Terrain[] terrains = Terrain.activeTerrains;

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];

            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;

            if (worldPosition.x < terrainPosition.x || worldPosition.x > terrainPosition.x + terrainSize.x)
            {
                continue;
            }

            if (worldPosition.z < terrainPosition.z || worldPosition.z > terrainPosition.z + terrainSize.z)
            {
                continue;
            }

            float y = terrain.SampleHeight(worldPosition) + terrainPosition.y;

            projected = new Vector3(
                worldPosition.x,
                y,
                worldPosition.z
            );

            return true;
        }

        Vector3 rayOrigin = new Vector3(
            worldPosition.x,
            generator.transform.position.y + generator.rayHeight,
            worldPosition.z
        );

        if (Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            generator.rayDistance,
            generator.groundMask,
            QueryTriggerInteraction.Ignore))
        {
            projected = hit.point;
            return true;
        }

        projected = worldPosition;
        return false;
    }

    private void DrawPreviewLegend(VegetationProceduralGenerator generator)
    {
        Quaternion rotation = generator.useTransformYRotation
            ? Quaternion.Euler(0f, generator.transform.eulerAngles.y, 0f)
            : Quaternion.identity;

        Vector3 corner = generator.transform.position + rotation * new Vector3(
            -generator.areaSize.x * 0.5f,
            0f,
            -generator.areaSize.y * 0.5f
        );

        if (!TryProjectToGround(generator, corner, out Vector3 labelPosition))
        {
            return;
        }

        labelPosition.y += previewHeightOffset + 1f;

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel);
        style.fontSize = 13;
        style.normal.textColor = Color.white;

        string text;

        if (previewMode == BiomeMaskPreviewMode.BiomeStructure)
        {
            text =
                $"Biome Mask\n" +
                $"Forest > {(structureThreshold + structureEdgeWidth):F2}\n" +
                $"Edge {(structureThreshold - structureEdgeWidth):F2} - {(structureThreshold + structureEdgeWidth):F2}\n" +
                $"Clearing < {(structureThreshold - structureEdgeWidth):F2}";
        }
        else
        {
            string layerName = "Unknown";

            if (generator.biome.layers != null && generator.biome.layers.Count > 0)
            {
                int index = Mathf.Clamp(selectedLayerIndex, 0, generator.biome.layers.Count - 1);

                if (generator.biome.layers[index] != null)
                {
                    layerName = generator.biome.layers[index].layerName;
                }
            }

            text = $"Biome Mask Layer Response\n{layerName}";
        }

        Handles.Label(
            labelPosition,
            text,
            style
        );
    }

    private static float FractalNoise(float x, float y)
    {
        float value = 0f;
        float amplitude = 0.5714286f;
        float frequency = 1f;
        float amplitudeSum = 0f;

        for (int octave = 0; octave < 3; octave++)
        {
            value += Mathf.PerlinNoise(
                x * frequency,
                y * frequency
            ) * amplitude;

            amplitudeSum += amplitude;

            frequency *= 2.03f;
            amplitude *= 0.5f;
        }

        if (amplitudeSum <= 0f)
        {
            return 0f;
        }

        return value / amplitudeSum;
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static int CombineSeed(int a, int b, int c)
    {
        unchecked
        {
            int hash = 17;

            hash = hash * 31 + a;
            hash = hash * 31 + b;
            hash = hash * 31 + c;

            return hash;
        }
    }

    private static Vector2 GetSeedOffset(int seed, int salt)
    {
        unchecked
        {
            uint hashA = HashUInt((uint)(seed ^ salt));
            uint hashB = HashUInt(hashA ^ 0x9E3779B9u);

            float x = (hashA & 0x00FFFFFFu) / 16777215f;
            float y = (hashB & 0x00FFFFFFu) / 16777215f;

            return new Vector2(
                x * 10000f,
                y * 10000f
            );
        }
    }

    private static uint HashUInt(uint value)
    {
        value ^= value >> 16;
        value *= 0x7FEB352Du;
        value ^= value >> 15;
        value *= 0x846CA68Bu;
        value ^= value >> 16;

        return value;
    }

    private static void RunGenerate(VegetationProceduralGenerator generator, bool regenerate)
    {
        try
        {
            int generated;

            if (regenerate)
            {
                generated = generator.Regenerate(
                    (progress, message) =>
                    {
                        EditorUtility.DisplayProgressBar(
                            "Generating Vegetation",
                            message,
                            Mathf.Clamp01(progress)
                        );
                    }
                );
            }
            else
            {
                generated = generator.Generate(
                    (progress, message) =>
                    {
                        EditorUtility.DisplayProgressBar(
                            "Generating Vegetation",
                            message,
                            Mathf.Clamp01(progress)
                        );
                    }
                );
            }

            SaveChanges(generator);

            Debug.Log(
                $"Vegetation generation finished. Generated {generated:N0} instances.",
                generator
            );
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            SceneView.RepaintAll();
        }
    }

    private static void ClearGenerated(VegetationProceduralGenerator generator)
    {
        int removed = generator.ClearGenerated();

        SaveChanges(generator);

        Debug.Log(
            $"Vegetation generator removed {removed:N0} generated instances.",
            generator
        );

        SceneView.RepaintAll();
    }

    private static void SaveChanges(VegetationProceduralGenerator generator)
    {
        if (generator == null)
        {
            return;
        }

        EditorUtility.SetDirty(generator);

        if (generator.database != null)
        {
            EditorUtility.SetDirty(generator.database);
        }

        if (generator.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                generator.gameObject.scene
            );
        }

        AssetDatabase.SaveAssets();
    }
}