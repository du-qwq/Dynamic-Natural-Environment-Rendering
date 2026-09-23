using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class VegetationMaterialAdapter
{
    private const string MarkerPrefix = "VegetationMaterialAdapter:v1";
    private static readonly Dictionary<string, Material> SessionCache = new Dictionary<string, Material>();

    public static bool TryAdaptMaterial(Material sourceMaterial, VegetationShaderAdapterProfile profile, string outputFolder, out Material adaptedMaterial, out string error)
    {
        adaptedMaterial = null;
        error = null;

        if (sourceMaterial == null)
        {
            error = "源Material为空。";
            return false;
        }

        if (profile == null)
        {
            error = $"Material '{sourceMaterial.name}'：Shader Adapter Profile为空。";
            return false;
        }

        if (sourceMaterial.shader == null)
        {
            error = $"Material '{sourceMaterial.name}'：没有Shader。";
            return false;
        }

        if (profile.IsTargetShader(sourceMaterial.shader))
        {
            adaptedMaterial = sourceMaterial;
            return true;
        }

        if (!profile.TryGetMapping(sourceMaterial.shader, out VegetationShaderAdapterProfile.ShaderMapping mapping))
        {
            error = $"Material '{sourceMaterial.name}' 使用的Shader没有映射：{sourceMaterial.shader.name}";
            return false;
        }

        if (mapping.targetShader == null)
        {
            error = $"Material '{sourceMaterial.name}' 的目标Vegetation Shader为空。";
            return false;
        }

        string sourcePath = AssetDatabase.GetAssetPath(sourceMaterial);

        if (string.IsNullOrEmpty(sourcePath))
        {
            error = $"Material '{sourceMaterial.name}' 不是Project中的Material资产，无法生成持久化适配材质。";
            return false;
        }

        string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);

        if (string.IsNullOrEmpty(sourceGuid))
        {
            error = $"无法获取Material '{sourceMaterial.name}' 的GUID。";
            return false;
        }

        string profilePath = AssetDatabase.GetAssetPath(profile);

        if (string.IsNullOrEmpty(profilePath))
        {
            error = $"Shader Adapter Profile '{profile.name}' 必须先保存为Asset。";
            return false;
        }

        string profileGuid = AssetDatabase.AssetPathToGUID(profilePath);

        if (string.IsNullOrEmpty(profileGuid))
        {
            error = $"无法获取Shader Adapter Profile '{profile.name}' 的GUID。";
            return false;
        }

        outputFolder = NormalizeFolder(outputFolder);

        if (!EnsureFolder(outputFolder, out error)) return false;

        string cacheKey = BuildCacheKey(sourceGuid, profileGuid, outputFolder);

        if (SessionCache.TryGetValue(cacheKey, out Material cachedMaterial) && cachedMaterial != null)
        {
            ApplyConversion(sourceMaterial, cachedMaterial, profile, mapping);
            EditorUtility.SetDirty(cachedMaterial);
            adaptedMaterial = cachedMaterial;
            return true;
        }

        string marker = BuildMarker(sourceGuid, profileGuid);
        Material existingMaterial = FindExistingGeneratedMaterial(outputFolder, marker);

        if (existingMaterial != null)
        {
            ApplyConversion(sourceMaterial, existingMaterial, profile, mapping);
            EditorUtility.SetDirty(existingMaterial);
            SessionCache[cacheKey] = existingMaterial;
            adaptedMaterial = existingMaterial;
            return true;
        }

        Material newMaterial = new Material(sourceMaterial)
        {
            name = sourceMaterial.name + "_Vegetation"
        };

        newMaterial.shader = mapping.targetShader;
        ApplyConversion(sourceMaterial, newMaterial, profile, mapping);

        string assetPath = BuildNewMaterialPath(sourceMaterial, sourceGuid, outputFolder);

        AssetDatabase.CreateAsset(newMaterial, assetPath);
        WriteMarker(assetPath, marker);

        SessionCache[cacheKey] = newMaterial;
        adaptedMaterial = newMaterial;

        return true;
    }

    public static bool TryAdaptMaterials(Material[] sourceMaterials, VegetationShaderAdapterProfile profile, string outputFolder, out Material[] adaptedMaterials, out string error)
    {
        error = null;

        if (sourceMaterials == null)
        {
            adaptedMaterials = Array.Empty<Material>();
            return true;
        }

        adaptedMaterials = new Material[sourceMaterials.Length];

        for (int i = 0; i < sourceMaterials.Length; i++)
        {
            if (!TryAdaptMaterial(sourceMaterials[i], profile, outputFolder, out Material adaptedMaterial, out error))
            {
                return false;
            }

            adaptedMaterials[i] = adaptedMaterial;
        }

        return true;
    }

    public static bool CanAdaptMaterial(Material material, VegetationShaderAdapterProfile profile, out Shader targetShader)
    {
        targetShader = null;

        if (material == null || material.shader == null || profile == null) return false;

        if (profile.IsTargetShader(material.shader))
        {
            targetShader = material.shader;
            return true;
        }

        if (!profile.TryGetMapping(material.shader, out VegetationShaderAdapterProfile.ShaderMapping mapping)) return false;

        targetShader = mapping.targetShader;
        return targetShader != null;
    }

    public static void ClearSessionCache()
    {
        SessionCache.Clear();
    }

    private static void ApplyConversion(Material sourceMaterial, Material targetMaterial, VegetationShaderAdapterProfile profile, VegetationShaderAdapterProfile.ShaderMapping mapping)
    {
        if (sourceMaterial == null || targetMaterial == null || mapping == null || mapping.targetShader == null) return;

        targetMaterial.shader = mapping.targetShader;

        CopyMatchingProperties(sourceMaterial, targetMaterial);

        if (profile.globalPropertyAliases != null)
        {
            for (int i = 0; i < profile.globalPropertyAliases.Count; i++)
            {
                ApplyAlias(sourceMaterial, targetMaterial, profile.globalPropertyAliases[i]);
            }
        }

        if (mapping.propertyAliases != null)
        {
            for (int i = 0; i < mapping.propertyAliases.Count; i++)
            {
                ApplyAlias(sourceMaterial, targetMaterial, mapping.propertyAliases[i]);
            }
        }

        CopyShaderKeywords(sourceMaterial, targetMaterial);

        targetMaterial.enableInstancing = true;
        targetMaterial.doubleSidedGI = sourceMaterial.doubleSidedGI;
        targetMaterial.globalIlluminationFlags = sourceMaterial.globalIlluminationFlags;
        targetMaterial.renderQueue = sourceMaterial.renderQueue;
    }

    private static void CopyMatchingProperties(Material sourceMaterial, Material targetMaterial)
    {
        Shader sourceShader = sourceMaterial.shader;
        Shader targetShader = targetMaterial.shader;

        if (sourceShader == null || targetShader == null) return;

        int targetPropertyCount = targetShader.GetPropertyCount();

        for (int i = 0; i < targetPropertyCount; i++)
        {
            string propertyName = targetShader.GetPropertyName(i);

            if (!sourceMaterial.HasProperty(propertyName)) continue;
            if (!TryGetPropertyType(sourceShader, propertyName, out ShaderPropertyType sourceType)) continue;

            ShaderPropertyType targetType = targetShader.GetPropertyType(i);

            CopyProperty(sourceMaterial, propertyName, sourceType, targetMaterial, propertyName, targetType);
        }
    }

    private static void ApplyAlias(Material sourceMaterial, Material targetMaterial, VegetationShaderAdapterProfile.PropertyAlias alias)
    {
        if (alias == null) return;
        if (string.IsNullOrWhiteSpace(alias.sourceProperty) || string.IsNullOrWhiteSpace(alias.targetProperty)) return;
        if (!sourceMaterial.HasProperty(alias.sourceProperty) || !targetMaterial.HasProperty(alias.targetProperty)) return;
        if (!TryGetPropertyType(sourceMaterial.shader, alias.sourceProperty, out ShaderPropertyType sourceType)) return;
        if (!TryGetPropertyType(targetMaterial.shader, alias.targetProperty, out ShaderPropertyType targetType)) return;

        CopyProperty(sourceMaterial, alias.sourceProperty, sourceType, targetMaterial, alias.targetProperty, targetType);
    }

    private static void CopyProperty(Material sourceMaterial, string sourceProperty, ShaderPropertyType sourceType, Material targetMaterial, string targetProperty, ShaderPropertyType targetType)
    {
        if (IsFloatType(sourceType) && IsFloatType(targetType))
        {
            targetMaterial.SetFloat(targetProperty, sourceMaterial.GetFloat(sourceProperty));
            return;
        }

        if (sourceType == ShaderPropertyType.Texture && targetType == ShaderPropertyType.Texture)
        {
            targetMaterial.SetTexture(targetProperty, sourceMaterial.GetTexture(sourceProperty));
            targetMaterial.SetTextureScale(targetProperty, sourceMaterial.GetTextureScale(sourceProperty));
            targetMaterial.SetTextureOffset(targetProperty, sourceMaterial.GetTextureOffset(sourceProperty));
            return;
        }

        if (sourceType == ShaderPropertyType.Color && targetType == ShaderPropertyType.Color)
        {
            targetMaterial.SetColor(targetProperty, sourceMaterial.GetColor(sourceProperty));
            return;
        }

        if (sourceType == ShaderPropertyType.Vector && targetType == ShaderPropertyType.Vector)
        {
            targetMaterial.SetVector(targetProperty, sourceMaterial.GetVector(sourceProperty));
            return;
        }

        if (sourceType == ShaderPropertyType.Color && targetType == ShaderPropertyType.Vector)
        {
            Color color = sourceMaterial.GetColor(sourceProperty);
            targetMaterial.SetVector(targetProperty, new Vector4(color.r, color.g, color.b, color.a));
            return;
        }

        if (sourceType == ShaderPropertyType.Vector && targetType == ShaderPropertyType.Color)
        {
            Vector4 vector = sourceMaterial.GetVector(sourceProperty);
            targetMaterial.SetColor(targetProperty, new Color(vector.x, vector.y, vector.z, vector.w));
        }
    }

    private static void CopyShaderKeywords(Material sourceMaterial, Material targetMaterial)
    {
        if (sourceMaterial == null || targetMaterial == null || sourceMaterial.shader == null || targetMaterial.shader == null) return;

        LocalKeyword[] targetKeywords = targetMaterial.shader.keywordSpace.keywords;

        for (int i = 0; i < targetKeywords.Length; i++)
        {
            string keywordName = targetKeywords[i].name;

            if (string.IsNullOrEmpty(keywordName)) continue;

            if (sourceMaterial.IsKeywordEnabled(keywordName))
                targetMaterial.EnableKeyword(keywordName);
            else
                targetMaterial.DisableKeyword(keywordName);
        }
    }

    private static bool IsFloatType(ShaderPropertyType type)
    {
        return type == ShaderPropertyType.Float || type == ShaderPropertyType.Range || type == ShaderPropertyType.Int;
    }

    private static bool TryGetPropertyType(Shader shader, string propertyName, out ShaderPropertyType propertyType)
    {
        propertyType = default;

        if (shader == null || string.IsNullOrEmpty(propertyName)) return false;

        int propertyCount = shader.GetPropertyCount();

        for (int i = 0; i < propertyCount; i++)
        {
            if (shader.GetPropertyName(i) != propertyName) continue;

            propertyType = shader.GetPropertyType(i);
            return true;
        }

        return false;
    }

    private static Material FindExistingGeneratedMaterial(string outputFolder, string marker)
    {
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { outputFolder });

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AssetImporter importer = AssetImporter.GetAtPath(path);

            if (importer == null || importer.userData != marker) continue;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material != null) return material;
        }

        return null;
    }

    private static string BuildNewMaterialPath(Material sourceMaterial, string sourceGuid, string outputFolder)
    {
        string cleanName = SanitizeFileName(sourceMaterial.name);
        string preferredPath = $"{outputFolder}/{cleanName}_Vegetation.mat";

        Material existing = AssetDatabase.LoadAssetAtPath<Material>(preferredPath);

        if (existing == null) return preferredPath;

        string shortGuid = sourceGuid.Length >= 8 ? sourceGuid.Substring(0, 8) : sourceGuid;
        string fallbackPath = $"{outputFolder}/{cleanName}_Vegetation_{shortGuid}.mat";

        if (AssetDatabase.LoadAssetAtPath<Material>(fallbackPath) == null) return fallbackPath;

        return AssetDatabase.GenerateUniqueAssetPath(fallbackPath);
    }

    private static string BuildCacheKey(string sourceGuid, string profileGuid, string outputFolder)
    {
        return sourceGuid + "|" + profileGuid + "|" + outputFolder;
    }

    private static string BuildMarker(string sourceGuid, string profileGuid)
    {
        return $"{MarkerPrefix}|source={sourceGuid}|profile={profileGuid}";
    }

    private static void WriteMarker(string assetPath, string marker)
    {
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);

        if (importer == null) return;

        importer.userData = marker;
        AssetDatabase.WriteImportSettingsIfDirty(assetPath);
    }

    private static string NormalizeFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return "Assets/VegetationGenerated/Materials";

        folder = folder.Replace("\\", "/").Trim();

        while (folder.EndsWith("/"))
        {
            folder = folder.Substring(0, folder.Length - 1);
        }

        return folder;
    }

    private static bool EnsureFolder(string folder, out string error)
    {
        error = null;

        if (AssetDatabase.IsValidFolder(folder)) return true;

        string[] parts = folder.Split('/');

        if (parts.Length == 0 || parts[0] != "Assets")
        {
            error = $"输出目录必须位于Assets中：{folder}";
            return false;
        }

        string current = "Assets";

        for (int i = 1; i < parts.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(parts[i])) continue;

            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }

        return AssetDatabase.IsValidFolder(folder);
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "Material";

        char[] invalidChars = Path.GetInvalidFileNameChars();

        for (int i = 0; i < invalidChars.Length; i++)
        {
            fileName = fileName.Replace(invalidChars[i], '_');
        }

        fileName = fileName.Replace('/', '_').Replace('\\', '_');

        return fileName;
    }
}