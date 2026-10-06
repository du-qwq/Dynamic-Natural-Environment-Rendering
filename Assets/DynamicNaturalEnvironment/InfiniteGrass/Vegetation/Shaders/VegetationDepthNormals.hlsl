#ifndef VEGETATION_DEPTH_NORMALS_INCLUDED
#define VEGETATION_DEPTH_NORMALS_INCLUDED

// Matches URP 14 DepthNormalsPass.hlsl and its signed normals render target.
half4 EncodeVegetationDepthNormal(float3 normalWS)
{
    normalWS = NormalizeNormalPerPixel(normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
    return half4(PackFloat2To888(remappedOctNormalWS), 0.0);
#else
    return half4(normalWS, 0.0);
#endif
}

#endif
