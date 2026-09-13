using UnityEngine;
using UnityEditor;
using System.IO;

public class CloudCoverageMapGenerator : EditorWindow
{
    [SerializeField, InspectorName("纹理尺寸")] private int textureSize = 512;
    [SerializeField, InspectorName("随机种子")] private int seed = 128;
    [SerializeField, InspectorName("边缘扰动强度"), Range(0f, 0.5f)] private float noiseStrength = 0.16f;
    [SerializeField, InspectorName("整体模糊"), Range(0f, 12f)] private float blurRadius = 4f;

    private const string OutputDirectory = "Assets/DynamicNaturalEnvironment/Clouds/Textures";
    private const string OutputPath = OutputDirectory + "/T_CloudCoverage_Hero01.png";

    [MenuItem("Tools/Environment/体积云/生成 Hero 云分布图")]
    public static void OpenWindow()
    {
        GetWindow<CloudCoverageMapGenerator>("云分布图生成器");
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "生成用于 Hero Shot 的宏观云分布图。\n" +
            "白色 = 容易生成云\n" +
            "黑色 = 晴空\n\n" +
            "生成结果只负责大尺度云团位置，云内部体积和边缘仍由原来的 Shape / Erosion Noise 负责。",
            MessageType.Info
        );

        textureSize = EditorGUILayout.IntPopup("纹理尺寸", textureSize, new[] { "256", "512", "1024" }, new[] { 256, 512, 1024 });
        seed = EditorGUILayout.IntField("随机种子", seed);
        noiseStrength = EditorGUILayout.Slider("边缘扰动强度", noiseStrength, 0f, 0.5f);
        blurRadius = EditorGUILayout.Slider("整体模糊", blurRadius, 0f, 12f);

        GUILayout.Space(8);

        if (GUILayout.Button("生成参考图云分布", GUILayout.Height(34)))
        {
            Generate();
        }

        GUILayout.Space(8);
        EditorGUILayout.LabelField("输出位置", OutputPath);
    }

    private void Generate()
    {
        if (!Directory.Exists(OutputDirectory)) Directory.CreateDirectory(OutputDirectory);

        Random.InitState(seed);

        int size = Mathf.Clamp(textureSize, 128, 2048);
        float[,] values = new float[size, size];

        // ------------------------------------------------------------
        // Hero Shot 宏观构图
        //
        // 主体逻辑：
        // 中上区域：主要积云群
        // 左右：少量辅助云
        // 中央偏下：主动留出蓝天
        //
        // 注意：
        // 最终镜头中的实际位置通过 Macro Offset 调整。
        // ------------------------------------------------------------

        AddBlob(values, 0.48f, 0.69f, 0.21f, 0.13f, 1.00f);
        AddBlob(values, 0.30f, 0.66f, 0.15f, 0.10f, 0.78f);
        AddBlob(values, 0.69f, 0.72f, 0.16f, 0.11f, 0.84f);

        AddBlob(values, 0.17f, 0.29f, 0.11f, 0.08f, 0.55f);
        AddBlob(values, 0.82f, 0.32f, 0.12f, 0.09f, 0.58f);

        // 主动挖出晴空，让云不是均匀铺满天空。
        SubtractBlob(values, 0.50f, 0.43f, 0.20f, 0.14f, 0.82f);
        SubtractBlob(values, 0.08f, 0.70f, 0.12f, 0.17f, 0.72f);
        SubtractBlob(values, 0.92f, 0.69f, 0.12f, 0.17f, 0.72f);

        ApplyLowFrequencyNoise(values, noiseStrength);
        Normalize(values);

        Texture2D texture = new Texture2D(size, size, TextureFormat.R8, false, true);
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float value = Mathf.Clamp01(values[x, y]);
                pixels[y * size + x] = new Color(value, value, value, 1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);

        if (blurRadius > 0.01f)
        {
            texture = Blur(texture, Mathf.RoundToInt(blurRadius));
        }

        File.WriteAllBytes(OutputPath, texture.EncodeToPNG());
        DestroyImmediate(texture);

        AssetDatabase.Refresh();

        TextureImporter importer = AssetImporter.GetAtPath(OutputPath) as TextureImporter;

        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(OutputPath);
        Debug.Log("已生成宏观云分布图：" + OutputPath);
    }

    private static void AddBlob(float[,] map, float centerX, float centerY, float radiusX, float radiusY, float strength)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            float v = (float)y / (height - 1);

            for (int x = 0; x < width; x++)
            {
                float u = (float)x / (width - 1);

                float dx = (u - centerX) / Mathf.Max(radiusX, 0.0001f);
                float dy = (v - centerY) / Mathf.Max(radiusY, 0.0001f);

                float gaussian = Mathf.Exp(-(dx * dx + dy * dy) * 0.5f) * strength;

                map[x, y] = Mathf.Max(map[x, y], gaussian);
            }
        }
    }

    private static void SubtractBlob(float[,] map, float centerX, float centerY, float radiusX, float radiusY, float strength)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            float v = (float)y / (height - 1);

            for (int x = 0; x < width; x++)
            {
                float u = (float)x / (width - 1);

                float dx = (u - centerX) / Mathf.Max(radiusX, 0.0001f);
                float dy = (v - centerY) / Mathf.Max(radiusY, 0.0001f);

                float gaussian = Mathf.Exp(-(dx * dx + dy * dy) * 0.5f) * strength;

                map[x, y] = Mathf.Max(0f, map[x, y] - gaussian);
            }
        }
    }

    private static void ApplyLowFrequencyNoise(float[,] map, float strength)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        float noiseOffsetX = Random.Range(-5000f, 5000f);
        float noiseOffsetY = Random.Range(-5000f, 5000f);

        for (int y = 0; y < height; y++)
        {
            float v = (float)y / height;

            for (int x = 0; x < width; x++)
            {
                float u = (float)x / width;

                float noiseA = Mathf.PerlinNoise(u * 3.1f + noiseOffsetX, v * 3.1f + noiseOffsetY);
                float noiseB = Mathf.PerlinNoise(u * 7.3f + noiseOffsetX * 0.37f, v * 7.3f + noiseOffsetY * 0.37f);

                float noise = noiseA * 0.72f + noiseB * 0.28f;
                float modulation = Mathf.Lerp(1f - strength, 1f + strength, noise);

                map[x, y] *= modulation;
            }
        }
    }

    private static void Normalize(float[,] map)
    {
        int width = map.GetLength(0);
        int height = map.GetLength(1);

        float maximum = 0f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                maximum = Mathf.Max(maximum, map[x, y]);
            }
        }

        if (maximum <= 0.0001f) return;

        float inverseMaximum = 1f / maximum;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                map[x, y] = Mathf.Pow(Mathf.Clamp01(map[x, y] * inverseMaximum), 0.82f);
            }
        }
    }

    private static Texture2D Blur(Texture2D source, int radius)
    {
        if (radius <= 0) return source;

        int width = source.width;
        int height = source.height;

        Color[] sourcePixels = source.GetPixels();
        Color[] result = new Color[sourcePixels.Length];

        int sampleRadius = Mathf.Clamp(radius, 1, 12);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float sum = 0f;
                int count = 0;

                for (int oy = -sampleRadius; oy <= sampleRadius; oy++)
                {
                    for (int ox = -sampleRadius; ox <= sampleRadius; ox++)
                    {
                        int sx = (x + ox + width) % width;
                        int sy = (y + oy + height) % height;

                        sum += sourcePixels[sy * width + sx].r;
                        count++;
                    }
                }

                float value = sum / count;
                result[y * width + x] = new Color(value, value, value, 1f);
            }
        }

        Texture2D output = new Texture2D(width, height, TextureFormat.R8, false, true);
        output.SetPixels(result);
        output.Apply(false, false);

        DestroyImmediate(source);
        return output;
    }
}