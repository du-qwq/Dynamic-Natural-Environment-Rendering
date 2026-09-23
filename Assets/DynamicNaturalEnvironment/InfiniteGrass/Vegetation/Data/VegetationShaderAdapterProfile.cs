using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "VSA_NewProfile", menuName = "Vegetation/Shader Adapter Profile")]
public class VegetationShaderAdapterProfile : ScriptableObject
{
    [Serializable]
    public class PropertyAlias
    {
        [Tooltip("原Shader中的属性名，例如 _MainTex")]
        public string sourceProperty;

        [Tooltip("目标Vegetation Shader中的属性名，例如 _BaseMap")]
        public string targetProperty;
    }

    [Serializable]
    public class ShaderMapping
    {
        [Tooltip("外部资产原始Shader")]
        public Shader sourceShader;

        [Tooltip("适配后的Vegetation Shader")]
        public Shader targetShader;

        [Tooltip("只有属性名发生变化时才需要填写")]
        public List<PropertyAlias> propertyAliases = new List<PropertyAlias>();
    }

    [Header("通用属性映射")]
    [Tooltip("所有Shader Mapping都会使用。适合 _MainTex → _BaseMap 这类通用映射")]
    public List<PropertyAlias> globalPropertyAliases = new List<PropertyAlias>();

    [Header("Shader映射")]
    public List<ShaderMapping> shaderMappings = new List<ShaderMapping>();

    public bool TryGetMapping(Shader sourceShader, out ShaderMapping mapping)
    {
        mapping = null;
        if (sourceShader == null || shaderMappings == null) return false;

        for (int i = 0; i < shaderMappings.Count; i++)
        {
            ShaderMapping current = shaderMappings[i];
            if (current == null || current.sourceShader != sourceShader) continue;

            mapping = current;
            return current.targetShader != null;
        }

        return false;
    }

    public bool IsTargetShader(Shader shader)
    {
        if (shader == null || shaderMappings == null) return false;

        for (int i = 0; i < shaderMappings.Count; i++)
        {
            ShaderMapping mapping = shaderMappings[i];
            if (mapping != null && mapping.targetShader == shader) return true;
        }

        return false;
    }

    public Shader GetTargetShader(Shader sourceShader)
    {
        return TryGetMapping(sourceShader, out ShaderMapping mapping) ? mapping.targetShader : null;
    }
}