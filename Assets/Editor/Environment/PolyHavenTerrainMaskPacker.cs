using UnityEditor;
using UnityEngine;
using System.IO;

public class PolyHavenTerrainMaskPacker : EditorWindow
{
    private Texture2D armTexture;
    private Texture2D heightTexture;
    private string outputName = "T_TerrainMask";

    [MenuItem("Tools/Environment/Poly Haven Terrain Mask Packer")]
    public static void Open()
    {
        GetWindow<PolyHavenTerrainMaskPacker>("Terrain Mask Packer");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Poly Haven → Unity Terrain Mask", EditorStyles.boldLabel);
        EditorGUILayout.Space(6);

        armTexture = (Texture2D)EditorGUILayout.ObjectField("AO/Rough/Metal", armTexture, typeof(Texture2D), false);
        heightTexture = (Texture2D)EditorGUILayout.ObjectField("Height / Displacement", heightTexture, typeof(Texture2D), false);
        outputName = EditorGUILayout.TextField("输出文件名", outputName);

        EditorGUILayout.Space(10);

        if (GUILayout.Button("生成 Terrain Mask Map", GUILayout.Height(32))) Generate();
    }

    private void Generate()
    {
        if (armTexture == null)
        {
            EditorUtility.DisplayDialog("缺少贴图", "请指定 AO/Rough/Metal 贴图。", "确定");
            return;
        }

        string armPath = AssetDatabase.GetAssetPath(armTexture);
        string heightPath = heightTexture != null ? AssetDatabase.GetAssetPath(heightTexture) : null;

        SetReadable(armPath, true);
        if (!string.IsNullOrEmpty(heightPath)) SetReadable(heightPath, true);

        Texture2D arm = AssetDatabase.LoadAssetAtPath<Texture2D>(armPath);
        Texture2D height = !string.IsNullOrEmpty(heightPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(heightPath) : null;

        int width = arm.width;
        int heightPixels = arm.height;

        Texture2D result = new Texture2D(width, heightPixels, TextureFormat.RGBA32, false, true);

        Color[] armPixels = arm.GetPixels();
        Color[] heightPixelsData = height != null ? height.GetPixels() : null;
        Color[] output = new Color[armPixels.Length];

        for (int i = 0; i < armPixels.Length; i++)
        {
            Color armPixel = armPixels[i];

            float ao = armPixel.r;
            float roughness = armPixel.g;
            float metallic = armPixel.b;
            float h = heightPixelsData != null && i < heightPixelsData.Length ? heightPixelsData[i].r : 0.5f;
            float smoothness = 1f - roughness;

            output[i] = new Color(metallic, ao, h, smoothness);
        }

        result.SetPixels(output);
        result.Apply();

        string directory = Path.GetDirectoryName(armPath);
        string outputPath = Path.Combine(directory, outputName + ".png").Replace("\\", "/");

        File.WriteAllBytes(outputPath, result.EncodeToPNG());
        DestroyImmediate(result);

        AssetDatabase.Refresh();

        TextureImporter importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;

        if (importer != null)
        {
            importer.sRGBTexture = false;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        SetReadable(armPath, false);
        if (!string.IsNullOrEmpty(heightPath)) SetReadable(heightPath, false);

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(outputPath);

        EditorUtility.DisplayDialog("完成", "Terrain Mask Map 已生成：\n" + outputPath, "确定");
    }

    private static void SetReadable(string path, bool readable)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.isReadable = readable;
        importer.SaveAndReimport();
    }
}