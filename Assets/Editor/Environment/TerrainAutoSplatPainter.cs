using UnityEditor;
using UnityEngine;

public class TerrainAutoSplatPainter : EditorWindow
{
    private Terrain targetTerrain;
    private TerrainLayer grassLayer;
    private TerrainLayer rockLayer;
    private TerrainLayer mudLayer;

    private float rockStartAngle = 18f;
    private float rockFullAngle = 36f;
    private float noiseScale = 2.5f;
    private float noiseStrength = 6f;
    private int seed = 128;

    private float waterHeight = 40f;
    private float mudHeightRange = 8f;
    private float mudMaxSlope = 16f;
    private float mudNoiseScale = 3f;
    private float mudNoiseStrength = 0.2f;

    [MenuItem("Tools/Environment/Terrain 自动材质分配")]
    public static void Open()
    {
        TerrainAutoSplatPainter window = GetWindow<TerrainAutoSplatPainter>("Terrain 自动材质");
        window.minSize = new Vector2(370f, 430f);
    }

    private void OnEnable()
    {
        if (targetTerrain == null) targetTerrain = Terrain.activeTerrain;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Terrain 自动材质分配", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("根据坡度分配 Grass / Rock，并根据水位与坡度分配湖岸 Mud。", MessageType.Info);

        EditorGUILayout.Space(8);
        targetTerrain = (Terrain)EditorGUILayout.ObjectField("目标 Terrain", targetTerrain, typeof(Terrain), true);
        grassLayer = (TerrainLayer)EditorGUILayout.ObjectField("草地 Layer", grassLayer, typeof(TerrainLayer), false);
        rockLayer = (TerrainLayer)EditorGUILayout.ObjectField("岩石 Layer", rockLayer, typeof(TerrainLayer), false);
        mudLayer = (TerrainLayer)EditorGUILayout.ObjectField("泥地 Layer", mudLayer, typeof(TerrainLayer), false);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("岩石坡度", EditorStyles.boldLabel);
        rockStartAngle = EditorGUILayout.Slider("开始出现岩石", rockStartAngle, 0f, 60f);
        rockFullAngle = EditorGUILayout.Slider("完全岩石坡度", rockFullAngle, 1f, 80f);
        noiseScale = EditorGUILayout.Slider("岩石噪声尺度", noiseScale, 0.5f, 15f);
        noiseStrength = EditorGUILayout.Slider("岩石坡度扰动", noiseStrength, 0f, 12f);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("湖岸泥地", EditorStyles.boldLabel);
        waterHeight = EditorGUILayout.FloatField("水面高度", waterHeight);
        mudHeightRange = EditorGUILayout.Slider("泥地区域高度范围", mudHeightRange, 1f, 25f);
        mudMaxSlope = EditorGUILayout.Slider("泥地最大坡度", mudMaxSlope, 1f, 35f);
        mudNoiseScale = EditorGUILayout.Slider("泥地噪声尺度", mudNoiseScale, 0.5f, 10f);
        mudNoiseStrength = EditorGUILayout.Slider("泥地边界扰动", mudNoiseStrength, 0f, 0.5f);

        EditorGUILayout.Space(8);
        seed = EditorGUILayout.IntField("随机种子", seed);

        EditorGUILayout.Space(12);

        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("自动分配 Grass / Rock / Mud", GUILayout.Height(40))) Paint();

        GUI.backgroundColor = Color.white;
    }

    private void Paint()
    {
        if (!Validate()) return;

        TerrainData data = targetTerrain.terrainData;
        TerrainLayer[] layers = data.terrainLayers;

        int grassIndex = FindLayerIndex(layers, grassLayer);
        int rockIndex = FindLayerIndex(layers, rockLayer);
        int mudIndex = FindLayerIndex(layers, mudLayer);

        if (grassIndex < 0 || rockIndex < 0 || mudIndex < 0)
        {
            EditorUtility.DisplayDialog("Layer 不存在", "请确认 Grass、Rock、Mud 都已添加到当前 Terrain。", "确定");
            return;
        }

        if (rockFullAngle <= rockStartAngle)
        {
            EditorUtility.DisplayDialog("坡度设置错误", "“完全岩石坡度”必须大于“开始出现岩石”。", "确定");
            return;
        }

        Undo.RegisterCompleteObjectUndo(data, "Terrain 自动材质分配");

        int width = data.alphamapWidth;
        int height = data.alphamapHeight;
        int layerCount = layers.Length;

        float[,,] map = data.GetAlphamaps(0, 0, width, height);

        float offsetX = seed * 0.137f + 13.7f;
        float offsetZ = seed * 0.271f + 47.3f;

        for (int y = 0; y < height; y++)
        {
            float nz = y / (height - 1f);

            for (int x = 0; x < width; x++)
            {
                float nx = x / (width - 1f);

                float slope = data.GetSteepness(nx, nz);
                float worldHeight = data.GetInterpolatedHeight(nx, nz) + targetTerrain.transform.position.y;

                float rockNoise = Mathf.PerlinNoise(nx * noiseScale + offsetX, nz * noiseScale + offsetZ) * 2f - 1f;
                float adjustedSlope = slope + rockNoise * noiseStrength;
                float rockWeight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rockStartAngle, rockFullAngle, adjustedSlope));

                float heightDistance = Mathf.Abs(worldHeight - waterHeight);
                float mudHeightWeight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, mudHeightRange, heightDistance));
                float mudSlopeWeight = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(mudMaxSlope * 0.6f, mudMaxSlope, slope));

                float mudNoise = Mathf.PerlinNoise(nx * mudNoiseScale + offsetX + 71f, nz * mudNoiseScale + offsetZ + 31f);
                mudNoise = Mathf.Lerp(1f - mudNoiseStrength, 1f, mudNoise);

                float mudWeight = mudHeightWeight * mudSlopeWeight * mudNoise;
                mudWeight *= 1f - rockWeight;
                mudWeight = Mathf.Clamp01(mudWeight);

                float grassWeight = Mathf.Clamp01(1f - rockWeight - mudWeight);

                float otherWeight = 0f;

                for (int i = 0; i < layerCount; i++)
                {
                    if (i == grassIndex || i == rockIndex || i == mudIndex) continue;
                    otherWeight += map[y, x, i];
                }

                float availableWeight = Mathf.Clamp01(1f - otherWeight);

                map[y, x, grassIndex] = grassWeight * availableWeight;
                map[y, x, rockIndex] = rockWeight * availableWeight;
                map[y, x, mudIndex] = mudWeight * availableWeight;
            }
        }

        data.SetAlphamaps(0, 0, map);
        EditorUtility.SetDirty(data);
        targetTerrain.Flush();
        SceneView.RepaintAll();
    }

    private bool Validate()
    {
        if (targetTerrain == null || targetTerrain.terrainData == null)
        {
            EditorUtility.DisplayDialog("缺少 Terrain", "请设置目标 Terrain。", "确定");
            return false;
        }

        if (grassLayer == null || rockLayer == null || mudLayer == null)
        {
            EditorUtility.DisplayDialog("缺少 Layer", "请指定 Grass、Rock、Mud 三个 Terrain Layer。", "确定");
            return false;
        }

        return true;
    }

    private static int FindLayerIndex(TerrainLayer[] layers, TerrainLayer target)
    {
        for (int i = 0; i < layers.Length; i++)
            if (layers[i] == target) return i;

        return -1;
    }
}