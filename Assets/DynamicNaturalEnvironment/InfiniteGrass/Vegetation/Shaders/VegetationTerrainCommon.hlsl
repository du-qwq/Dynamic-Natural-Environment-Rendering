#ifndef VEGETATION_TERRAIN_COMMON_INCLUDED
#define VEGETATION_TERRAIN_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_VegetationTerrainColorMap);
SAMPLER(sampler_VegetationTerrainColorMap);

float _VegetationTerrainColorEnabled;
float4 _VegetationTerrainOriginSize;

float2 GetVegetationTerrainUV(float3 positionWS)
{
    float2 origin = _VegetationTerrainOriginSize.xy;
    float2 size = max(_VegetationTerrainOriginSize.zw, float2(0.001, 0.001));
    return (positionWS.xz - origin) / size;
}

float3 SampleVegetationTerrainColor(float2 terrainUV)
{
    return SAMPLE_TEXTURE2D_LOD(_VegetationTerrainColorMap, sampler_VegetationTerrainColorMap, saturate(terrainUV), 0).rgb;
}

#endif