using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class CreateVolumetricCloudsMaterial
{
    private const string RootPath = "Assets/VolumetricClouds";
    private const string MaterialPath = RootPath + "/VolumetricClouds.mat";

    [MenuItem("Tools/Volumetric Clouds/Create Material")]
    private static void CreateMaterial()
    {
        Shader shader = Shader.Find("Hidden/Sky/VolumetricClouds");

        if (shader == null)
        {
            Debug.LogError("没有找到 Shader：Hidden/Sky/VolumetricClouds。请检查 VolumetricClouds.shader 是否存在编译错误。");
            return;
        }

        Texture cloudLut = AssetDatabase.LoadAssetAtPath<Texture>(RootPath + "/Textures/CloudLutRainAO.png");
        Texture erosionNoise = AssetDatabase.LoadAssetAtPath<Texture>(RootPath + "/Textures/PerlinNoise32RGB.png");
        Texture worleyNoise = AssetDatabase.LoadAssetAtPath<Texture>(RootPath + "/Textures/WorleyNoise128RGBA.png");

        if (cloudLut == null)
        {
            Debug.LogError("没有找到 CloudLutRainAO.png，查找路径：" + RootPath + "/Textures/CloudLutRainAO.png");
            return;
        }

        if (erosionNoise == null)
        {
            Debug.LogError("没有找到 PerlinNoise32RGB.png，查找路径：" + RootPath + "/Textures/PerlinNoise32RGB.png");
            return;
        }

        if (worleyNoise == null)
        {
            Debug.LogError("没有找到 WorleyNoise128RGBA.png，查找路径：" + RootPath + "/Textures/WorleyNoise128RGBA.png");
            return;
        }

        if (cloudLut.dimension != TextureDimension.Tex2D)
        {
            Debug.LogError("CloudLutRainAO 必须导入为 Texture2D，当前类型：" + cloudLut.dimension);
            return;
        }

        if (erosionNoise.dimension != TextureDimension.Tex3D)
        {
            Debug.LogError("PerlinNoise32RGB 必须导入为 Texture3D，当前类型：" + erosionNoise.dimension + "。请确认复制了原项目对应的 .meta 文件。");
            return;
        }

        if (worleyNoise.dimension != TextureDimension.Tex3D)
        {
            Debug.LogError("WorleyNoise128RGBA 必须导入为 Texture3D，当前类型：" + worleyNoise.dimension + "。请确认复制了原项目对应的 .meta 文件。");
            return;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (material == null)
        {
            material = new Material(shader);
            material.name = "VolumetricClouds";
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        material.SetTexture("_CloudLutTexture", cloudLut);
        material.SetTexture("_ErosionNoise", erosionNoise);
        material.SetTexture("_Worley128RGBA", worleyNoise);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = material;
        EditorGUIUtility.PingObject(material);

        Debug.Log("体积云材质创建完成：" + MaterialPath);
    }
}