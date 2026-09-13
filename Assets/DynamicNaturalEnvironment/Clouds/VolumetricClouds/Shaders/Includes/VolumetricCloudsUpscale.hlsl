#ifndef URP_VOLUMETRIC_CLOUDS_UPSCALE_HLSL
#define URP_VOLUMETRIC_CLOUDS_UPSCALE_HLSL

#define KERNEL_SIZE 3
#define UPSAMPLE_EPSILON 1e-5h

half ComputeUpsampleWeight(half spatialDistance, half valueDifference)
{
    half spatialWeight = exp(-spatialDistance * spatialDistance);
    return spatialWeight * rcp(valueDifference + UPSAMPLE_EPSILON);
}

float2 GetLowResolutionPixelCenter(float2 screenUV)
{
    float2 lowResolutionSize = _VolumetricCloudsLightingTexture_TexelSize.zw;
    float2 lowResolutionTexelSize = _VolumetricCloudsLightingTexture_TexelSize.xy;
    return (floor(screenUV * lowResolutionSize) + 0.5) * lowResolutionTexelSize;
}

half4 BilateralUpscale(float2 screenUV)
{
    float2 centerUV = GetLowResolutionPixelCenter(screenUV);
    float2 texelSize = _VolumetricCloudsLightingTexture_TexelSize.xy;

    half4 referenceColor = SAMPLE_TEXTURE2D_X_LOD(
        _VolumetricCloudsLightingTexture,
        s_linear_clamp_sampler,
        screenUV,
        0
    );

    half4 accumulatedColor = 0.0h;
    half accumulatedWeight = 0.0h;

    for (int y = -KERNEL_SIZE; y <= KERNEL_SIZE; y++)
    {
        for (int x = -KERNEL_SIZE; x <= KERNEL_SIZE; x++)
        {
            float2 sampleUV = centerUV + float2(x, y) * texelSize;

            half4 sampleColor = SAMPLE_TEXTURE2D_X_LOD(
                _VolumetricCloudsLightingTexture,
                s_linear_clamp_sampler,
                sampleUV,
                0
            );

            half2 pixelOffset = (screenUV - sampleUV) * _ScreenParams.xy;
            half spatialDistance = length(pixelOffset);
            half colorDifference = length(referenceColor - sampleColor);
            half sampleWeight = ComputeUpsampleWeight(spatialDistance, colorDifference);

            accumulatedColor += sampleColor * sampleWeight;
            accumulatedWeight += sampleWeight;
        }
    }

    return accumulatedColor * rcp(max(accumulatedWeight, UPSAMPLE_EPSILON));
}

half BilateralUpscaleTransmittance(float2 screenUV)
{
    float2 centerUV = GetLowResolutionPixelCenter(screenUV);
    float2 texelSize = _VolumetricCloudsLightingTexture_TexelSize.xy;

    half referenceTransmittance = SAMPLE_TEXTURE2D_X_LOD(
        _VolumetricCloudsLightingTexture,
        s_linear_clamp_sampler,
        screenUV,
        0
    ).a;

    half accumulatedTransmittance = 0.0h;
    half accumulatedWeight = 0.0h;

    for (int y = -KERNEL_SIZE; y <= KERNEL_SIZE; y++)
    {
        for (int x = -KERNEL_SIZE; x <= KERNEL_SIZE; x++)
        {
            float2 sampleUV = centerUV + float2(x, y) * texelSize;

            half sampleTransmittance = SAMPLE_TEXTURE2D_X_LOD(
                _VolumetricCloudsLightingTexture,
                s_linear_clamp_sampler,
                sampleUV,
                0
            ).a;

            half2 pixelOffset = (screenUV - sampleUV) * _ScreenParams.xy;
            half spatialDistance = length(pixelOffset);
            half transmittanceDifference = abs(referenceTransmittance - sampleTransmittance);
            half sampleWeight = ComputeUpsampleWeight(spatialDistance, transmittanceDifference);

            accumulatedTransmittance += sampleTransmittance * sampleWeight;
            accumulatedWeight += sampleWeight;
        }
    }

    return accumulatedTransmittance * rcp(max(accumulatedWeight, UPSAMPLE_EPSILON));
}

#endif