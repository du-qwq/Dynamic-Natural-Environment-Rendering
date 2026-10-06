#ifndef VEGETATION_LOD_INCLUDED
#define VEGETATION_LOD_INCLUDED

// Set per draw. Widths are zero when fading is disabled or a boundary has no
// adjacent valid mesh LOD. Culling and these fragments use the same pivot.
float _VegetationLODIndex;
float _VegetationLODOffset;
float4 _VegetationLODDistances;
float4 _VegetationLODCrossFadeParams;
float4 _VegetationLODReferencePosition;

float GetVegetationLODDistance(float3 pivotWS)
{
    return distance(pivotWS, _VegetationLODReferencePosition.xyz);
}

float VegetationLODDither(float2 pixelPosition)
{
    uint2 pixel = (uint2)floor(pixelPosition);
    uint hash = pixel.x * 0x9E3779B9u ^ pixel.y * 0x85EBCA6Bu;
    hash ^= hash >> 16;
    hash *= 0x7FEB352Du;
    hash ^= hash >> 15;
    return ((hash & 255u) + 0.5) / 256.0;
}

void ApplyVegetationLODCrossFade(float pivotDistance, float2 pixelPosition)
{
    if (all(_VegetationLODCrossFadeParams.xyz <= 0.0)) return;

    [unroll]
    for (int boundary = 0; boundary < 3; boundary++)
    {
        float width = _VegetationLODCrossFadeParams[boundary];
        if (width <= 0.0) continue;

        float threshold = _VegetationLODDistances[boundary];
        float start = threshold - width * 0.5;
        if (pivotDistance < start || pivotDistance >= threshold + width * 0.5) continue;

        float nearLOD = min((float)boundary + _VegetationLODOffset, 3.0);
        float farLOD = min(nearLOD + 1.0, 3.0);
        float t = saturate((pivotDistance - start) / width);
        float noise = VegetationLODDither(pixelPosition);

        if (_VegetationLODIndex == nearLOD) clip(noise - t);
        else if (_VegetationLODIndex == farLOD) clip(t - noise - 0.000001);
        return;
    }
}

#endif
