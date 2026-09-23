using System.IO;
using UnityEditor;
using UnityEngine;

public class TerrainColorMapBakerWindow : EditorWindow
{
    private Terrain terrain;
    private VegetationTerrainColorController controller;
    private int resolution = 1024;

    [MenuItem("Tools/Vegetation/Terrain Color Map Baker")]
    public static void Open()
    {
        GetWindow<TerrainColorMapBakerWindow>("Terrain Color Baker");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Terrain Color Map Baker", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        controller = (VegetationTerrainColorController)EditorGUILayout.ObjectField("Controller", controller, typeof(VegetationTerrainColorController), true);

        resolution = EditorGUILayout.IntPopup("Resolution", resolution, new[] { "256", "512", "1024", "2048" }, new[] { 256, 512, 1024, 2048 });

        EditorGUILayout.Space();

        if (terrain == null) EditorGUILayout.HelpBox("请选择需要烘焙的Terrain。", MessageType.Info);
        else
        {
            TerrainData data = terrain.terrainData;
            EditorGUILayout.LabelField($"Terrain Size: {data.size.x:F0} x {data.size.z:F0}");
            EditorGUILayout.LabelField($"Terrain Layers: {data.terrainLayers.Length}");
            EditorGUILayout.LabelField($"Alphamap: {data.alphamapWidth} x {data.alphamapHeight}");
        }

        EditorGUILayout.Space();

        GUI.enabled = terrain != null && terrain.terrainData != null;

        if (GUILayout.Button("Bake Terrain Color Map", GUILayout.Height(34f))) Bake();

        GUI.enabled = true;
    }

    private void Bake()
    {
        TerrainData data = terrain.terrainData;
        TerrainLayer[] layers = data.terrainLayers;

        if (layers == null || layers.Length == 0)
        {
            Debug.LogError("Terrain Color Baker：当前Terrain没有Terrain Layer。");
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject("保存 Terrain Color Map", "T_TerrainColorMap", "png", "选择Terrain Color Map保存位置。");
        if (string.IsNullOrEmpty(path)) return;

        float[,,] alphamaps = data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight);
        Texture2D[] readableTextures = new Texture2D[layers.Length];

        try
        {
            for (int i = 0; i < layers.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Terrain Color Map", $"读取 Terrain Layer {i + 1}/{layers.Length}", 0f);
                if (layers[i] != null && layers[i].diffuseTexture != null) readableTextures[i] = CreateReadableCopy(layers[i].diffuseTexture);
            }

            Texture2D result = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, false);
            Color[] pixels = new Color[resolution * resolution];

            for (int y = 0; y < resolution; y++)
            {
                float v = resolution <= 1 ? 0f : y / (float)(resolution - 1);

                for (int x = 0; x < resolution; x++)
                {
                    float u = resolution <= 1 ? 0f : x / (float)(resolution - 1);
                    float localX = u * data.size.x;
                    float localZ = v * data.size.z;

                    Color finalColor = Color.black;
                    float totalWeight = 0f;

                    for (int layerIndex = 0; layerIndex < layers.Length; layerIndex++)
                    {
                        TerrainLayer layer = layers[layerIndex];
                        if (layer == null) continue;

                        float weight = SampleAlphamap(alphamaps, layerIndex, u, v);
                        if (weight <= 0.0001f) continue;

                        Texture2D texture = readableTextures[layerIndex];
                        Color layerColor = Color.white;

                        if (texture != null)
                        {
                            float tileX = Mathf.Abs(layer.tileSize.x) > 0.0001f ? layer.tileSize.x : 1f;
                            float tileZ = Mathf.Abs(layer.tileSize.y) > 0.0001f ? layer.tileSize.y : 1f;

                            float layerU = Mathf.Repeat((localX + layer.tileOffset.x) / tileX, 1f);
                            float layerV = Mathf.Repeat((localZ + layer.tileOffset.y) / tileZ, 1f);

                            layerColor = texture.GetPixelBilinear(layerU, layerV);
                        }

                        finalColor += layerColor * weight;
                        totalWeight += weight;
                    }

                    if (totalWeight > 0.0001f) finalColor /= totalWeight;
                    else finalColor = Color.white;

                    finalColor.a = 1f;
                    pixels[y * resolution + x] = finalColor;
                }

                if (y % 16 == 0) EditorUtility.DisplayProgressBar("Terrain Color Map", $"烘焙 {y + 1}/{resolution}", y / (float)resolution);
            }

            result.SetPixels(pixels);
            result.Apply(false, false);

            File.WriteAllBytes(Path.GetFullPath(path), result.EncodeToPNG());
            DestroyImmediate(result);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer != null)
            {
                importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = true;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.maxTextureSize = resolution;
                importer.SaveAndReimport();
            }

            Texture2D bakedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            if (controller != null)
            {
                Undo.RecordObject(controller, "Assign Terrain Color Map");
                controller.terrain = terrain;
                controller.terrainColorMap = bakedTexture;
                EditorUtility.SetDirty(controller);
            }

            Debug.Log($"Terrain Color Map烘焙完成：{path}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();

            for (int i = 0; i < readableTextures.Length; i++)
            {
                if (readableTextures[i] != null) DestroyImmediate(readableTextures[i]);
            }
        }
    }

    private static float SampleAlphamap(float[,,] alphamaps, int layer, float u, float v)
    {
        int height = alphamaps.GetLength(0);
        int width = alphamaps.GetLength(1);

        float fx = Mathf.Clamp01(u) * (width - 1);
        float fy = Mathf.Clamp01(v) * (height - 1);

        int x0 = Mathf.FloorToInt(fx);
        int y0 = Mathf.FloorToInt(fy);
        int x1 = Mathf.Min(x0 + 1, width - 1);
        int y1 = Mathf.Min(y0 + 1, height - 1);

        float tx = fx - x0;
        float ty = fy - y0;

        float a = Mathf.Lerp(alphamaps[y0, x0, layer], alphamaps[y0, x1, layer], tx);
        float b = Mathf.Lerp(alphamaps[y1, x0, layer], alphamaps[y1, x1, layer], tx);

        return Mathf.Lerp(a, b, ty);
    }

    private static Texture2D CreateReadableCopy(Texture2D source)
    {
        RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;

        Graphics.Blit(source, rt);
        RenderTexture.active = rt;

        Texture2D copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
        copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        copy.Apply(false, false);
        copy.wrapMode = TextureWrapMode.Repeat;
        copy.filterMode = FilterMode.Bilinear;

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        return copy;
    }
}